using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

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

        private static async Task SendAsync(WebSocket clientWebSocket, string data,
            CancellationToken cancellationToken)
        {
            var subBytes = Encoding.UTF8.GetBytes(data);
            await clientWebSocket.SendAsync(
                new ArraySegment<byte>(subBytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var contractIds = await _mediator.GetContractsAsync();

            using var clientSocket = new ClientWebSocket();
            clientSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            clientSocket.Options.SetRequestHeader("User-Agent", "Auto-Invest");
            await clientSocket.ConnectAsync(new Uri($"{_serverConfig.WebSocketUrl}/v1/api/ws"), stoppingToken);

            var resultsChannel = Channel.CreateUnbounded<JsonElement>();
            _mediator.RegisterWebSocketResults(resultsChannel.Reader);

            foreach (var contractId in contractIds)
            {
                if (string.IsNullOrEmpty(contractId.ConId)) continue;

                await SendAsync(clientSocket, $"smd+{contractId.ConId}+{{\"fields\":[\"31\"]}}", stoppingToken);
            }

            await SendAsync(clientSocket, "str+{}", stoppingToken);

            while (
                !stoppingToken.IsCancellationRequested &&
                clientSocket.CloseStatus == null &&
                clientSocket.State == WebSocketState.Open)
            {
                var socketElement = await ReceiveMessageAsync(clientSocket, stoppingToken);
                await resultsChannel.Writer.WriteAsync(socketElement, stoppingToken);
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
