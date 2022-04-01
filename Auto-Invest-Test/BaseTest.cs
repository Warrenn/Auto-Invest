using System.Collections.Generic;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using Moq;
using TestStack.BDDfy;
using static System.Math;

namespace Auto_Invest_Test
{
    //Test Triggers are set appropriately
    //Test that buy run and sell run are set appropriately
    //Test that buy trigger is set lower than offset if borrowing funds
    //Test that emergency sell value is set appropriately if shorting stocks
    //Test that emergency stops are correct if borrowing funds
    //Test that emergency stops are correct if shorting stock
    //Test that emergency stop at max value is removed when trade results in no more shorting
    //Test that emergency stops are removed if trade results in no more shorting

    [Story(
        AsA = "Trader",
        IWant = "To Automate my trades",
        SoThat = "I can have automatic trades")]
    public class BaseTest
    {
        protected decimal _funds = 1000;
        protected decimal _amount = 10;
        protected decimal _trailing = 1;
        protected Contract _contract;
        protected Mock<IContractClient> _contractClientMock = new();
        protected ContractManager _manager;
        protected TrailingBuySellStrategy _strategy;

        public async Task WhenTradesAre(params decimal[] trades)
        {
            IOrderCompletion orderCompletion = null;
            var orderId = 1;
            var stopLimits = new Dictionary<int, StopLimit>();

            _contract = new Contract("SYMBOL", _funds, _amount, _trailing);
            _contractClientMock
                .Setup(_ => _.ListenForCompletion("SYMBOL", It.IsAny<IOrderCompletion>()))
                .Callback((string s, IOrderCompletion o) => { orderCompletion = o; });
            _contractClientMock
                .Setup(_ => _.PlaceStopLimit(It.IsAny<StopLimit>()))
                .Callback(async (StopLimit l) => await Task.Run(() =>
                {
                    if (l.OrderId < 1) l.OrderId = orderId++;
                    stopLimits[l.OrderId] = l;
                    return new ContractResult { OrderId = l.OrderId };
                }));
            _contractClientMock
                .Setup(_ => _.CancelOrder(It.IsAny<int>()))
                .Callback(async (int orderId) => await Task.Run(() =>
                 {
                     if (stopLimits.ContainsKey(orderId))
                         stopLimits.Remove(orderId);
                 }));

            _manager = new ContractManager(_contractClientMock.Object);
            _manager.RegisterContract(_contract);
            _strategy = new TrailingBuySellStrategy(_manager);

            var previousTrade = trades[0];
            foreach (var trade in trades)
            {
                await _strategy.Tick(new TickPosition
                {
                    Position = trade,
                    Symbol = "SYMBOL"
                });

                var min = Min(trade, previousTrade);
                var max = Max(trade, previousTrade);

                foreach(var limit in stopLimits.Values)
                {
                    if (limit.StopPrice < min || limit.StopPrice > max) continue;
                    var slippage = limit.Side == ActionSide.Sell ? 0.1M : -0.1M;
                    var price = limit.StopPrice + slippage;
                    var orderCost = price * limit.Quantity;
                    var commision = Max(1M, orderCost * 0.01M);

                    orderCompletion?.OrderCompleted(new CompletedOrder
                    {
                        OrderId = limit.OrderId,
                        Commission = commision,
                        CostOfOrder = orderCost,
                        PricePerUnit = price,
                        Qty = limit.Quantity,
                        Side = limit.Side,
                        Symbol = limit.Symbol
                    });
                    
                    stopLimits.Remove(limit.OrderId);

                }

                previousTrade = trade;
            }
        }
    }

}