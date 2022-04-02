using System;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using TestStack.BDDfy;
using Xunit;
using Shouldly;
using Moq;
using static System.Math;
using System.Collections.Generic;

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
    //trigger a buy run
    //move a stop for buy
    //move a stop for sell
    //fill a contract
    // - the avereage is correct
    // //trigger a sell run
    //Assert short sale

    [Story(
        AsA = "Trader",
        IWant = "To Automate my trades",
        SoThat = "I can have automatic trades")]
    public class TestContractManagement
    {
        const string SYMBOL = "SYMBOL";
        private decimal _funds = 1000;
        private decimal _amount = 10;
        private decimal _trailing = 1;
        private Contract _contract;
        private Mock<IContractClient> _contractClientMock = new();
        private ContractManager _manager;
        private TrailingBuySellStrategy _strategy;

        public void given_funds_of(decimal funds) { _funds = funds; }
        public void given_amount_of(decimal amount) { _amount = amount; }
        public void given_trailing_of(decimal trailing) { _trailing = trailing; }
        public void the_upper_bound_should_be(decimal upperBound) => _contract.UpperBound.ShouldBe(upperBound);
        public void the_lower_bound_should_be(decimal lowerBound) => _contract.LowerBound.ShouldBe(lowerBound);
        public void the_average_should_be(decimal average) => _contract.AveragePrice.ShouldBe(average);
        public void the_runstate_should_be(RunState runstate) { _contract.RunState.ShouldBe(runstate); }

        public async Task when_trades_are(params decimal[] trades)
        {
            IOrderCompletion orderCompletion = null;
            var orderId = 1;
            var stopLimits = new Dictionary<int, StopLimit>();

            _contract = new Contract(SYMBOL, _funds, _amount, _trailing);
            _contractClientMock
                .Setup(_ => _.ListenForCompletion(SYMBOL, It.IsAny<IOrderCompletion>()))
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

                foreach (var limit in stopLimits.Values)
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
            _contract = await _manager.GetContractState(SYMBOL);
        }

        [Fact]
        public void upper_and_lower_bounds_need_to_be_correct()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of $1000")
                .And(_ => _.given_trailing_of(1), "And a trail of $1")
                .When(_ => _.when_trades_are(10), "When the trades are $10 per unit")
                .Then(_ => _.the_upper_bound_should_be(11), "The Upper Bound should be $11")
                .And(_ => _.the_lower_bound_should_be(9), "The Lower Bound should be $9")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be $10")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .BDDfy();
        }

        //public void GetTheCorrectMaxSellingPrice()
        //{
        //    var offset = 1M;
        //    TrailingBuySellStrategy.SafetyPrice(75000, -1000, Contract.MaintenanceMargin, offset);

        //    var mc = TrailingBuySellStrategy.MarginCallPrice(50M, Contract.InitialMargin, Contract.MaintenanceMargin);
        //}

        // [Test]
        // public async Task Test1()
        // {
        //     var Symbol = "NDAQ";
        //     var funding = 10000;
        //     var contract = new Contract(
        //         Symbol,
        //         funding,
        //         0.1M,
        //         0.0001M,
        //         marginRisk: 10.0M);

        //     var contractManager = new ContractManager(null);
        //     contractManager.RegisterContract(contract);

        //     var strategy = new TrailingBuySellStrategy(contractManager);

        //     var random = new Random((int)DateTime.UtcNow.Ticks);
        //     var checkC = await contractManager.GetContractState(Symbol);
        //     Trace.WriteLine($"start funding:{checkC.Funding:C} ");
        //     var start = new DateTime(2017, 1, 1);
        //     var end = start;

        //     while (true)
        //     {
        //         if (start >= new DateTime(2022, 1, 1)) break;

        //         end = start.AddMonths(1).Subtract(TimeSpan.FromDays(1));

        //         var hist = await Yahoo.GetHistoricalAsync(Symbol, start, end);

        //         foreach (var candle in hist)
        //         {
        //             await processTick(candle.Open);
        //             if (random.Next(0, 2) == 0)
        //             {
        //                 await processTick(candle.Low);
        //                 await processTick(candle.High);
        //             }
        //             else
        //             {
        //                 await processTick(candle.High);
        //                 await processTick(candle.Low);
        //             }

        //             await processTick(candle.Close);
        //         }

        //         start = start.AddMonths(1);
        //     }

        //     checkC = await contractManager.GetContractState(Symbol);
        //     var netp = (((checkC.Funding + (checkC.Quantity * checkC.AveragePrice)) - funding) / funding);
        //     var years = 5;

        //     Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.Quantity:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + (checkC.Quantity * checkC.AveragePrice):C}");
        //     Trace.WriteLine($"total % :{((checkC.Funding - funding) / funding):P} net with assets % :{netp:P} average for {years} years {netp / years:P}");
        //     Trace.WriteLine("DONE");

        //     async Task processTick(decimal tick)
        //     {
        //         var contract = await contractManager.GetContractState(Symbol);

        //         var position = new TickPosition
        //         {
        //             Symbol = Symbol,
        //             Position = tick
        //         };

        //         if (contract.AveragePrice == 0)
        //         {
        //             contractManager.InitializeContract(position);
        //         }


        //         if (contract.RunState == RunState.BuyRun &&
        //             (tick >= contract.BuyOrderLimit))
        //         {
        //             await contractManager.TrailingBuyComplete(new ActionDetails
        //             {
        //                 Symbol = Symbol,
        //                 PricePerUnit = contract.BuyOrderLimit,
        //                 CostOfOrder = contract.TradeQty * tick,
        //                 Qty = contract.TradeQty
        //             });
        //             return;
        //         }

        //         if (contract.RunState == RunState.SellRun &&
        //             (tick <= contract.SellOrderLimit))
        //         {
        //             await contractManager.TrailingSellComplete(new ActionDetails
        //             {
        //                 Symbol = Symbol,
        //                 PricePerUnit = contract.SellOrderLimit,
        //                 CostOfOrder = contract.TradeQty * tick,
        //                 Qty = contract.TradeQty
        //             });
        //             return;
        //         }

        //         await strategy.Tick(position);
        //     }
        // }
    }

}