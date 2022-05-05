using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class WebSocketWorker : BackgroundService
    {
        private readonly LocalServerConfig _serverConfig;
        private readonly IMediator _mediator;

        public WebSocketWorker(LocalServerConfig serverConfig, IMediator mediator)
        {
            _serverConfig = serverConfig;
            _mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var contractIds = await _mediator.GetContractsAsync();

            using var clientSocket = new ClientWebSocket();
            clientSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            clientSocket.Options.SetRequestHeader("User-Agent", "Auto-Invest");

            await clientSocket.ConnectAsync(new Uri($"{_serverConfig.WebSocketUrl}/v1/api/ws"), stoppingToken);

            var orderChannel = Channel.CreateUnbounded<CompletedOrder>();
            var tickChannel = Channel.CreateUnbounded<TickPosition>();

            _mediator.RegisterCompletedOrderReader(orderChannel.Reader);
            _mediator.RegisterTickPositionReader(tickChannel.Reader);
            var symbolLookup = new Dictionary<string, string>();

            foreach (var contractId in contractIds)
            {
                //todo make this a function that takes in string and socket
                var subscription = $"smd+{contractId.ConId}+{{\"fields\":[\"31\"]}}";
                var subBytes = Encoding.UTF8.GetBytes(subscription);
                await clientSocket.SendAsync(
                    new ArraySegment<byte>(subBytes),
                    WebSocketMessageType.Text,
                    true,
                    stoppingToken);

                if (string.IsNullOrEmpty(contractId.ConId)) continue;
                symbolLookup[contractId.ConId] = contractId.Symbol;
            }

            while (
                !stoppingToken.IsCancellationRequested &&
                clientSocket.CloseStatus == null &&
                clientSocket.State == WebSocketState.Open)
            {
                var socketElement = await ReceiveMessageAsync(clientSocket, stoppingToken);

                //todo lets get another background service working here instead to pickup the JsonElement
                var topic = socketElement.GetProperty("topic").GetString();

                if (string.IsNullOrWhiteSpace(topic)) continue;

                if (topic[..3] == "smd")
                {
                    var conId = topic[4..];
                    if (!symbolLookup.ContainsKey(conId)) continue;
                    var symbol = symbolLookup[conId];

                    var priceString = socketElement.GetProperty("31").GetString() ?? "";
                    if (!decimal.TryParse(priceString, out var price)) continue;

                    var position = new TickPosition
                    {
                        Position = price,
                        Symbol = symbol
                    };

                    await tickChannel.Writer.WriteAsync(position, stoppingToken);
                }

                //todo finish this up for sure
                if (topic == "str")
                {
                    var args = socketElement.GetProperty("args");
                    foreach (var arg in args.EnumerateArray())
                    {

                    }
                }
            }
        }

        private static async Task<JsonElement> ReceiveMessageAsync(WebSocket socket, CancellationToken cancel)
        {
            await using var stream = new MemoryStream();
            var buffer = WebSocket.CreateServerBuffer(1024);

            while (true)
            {
                var received = await socket.ReceiveAsync(buffer, cancel);
                if (buffer.Array == null) break;

                stream.Write(buffer.Array, buffer.Offset, received.Count);
                if (received.EndOfMessage) break;
            }

            stream.Position = 0;
            var element = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancel);
            return element;
        }
    }
}
