using System.Buffers;
using System.Net.WebSockets;

await Parallel.ForEachAsync(Enumerable.Range(0, 100000), 
async (index, ct) =>
{
    var websocket = new ClientWebSocket();

    await websocket.ConnectAsync(new Uri($"ws://localhost:8001/test"), CancellationToken.None);

    Console.WriteLine("连接成功");

    //StartReceiveAsync(websocket);
    await websocket.CloseAsync(WebSocketCloseStatus.ProtocolError, null, ct);
});

async void StartReceiveAsync(ClientWebSocket webSocket)
{
    while (true)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);

        try
        {
            await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }    
    }
}

Console.WriteLine("完毕");
await Task.Delay(1000000000);