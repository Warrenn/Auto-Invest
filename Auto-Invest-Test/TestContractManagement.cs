using System;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using TestStack.BDDfy;
using Xunit;
using Shouldly;
using Moq;
using static System.Math;
using System.Collections.Generic;
using System.Linq;
using TestStack.BDDfy.Configuration;

namespace Auto_Invest_Test
{
    //Test that buy trigger is set lower than offset if borrowing funds
    //Test that emergency stops are correct if borrowing funds

    //Assert short sale
    //Test that emergency sell value is set appropriately if shorting stocks
    //Test that emergency stops are correct if shorting stock

    //Test that emergency stop at max value is removed when trade results in no more shorting
    //Test that emergency stops are removed if trade results in no more shorting

    [Story(
        AsA = "Trader",
        IWant = "To Automate my trades",
        SoThat = "I can have automatic trades")]
    public class TestContractManagement
    {
        const string SYMBOL = "SYMBOL";
        private decimal _funds = 1000;
        private decimal _tradeQty = 10;
        private decimal _initialAmount = 0;
        private uint _bands = 10;
        private decimal _trailing = 1;
        private Contract _contract;
        private readonly Mock<IContractClient> _contractClientMock = new();
        private ContractManager _manager;
        private TrailingBuySellStrategy _strategy;
        private Dictionary<int, StopLimit> _stopLimits;

        public TestContractManagement()
        {
            Configurator.Processors.ConsoleReport.Enable();
            Configurator.Processors.TestRunner.Enable();
        }

        public void the_limit_order_update_should_be_called_more_than_once(ActionSide side, int times)
        {
            var orderId = side == ActionSide.Sell ? _contract.TrailingSellOrderId : _contract.TrailingBuyOrderId;
            _contractClientMock.Verify(_ => _.PlaceStopLimit(It.Is<StopLimit>(l =>
                l.Side == side &&
                l.OrderId == orderId)), Times.AtLeast(times));
        }

        public void the_funds_should_be(decimal funds) => _contract.Funding.ShouldBe(funds);

        public void the_quantity_should_be(decimal amount) => _contract.QuantityOnHand.ShouldBe(amount);

        public void there_should_be_no_sell_limit_value() => _contract.SellOrderLimit.ShouldBe(-1);

        public void there_should_be_no_buy_limit_value() => _contract.BuyOrderLimit.ShouldBe(-1);

        public void the_safety_bands_should_be(ActionSide side, params decimal[] bands)
        {
            _contract.EmergencyOrders.Count().ShouldBe(bands.Length);
            foreach (var band in bands)
            {
                _contract.EmergencyOrders.ShouldContain(_ => _.Action == side && _.PricePerUnit == band);
            }
        }

        public void given_funds_of(decimal funds) { _funds = funds; }

        public void given_trade_qty_of(decimal amount) { _tradeQty = amount; }

        public void given_initial_amount_of(decimal amount) { _initialAmount = amount; }

        public void given_layers_of(uint amount) { _bands = amount; }

        public void given_trailing_of(decimal trailing) { _trailing = trailing; }

        public void the_upper_bound_should_be(decimal upperBound) => _contract.UpperBound.ShouldBe(upperBound);

        public void the_lower_bound_should_be(decimal lowerBound) => _contract.LowerBound.ShouldBe(lowerBound);

        public void the_average_should_be(decimal average) => _contract.AveragePrice.ShouldBe(average);

        public void the_runstate_should_be(RunState runstate) { _contract.RunState.ShouldBe(runstate); }

        public void the_max_stop_limit_should_be_set() =>
            _stopLimits.ShouldContain(_ => _.Value.Side == ActionSide.Sell && _.Value.StopPrice == _contract.MaxSellPrice);

        public void the_trailing_stop_limit_should_be(ActionSide side, decimal stopLimit)
        {
            _stopLimits.ShouldContain(_ => _.Value.Side == side && _.Value.StopPrice == stopLimit);
            if (side == ActionSide.Sell) _contract.TrailingSellOrderId.ShouldBeGreaterThan(0);
            else _contract.TrailingBuyOrderId.ShouldBeGreaterThan(0);
        }

        public void the_max_price_should_be(decimal maxPrice) => _contract.MaxSellPrice.ShouldBe(maxPrice);

