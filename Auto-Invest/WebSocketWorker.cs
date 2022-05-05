﻿using System.Net.WebSockets;
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

            await clientSocket.ConnectAsync(new Uri($"{_serverConfig.WebSocketUrl}/ws/socketEndPoint"), stoppingToken);

            var orderChannel = Channel.CreateUnbounded<CompletedOrder>();
            var tickChannel = Channel.CreateUnbounded<TickPosition>();

            _mediator.RegisterCompletedOrderReader(orderChannel);
            _mediator.RegisterTickPositionReader(tickChannel);

            foreach (var contractId in contractIds)
            {
                var subscription = $"smd+{contractId.ConId}+{{\"fields\":[\"31\"]}}";
                var subBytes = Encoding.UTF8.GetBytes(subscription);
                await clientSocket.SendAsync(
                    new ArraySegment<byte>(subBytes),
                    WebSocketMessageType.Text,
                    true,
                    stoppingToken);
            }

            while (
                !stoppingToken.IsCancellationRequested &&
                clientSocket.CloseStatus == null &&
                clientSocket.State == WebSocketState.Open)
            {
                var stream = await ReceiveMessageAsync(clientSocket, stoppingToken);

                var completedOrder = await JsonSerializer.DeserializeAsync<CompletedOrder>(stream, cancellationToken: stoppingToken);
                if (completedOrder != null) await orderChannel.Writer.WriteAsync(completedOrder, stoppingToken);

                var tickPosition =
                    await JsonSerializer.DeserializeAsync<TickPosition>(stream, cancellationToken: stoppingToken);
                if (tickPosition != null) await tickChannel.Writer.WriteAsync(tickPosition, stoppingToken);
            }
        }

        private static async Task<Stream> ReceiveMessageAsync(WebSocket socket, CancellationToken cancel)
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
            return stream;
        }
    }
}