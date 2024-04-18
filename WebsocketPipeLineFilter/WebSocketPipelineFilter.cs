using System.Buffers;
using System.Collections.Specialized;
using System.Text;
using SuperSocket.ProtoBase;
using SuperSocket.WebSocket;

namespace WebsocketPipeLineFilter;

public class WebSocketPipelineFilterP : IPipelineFilter<WebSocketPackage>
{
    private static ReadOnlySpan<byte> _CRLF => new byte[] { (byte)'\r', (byte)'\n' };
    
    private static readonly char _TAB = '\t';

    private static readonly char _COLON = ':';

    private static readonly ReadOnlyMemory<byte> _headerTerminator = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
    
    public IPackageDecoder<WebSocketPackage> Decoder { get; set; }

    public IPipelineFilter<WebSocketPackage> NextFilter { get; internal set; }

    public WebSocketPackage Filter(ref SequenceReader<byte> reader)
    {
        var terminatorSpan = _headerTerminator.Span;

        if (!reader.TryReadTo(out ReadOnlySequence<byte> pack, terminatorSpan, advancePastDelimiter: false))
            return null;

        reader.Advance(terminatorSpan.Length);

        var package = ParseHandshake(ref pack);

        NextFilter = new WebSocketDataPipelineFilter(package.HttpHeader);
        
        return package;
    }

    private WebSocketPackage ParseHandshake(ref ReadOnlySequence<byte> pack)
    {
        var header = ParseHttpHeaderItems(ref pack);

        return new WebSocketPackage
        {
            HttpHeader = header,
            OpCode = OpCode.Handshake
        };
    }

    private bool TryParseHttpHeaderItems(ref ReadOnlySequence<byte> header, out string firstLine, out NameValueCollection items)
    {
        var headerText = header.GetString(Encoding.UTF8);
        var reader = new StringReader(headerText);
        firstLine = reader.ReadLine();

        if (string.IsNullOrEmpty(firstLine))
        {
            items = null;
            return false;
        }

        items = new NameValueCollection();

        var prevKey = string.Empty;
        var line = string.Empty;
        
        while (!string.IsNullOrEmpty(line = reader.ReadLine()))
        {
            if (line.StartsWith(_TAB) && !string.IsNullOrEmpty(prevKey))
            {
                var currentValue = items.Get(prevKey);
                items[prevKey] = currentValue + line.Trim();
                continue;
            }

            int pos = line.IndexOf(_COLON);

            if (pos <= 0)
                continue;

            string key = line.Substring(0, pos);

            if (!string.IsNullOrEmpty(key))
                key = key.Trim();

            if (string.IsNullOrEmpty(key))
                continue;

            var valueOffset = pos + 1;

            if (line.Length <= valueOffset) //No value in this line
                continue;

            var value = line.Substring(valueOffset);

            if (!string.IsNullOrEmpty(value) && value.StartsWith(' ') && value.Length > 1)
                value = value.Substring(1);

            var existingValue = items.Get(key);

            if (string.IsNullOrEmpty(existingValue))
            {
                items.Add(key, value);
            }
            else
            {
                items[key] = existingValue + ", " + value;
            }

            prevKey = key;
        }

        return true;
    }

    protected virtual HttpHeader CreateHttpHeader(string verbItem1, string verbItem2, string verbItem3, NameValueCollection items)
    {
        return HttpHeader.CreateForRequest(verbItem1, verbItem2, verbItem3, items);
    }

    private HttpHeader ParseHttpHeaderItems(ref ReadOnlySequence<byte> header)
    {
        if (!TryParseHttpHeaderItems(ref header, out var firstLine, out var items))
            return null;

        var verbItems = firstLine.Split(' ', 3);

        if (verbItems.Length < 3)
        {
            // invalid first line
            return null;
        }

        return CreateHttpHeader(verbItems[0], verbItems[1], verbItems[2], items);
    }

    public void Reset()
    {
        
    }

    public object Context { get; set; }
}

public sealed class WebSocketPipelineFilter : IPipelineFilter<WebSocketPackage>
{
    private static ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
    private static ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

    public IPackageDecoder<WebSocketPackage> Decoder { get; set; }

    public IPipelineFilter<WebSocketPackage> NextFilter { get; internal set; }

    public object Context { get; set; }

    public WebSocketPackage Filter(ref SequenceReader<byte> reader)
    {
        if (!reader.TryReadTo(out ReadOnlySpan<byte> methodSpan, (byte)' '))
            return null;

        if (!reader.TryReadTo(out ReadOnlySpan<byte> pathSpan, (byte)' '))
            return null;

        if (!reader.TryReadTo(out ReadOnlySequence<byte> versionSpan, NewLine))
            return null;

        var method = Encoding.ASCII.GetString(methodSpan);
        var requestUri = Encoding.ASCII.GetString(pathSpan);
        var version = Encoding.ASCII.GetString(versionSpan.IsSingleSegment ? versionSpan.FirstSpan : versionSpan.ToArray());

        var items = new NameValueCollection();
        
        while (reader.TryReadTo(out ReadOnlySequence<byte> headerLine, NewLine)) 
        {
            if (headerLine.Length == 0)
                break;

            ParseHeader(headerLine, out var headerName, out var headerValue);

            var key = Encoding.ASCII.GetString(headerName.Trim(TrimChars));
            var value = Encoding.ASCII.GetString(headerValue.Trim(TrimChars));

            items.Add(key, value);
        }

        var httpHeader = HttpHeader.CreateForRequest(method, requestUri, version, items);

        NextFilter = new WebSocketDataPipelineFilter(httpHeader);

        return new WebSocketPackage
        {
            HttpHeader = httpHeader,
            OpCode = OpCode.Handshake
        };
    }

    internal static void ParseHeader(in ReadOnlySequence<byte> headerLine, out ReadOnlySpan<byte> headerName, out ReadOnlySpan<byte> headerValue)
    {
        if (headerLine.IsSingleSegment)
        {
            var span = headerLine.FirstSpan;
            var colon = span.IndexOf((byte)':');
            headerName = span.Slice(0, colon);
            headerValue = span.Slice(colon + 1);
        }
        else
        {
            var headerReader = new SequenceReader<byte>(headerLine);
            headerReader.TryReadTo(out headerName, (byte)':');
            var remaining = headerReader.Sequence.Slice(headerReader.Position);
            headerValue = remaining.IsSingleSegment ? remaining.FirstSpan : remaining.ToArray();
        }
    }

    public void Reset()
    {
        
    }
}