        public async Task when_trades_are(params decimal[] trades)
        {
            IOrderCompletion orderCompletion = null;
            var orderId = 1;
            _stopLimits = new Dictionary<int, StopLimit>();

            _contract = new Contract(SYMBOL, _funds, _tradeQty, _trailing, _bands, _initialAmount);
            _contractClientMock
                .Setup(_ => _.ListenForCompletion(SYMBOL, It.IsAny<IOrderCompletion>()))
                .Callback((string s, IOrderCompletion o) => { orderCompletion = o; });
            _contractClientMock
                .Setup(_ => _.PlaceStopLimit(It.IsAny<StopLimit>()))
                .ReturnsAsync((StopLimit l) =>
                {
                    if (l.OrderId < 1) l.OrderId = orderId++;
                    _stopLimits[l.OrderId] = l;
                    return new ContractResult { OrderId = l.OrderId };
                });
            _contractClientMock
                .Setup(_ => _.CancelOrder(It.IsAny<int>()))
                .Callback(async (int id) => await Task.Run(() =>
                {
                    if (_stopLimits.ContainsKey(id))
                        _stopLimits.Remove(id);
                }));

            _manager = new ContractManager(_contractClientMock.Object);
            _manager.RegisterContract(_contract);
            _strategy = new TrailingBuySellStrategy(_manager);

            var previousTrade = trades[0];
            foreach (var trade in trades)
            {
                var min = Min(trade, previousTrade);
                var max = Max(trade, previousTrade);

                foreach (var limit in _stopLimits.Values)
                {
                    if (limit.StopPrice < min || limit.StopPrice > max) continue;
                    var slippage = limit.Side == ActionSide.Sell ? -0.1M : 0.1M;
                    var price = limit.StopPrice + slippage;
                    var orderCost = price * limit.Quantity;
                    var commission = Max(1M, orderCost * 0.01M);

                    await orderCompletion?.OrderCompleted(new CompletedOrder
                    {
                        OrderId = limit.OrderId,
                        Commission = commission,
                        CostOfOrder = orderCost,
                        PricePerUnit = price,
                        Qty = limit.Quantity,
                        Side = limit.Side,
                        Symbol = limit.Symbol
                    });

                    _stopLimits.Remove(limit.OrderId);
                }

                await _strategy.Tick(new TickPosition
                {
                    Position = trade,
                    Symbol = "SYMBOL"
                });

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
        
        [Fact]
        public void trailing_sell_should_be_under_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of $1000")
                .And(_ => _.given_trailing_of(1), "And a trail of $1")
                .When(_ => _.when_trades_are(10, 20), "When the trades go up past trigger points")
                .Then(_ => _.the_upper_bound_should_be(-1), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be(-1), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be $10")
                .And(_ => _.the_runstate_should_be(RunState.SellRun), "The Contract RunState should be SellRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Sell, 19), "The trailing stop sell limit should be the trail value under market price")
                .And(_ => _.the_max_price_should_be(199), "the max price should be the highest price that a short can be afforded")
                .BDDfy("sell should be under market price");
        }

        [Fact]
        public void trailing_buy_should_be_under_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of $1000")
                .And(_ => _.given_trailing_of(1), "And a trail of $1")
                .When(_ => _.when_trades_are(10, 5), "When the trades go down below trigger points")
                .Then(_ => _.the_upper_bound_should_be(-1), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be(-1), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.BuyRun), "The Contract RunState should be BuyRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Buy, 6), "The trailing stop by limit should be the trail value over the market price")
                .And(_ => _.the_max_price_should_be(199), "the max price should be the highest price that a short can be afforded")
                .BDDfy("buy should be under market price");
        }

        [Fact]
        public void trailing_buy_should_move_with_the_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .When(_ => _.when_trades_are(10, 7, 5), "When the trades go down below trigger points")
                .Then(_ => _.the_upper_bound_should_be(-1), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be(-1), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.BuyRun), "The Contract RunState should be BuyRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Buy, 6), "The trailing stop by limit should be the trail value over the market price")
                .And(_ => _.the_limit_order_update_should_be_called_more_than_once(ActionSide.Buy, 2), "The limit order needs to be updated at least {0} times")
                .And(_ => _.the_max_price_should_be(199), "the max price should be the highest price that a short can be afforded")
                .BDDfy("trailing buy should move with the market price");
        }

        [Fact]
        public void trailing_sell_should_move_with_the_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .When(_ => _.when_trades_are(10, 20, 30), "When the trades go above trigger points")
                .Then(_ => _.the_upper_bound_should_be(-1), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be(-1), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.SellRun), "The Contract RunState should be SellRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Sell, 29), "The trailing stop by limit should be the trail under the market price")
                .And(_ => _.the_limit_order_update_should_be_called_more_than_once(ActionSide.Sell, 2), "The limit order needs to be updated at least {0} times")
                .And(_ => _.the_max_price_should_be(199), "the max price should be the highest price that a short can be afforded")
                .BDDfy("trailing sell should move with the market price");
        }

