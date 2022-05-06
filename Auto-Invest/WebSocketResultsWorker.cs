using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class WebSocketResultsWorker : BackgroundService
    {
        private readonly IMediator _mediator;

        public WebSocketResultsWorker(IMediator mediator)
        {
            _mediator = mediator;
        }

        #region Overrides of BackgroundService

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var contractIds = await _mediator.GetContractsAsync();
            var socketResultsReader = await _mediator.GetWebSocketResultsReaderAsync();

            var symbolLookup = contractIds
                .Where(_ => !string.IsNullOrWhiteSpace(_.ConId))
                .ToDictionary(x => x.ConId ?? "", x => x.Symbol);

            var orderChannel = Channel.CreateUnbounded<CompletedOrder>();
            var tickChannel = Channel.CreateUnbounded<TickPosition>();

            _mediator.RegisterCompletedOrderReader(orderChannel.Reader);
            _mediator.RegisterTickPositionReader(tickChannel.Reader);

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
