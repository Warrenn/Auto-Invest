using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using CoreBot;
using IBApi;
using Contract = IBApi.Contract;

namespace Auto_Invest
{
    public class IBKRClientContract : IContractClient
    {
        private readonly IBClient _client;
        private readonly IDictionary<int, OrderProgress> _orders = new Dictionary<int, OrderProgress>();
        private readonly IDictionary<string, double> _commission = new Dictionary<string, double>();
        private readonly IDictionary<string, decimal> _price = new Dictionary<string, decimal>();

        public IBKRClientContract(IBClient client)
        {
            _client = client;
        }

        #region Implementation of IContractClient

        public async Task CancelOrder(int orderId)
        {
            if (!_orders.ContainsKey(orderId)) return;

            _client.ClientSocket.cancelOrder(orderId);
        }

        private static Contract CreateContract(string symbol) => new()
        {
            Symbol = symbol,
            SecType = SecTypes.STOCK,
            Currency = Currency.USD,
            Exchange = Exchanges.SMART,
            PrimaryExch = Exchanges.ISLAND
        };

        public async Task<ContractResult> PlaceStopLimit(StopLimit stopLimit)
        {
            var contract = CreateContract(stopLimit.Symbol);
            var orderId = (stopLimit.OrderId <= 0) ? _client.GetNextOrderId() : stopLimit.OrderId;
            var side = (stopLimit.Side == ActionSide.Sell) ? ActionTypes.SELL : ActionTypes.BUY;

            var order = new Order
            {
                OrderId = orderId,
                Action = side,
                OrderType = OrderTypes.STOP,
                TotalQuantity = (double)stopLimit.Quantity,
                AuxPrice = (double)stopLimit.StopPrice
            };
            _client.ClientSocket.placeOrder(orderId, contract, order);

            if (!_orders.ContainsKey(orderId)) _orders[orderId] = new OrderProgress
            {
                Id = orderId,
                Symbol = stopLimit.Symbol,
                Progress = ProgressStatus.Placed,
                Side = stopLimit.Side
            };

            var result = new ContractResult
            {
                OrderId = orderId
            };

            return result;
        }

        public void ListenForCompletion(string symbol, IOrderCompletion orderCompletion)
        {
            _client.ExecDetailsEvent += details =>
            {
                if (details.Contract.Symbol != symbol) return;
                if (!_orders.ContainsKey(details.Execution.OrderId)) return;

                var progress = _orders[details.Execution.OrderId];
                progress.CumQty = details.Execution.CumQty;
                progress.AvgPrice = details.Execution.AvgPrice;
                progress.ExecId = details.Execution.ExecId;
                progress.Progress |= ProgressStatus.Execution;

                if (_commission.ContainsKey(details.Execution.ExecId))
                {
                    progress.Commission = _commission[details.Execution.ExecId];
                    progress.Progress |= ProgressStatus.Commision;
                }

                FireIfComplete(progress);
            };

            _client.CommissionReportEvent += report =>
            {
                if (_orders.Values.Any(_ => _.ExecId == report.ExecId))
                {
                    var progress = _orders.Values.First(_ => _.ExecId == report.ExecId);
                    progress.Commission = report.Commission;
                    progress.Progress |= ProgressStatus.Commision;

                    FireIfComplete(progress);
                    return;
                }

                _commission[report.ExecId] = report.Commission;
            };

            void FireIfComplete(OrderProgress progress)
            {
                if (progress.Progress != (ProgressStatus.Placed | ProgressStatus.Commision | ProgressStatus.Execution)) return;
                orderCompletion.OrderCompleted(new CompletedOrder
                {
                    Commission = (decimal)progress.Commission,
                    CostOfOrder = (decimal)(progress.AvgPrice * progress.CumQty),
                    OrderId = progress.Id,
                    PricePerUnit = (decimal)progress.AvgPrice,
                    Qty = (decimal)progress.CumQty,
                    Side = progress.Side,
                    Symbol = progress.Symbol
                });
            }
        }

        public async Task<decimal> GetMarketPrice(string symbol)
        {
            if (_price.ContainsKey(symbol)) return _price[symbol];
            return 0;
        }

        public void SetMarketPrice(string symbol, decimal price)
        {
            _price[symbol] = price;
        }

        #endregion
    }
}