        [Fact]
        public void sell_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(10), "And an initial amount of {0}")
                .And(_ => _.given_trade_qty_of(10))
                .When(_ => _.when_trades_are(10, 20, 30, 29), "When the market values runs up and then suddenly reverses down")
                .Then(_ => _.the_upper_bound_should_be(29.9M), "The Upper Bound should be reset")
                .And(_ => _.the_lower_bound_should_be(27.9M), "The Lower Bound should be reset")
                .And(_ => _.the_average_should_be(28.9M), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_quantity_should_be(0), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be((decimal)(1000 + (28.9 * 10) - ((28.9 * 10) * 0.01))), "The funds should be ${0}")
                .And(_ => _.the_max_price_should_be(256.222M), "the max price should be the highest price that a short can be afforded")
                .BDDfy("a sell run should trigger a sell order on a reversal");
        }

        [Fact]
        public void buy_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_trade_qty_of(10))
                .When(_ => _.when_trades_are(30, 20, 10, 11), "When the market values runs down and then suddenly reverses up")
                .Then(_ => _.the_upper_bound_should_be(12.1M), "The Upper Bound should be reset")
                .And(_ => _.the_lower_bound_should_be(10.1M), "The Lower Bound should be reset")
                .And(_ => _.the_average_should_be(11.1M), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_quantity_should_be(10), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be((decimal)(1000 - (11.1 * 10) - ((11.1 * 10) * 0.01))), "The funds should be ${0}")
                .And(_ => _.the_max_price_should_be(-1), "the max price should be set as there is enough stock on hand")
                .BDDfy("a buy run should trigger a buy order on a reversal");
        }

        [Fact]
        public void borrow_when_funds_are_insufficient()
        {
            this
                .Given(_ => _.given_funds_of(10), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_trade_qty_of(10))
                .And(_ => _.given_initial_amount_of(4))
                .When(_ => _.when_trades_are(30, 25, 20, 22), "When the market values runs down and then suddenly reverses up")
                .And(_ => _.the_quantity_should_be(20), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be((decimal)(10 - (22.1 * 10) - ((22.1 * 10) * 0.01))), "The funds should be ${0}")
                .And(_ => _.the_lower_bound_should_be(0), "The Lower Bound should be set to value below market and trail")
                .And(_ => _.the_max_price_should_be(-1), "the max price should not be set as we have enough stocks on hand for a sell")
                .And(_ => _.the_safety_bands_should_be(ActionSide.Buy, 10, 11, 12, 13), "the safety bands should be set as stop orders")
                .BDDfy("if there not enough funds but enough purchase power borrow funds to buy");
        }

        [Fact]
        //todo: finish this
        public void do_not_borrow_when_purchase_power_is_insufficient()
        {
            throw new NotImplementedException();
        }

        [Fact]
        //todo: finish this
        public void short_stock_when_there_is_not_enough_stock_on_hand()
        {
            throw new NotImplementedException();
        }

        [Fact]
        //todo: finish this
        public void do_not_short_stock_when_there_is_not_enough_purchase_power()
        {
            throw new NotImplementedException();
        }

        [Fact]
        //todo: finish this
        public void set_the_max_stop_limit_when_market_price_is_too_high()
        {
            throw new NotImplementedException();
        }

        [Fact]
        //todo: finish this
        public void recover_from_trading_on_margin()
        {
            throw new NotImplementedException();
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