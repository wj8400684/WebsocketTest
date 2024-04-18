using Microsoft.Extensions.Hosting;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket.Server;

await WebSocketHostBuilder.Create()
                          .UsePipelineFilter<WebsocketPipeLineFilter.WebSocketPipelineFilter>()
                          .UseWebSocketMessageHandler(async (session, message) =>
                          {
                                Console.WriteLine(message.Message);
                                await Task.CompletedTask;
                          })
                          .ConfigureSuperSocket(options=>
                          {
                            options.AddListener(new ListenOptions
                            {
                                Ip = "Any",
                                Port = 8001,
                            });
                          }).RunConsoleAsync();