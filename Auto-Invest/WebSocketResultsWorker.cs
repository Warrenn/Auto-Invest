using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class WebSocketResultsWorker : BackgroundService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<WebSocketResultsWorker> _logger;

        public WebSocketResultsWorker(IMediator mediator, ILogger<WebSocketResultsWorker> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        #region Overrides of BackgroundService

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WebSocketResultsWorker Started awaiting dependencies");
            var contractIds = await _mediator.GetContractsAsync();
            var socketResultsReader = await _mediator.GetWebSocketResultsReaderAsync();

            var symbolLookup = contractIds
                .Where(_ => !string.IsNullOrWhiteSpace(_.ConId))
                .ToDictionary(x => x.ConId ?? "", x => x.Symbol);

            var orderChannel = Channel.CreateUnbounded<CompletedOrder>();
            var tickChannel = Channel.CreateUnbounded<TickPosition>();

            _mediator.RegisterCompletedOrderReader(orderChannel.Reader);
            _mediator.RegisterTickPositionReader(tickChannel.Reader);

            _logger.LogInformation("WebSocketResultsWorker processing websocket messages");
            await foreach (var socketElement in socketResultsReader.ReadAllAsync(stoppingToken))
            {
                var topic = socketElement.GetProperty("topic").GetString();

                if (string.IsNullOrWhiteSpace(topic)) continue;

                if (topic[..3] == "smd")
                {
                    var conId = topic[4..];
                    if (!symbolLookup.ContainsKey(conId)) continue;
                    var symbol = symbolLookup[conId];

                    var priceString = socketElement.GetProperty("31").GetString() ?? "";
                    if (!decimal.TryParse(priceString, out var price)) continue;

                    _logger.LogTrace("New position {conId} {symbol} {price}", conId, symbol, price);
                    var position = new TickPosition
                    {
                        Position = price,
                        Symbol = symbol
                    };

                    await tickChannel.Writer.WriteAsync(position, stoppingToken);
                }

                if (topic != "str") continue;

                var args = socketElement.GetProperty("args");
                foreach (var arg in args.EnumerateArray())
                {
                    var conId = arg.GetProperty("conid").GetString() ?? "";
                    if (!symbolLookup.ContainsKey(conId)) continue;

                    if (!decimal.TryParse(arg.GetProperty("price").GetString() ?? "", out var price)) continue;
                    if (!decimal.TryParse(arg.GetProperty("commission").GetString() ?? "", out var commission)) continue;
                    if (!decimal.TryParse(arg.GetProperty("size").GetString() ?? "", out var size)) continue;
                    if (!decimal.TryParse(arg.GetProperty("net_amount").GetString() ?? "", out var netAmount)) continue;
                    if (!int.TryParse(arg.GetProperty("order_ref").GetString() ?? "", out var orderId)) continue;

                    var side = arg.GetProperty("side").GetString() == "B" ? ActionSide.Buy : ActionSide.Sell;
                    netAmount -= commission;

                    _logger.LogTrace("Trade Completed {conId} {side} {price}", conId, side, price);
                    var completedOrder = new CompletedOrder
                    {
                        Commission = commission,
                        Qty = size,
                        Side = side,
                        Symbol = symbolLookup[conId],
                        PricePerUnit = price,
                        OrderId = orderId,
                        CostOfOrder = netAmount
                    };

                    await orderChannel.Writer.WriteAsync(completedOrder, stoppingToken);
                }

            }
        }

        #endregion
    }
}
