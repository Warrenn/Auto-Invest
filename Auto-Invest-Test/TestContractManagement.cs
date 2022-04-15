using System;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using TestStack.BDDfy;
using Xunit;
using Shouldly;
using Moq;
using static System.Math;
using static Newtonsoft.Json.JsonConvert;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TestStack.BDDfy.Configuration;

namespace Auto_Invest_Test
{
    [Story(
        AsA = "Trader",
        IWant = "To Automate my trades",
        SoThat = "I can have automatic trades")]
    public class TestContractManagement : TestContractManagementBase
    {
        public TestContractManagement()
        {
            Configurator.Processors.ConsoleReport.Enable();
            Configurator.Processors.TestRunner.Enable();
        }

        public void the_limit_order_update_should_be_called_more_than_once(ActionSide side, int times)
        {
            var orderId = side == ActionSide.Sell ? Contract.TrailingSellOrderId : Contract.TrailingBuyOrderId;
            ContractClientMock.Verify(_ => _.PlaceStopLimit(It.Is<StopLimit>(l =>
                l.Side == side &&
                l.OrderId == orderId)), Times.AtLeast(times));
        }

        public void the_funds_should_be(decimal funds) => Round(Contract.Funding, 2).ShouldBe(funds);

        public void the_quantity_should_be(decimal amount) => Round(Contract.QuantityOnHand, 2).ShouldBe(amount);

        public void there_should_be_no_sell_limit_value() => Contract.SellOrderLimit.ShouldBe(-1);

        public void there_should_be_no_buy_limit_value() => Contract.BuyOrderLimit.ShouldBe(-1);

        public void the_safety_bands_should_be(ActionSide side, params decimal[] bands)
        {
            Contract.EmergencyOrders.Count().ShouldBe(bands.Length);

            Contract.EmergencyOrders.ShouldAllBe(_ => _.Action == side);
            var prices = Contract.EmergencyOrders.Select(_ => Math.Round(_.PricePerUnit, 2)).ToArray();

            foreach (var band in bands)
            {
                prices.ShouldContain(band);
            }
        }

        public void given_funds_of(decimal funds) { Funds = funds; }

        public void given_trade_qty_of(decimal amount) { TradePercentage = amount; }

        public void given_initial_amount_of(decimal amount) { InitialSize = amount; }

        public void given_trailing_of(decimal trailing) { TrailingOffset = trailing; }

        public void the_stock_on_hand_should_be_more_than(decimal value) => Contract.QuantityOnHand.ShouldBeGreaterThan(value);

        public void the_stock_on_hand_should_be_less_than(decimal value) => Contract.QuantityOnHand.ShouldBeLessThan(value);

        public void the_funding_should_be_less_than(decimal funding) => Contract.Funding.ShouldBeLessThan(funding);

        public void the_average_should_be(decimal value) => Round(Contract.AveragePrice, 2).ShouldBe(value);

        public void the_funding_should_be_more_than(decimal funding) => Contract.Funding.ShouldBeGreaterThan(funding);

        public void the_upper_bound_should_be_more_than(decimal value) =>
            Contract.UpperBound.ShouldBeGreaterThan(value);

        public void the_lower_bound_should_be_less_than(decimal value) =>
            Contract.LowerBound.ShouldBeLessThan(value);

        public void the_upper_bound_should_be_less_than(decimal value) => Contract.UpperBound.ShouldBeLessThan(value);

        public void the_runstate_should_be(RunState runState) => Contract.RunState.ShouldBe(runState);

        public void the_trailing_stop_limit_should_be(ActionSide side, decimal stopLimit)
        {
            StopLimits.Count.ShouldBe(1);
            var limit = StopLimits.First().Value;
            limit.Side.ShouldBe(side);
            if (side == ActionSide.Sell) limit.StopPrice.ShouldBeLessThan(stopLimit);
            else limit.StopPrice.ShouldBeGreaterThan(stopLimit);
            if (side == ActionSide.Sell) Contract.TrailingSellOrderId.ShouldBeGreaterThan(0);
            else Contract.TrailingBuyOrderId.ShouldBeGreaterThan(0);
        }

        public async Task when_trades_are(params decimal[] trades) => await simulate_trades(trades);

        [Fact]
        public void upper_and_lower_bounds_need_to_be_correct()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of {0:C}")
                .And(_ => _.given_trailing_of(0.1M), "And a trail of {0:C}")
                .When(_ => _.when_trades_are(10), "When the trades are {0:C} per unit")
                .Then(_ => _.the_upper_bound_should_be_more_than(10))
                .And(_ => _.the_lower_bound_should_be_less_than(10))
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be $10")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .BDDfy();
        }

        [Fact]
        public void trailing_sell_should_be_under_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of $1000")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of $1")
                .When(_ => _.when_trades_are(10, 20), "When the trades go up past trigger points")
                .Then(_ => _.the_upper_bound_should_be_less_than(0), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be_less_than(0), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be $10")
                .And(_ => _.the_runstate_should_be(RunState.SellRun), "The Contract RunState should be SellRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Sell, 20), "The trailing stop sell limit should be the trail value under market price")
                .BDDfy("sell should be under market price");
        }

        [Fact]
        public void trailing_buy_should_be_under_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of $1000")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of $1")
                .When(_ => _.when_trades_are(10, 5), "When the trades go down below trigger points")
                .Then(_ => _.the_upper_bound_should_be_less_than(0), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be_less_than(0), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.BuyRun), "The Contract RunState should be BuyRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Buy, 5), "The trailing stop by limit should be the trail value over the market price")
                .BDDfy("buy should be under market price");
        }

        [Fact]
        public void trailing_buy_should_move_with_the_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0}")
                .When(_ => _.when_trades_are(10, 7, 5), "When the trades go down below trigger points")
                .And(_ => _.the_runstate_should_be(RunState.BuyRun), "The Contract RunState should be BuyRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Buy, 5), "The trailing stop by limit should be the trail value over the market price")
                .And(_ => _.the_limit_order_update_should_be_called_more_than_once(ActionSide.Buy, 2), "The limit order needs to be updated at least {0} times")
                .BDDfy("trailing buy should move with the market price");
        }

        [Fact]
        public void trailing_sell_should_move_with_the_market_price()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0:C}")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0:C}")
                .When(_ => _.when_trades_are(10, 20, 30), "When the trades go above trigger points")
                .Then(_ => _.the_upper_bound_should_be_less_than(0), "The Upper Bound should be removed")
                .And(_ => _.the_lower_bound_should_be_less_than(0), "The Lower Bound should be removed")
                .And(_ => _.the_average_should_be(10), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.SellRun), "The Contract RunState should be SellRun")
                .And(_ => _.the_trailing_stop_limit_should_be(ActionSide.Sell, 30), "The trailing stop by limit should be the trail under the market price")
                .And(_ => _.the_limit_order_update_should_be_called_more_than_once(ActionSide.Sell, 2), "The limit order needs to be updated at least {0} times")
                .BDDfy("trailing sell should move with the market price");
        }

        [Fact]
        public void sell_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(0), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(10), "And an initial amount of {0}")
                .And(_ => _.given_trade_qty_of(1M))
                .When(_ => _.when_trades_are(10, 20, 30, 29), "When the market values runs up and then suddenly reverses down")
                .And(_ => _.the_stock_on_hand_should_be_less_than(1))
                .And(_ => _.the_funding_should_be_more_than(0))
                .BDDfy("a sell run should trigger a sell order on a reversal");
        }

        [Fact]
        public void buy_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0}")
                .And(_ => _.given_trade_qty_of(0.02M))
                .When(_ => _.when_trades_are(30, 20, 9, 11), "When the market values runs down and then suddenly reverses up")
                .And(_ => _.the_stock_on_hand_should_be_more_than(0))
                .And(_ => _.the_funding_should_be_less_than(1000), "The funds should be less ${0}")
                .BDDfy("a buy run should trigger a buy order on a reversal");
        }

        [Fact]
        public void borrow_when_funds_are_insufficient()
        {
            this
                .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(0.1M), "And a trail of ${0}")
                .And(_ => _.given_trade_qty_of(1))
                .And(_ => _.given_initial_amount_of(1))
                .When(_ => _.when_trades_are(30, 25, 19, 22),
                    "When the market values runs down and then suddenly reverses up")
                .Then(_ => _.the_quantity_should_be(11.57m), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be(-122.96M), "The funds should be ${0}")
                .And(_ => _.the_safety_bands_should_be(ActionSide.Sell, 15.19m, 14.47m, 13.69m, 12.85m, 11.93m, 10.91m, 9.74m, 8.34m, 6.55m, 3.73m), "the safety bands should be set as stop orders")
                .BDDfy("if there not enough funds but enough purchase power borrow funds to buy");
        }


        [Fact]
        public void do_not_borrow_when_purchase_power_is_insufficient()
        {
            this
                .Given(_ => _.given_funds_of(-1000), "Given funds of ${0}")
                .And(_ => _.given_trade_qty_of(1))
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(1))
                .When(_ => _.when_trades_are(30, 25, 27, 22),
                    "When the market values runs down and then suddenly reverses up")
                .Then(_ => _.the_quantity_should_be(1), "The quantity should be the same {0}")
                .And(_ => _.the_funds_should_be(-1000), "The funds should be the same ${0}")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "the contract state should still be in a trigger run")
                .BDDfy("if there not enough funds and not enough purchase power don't do the trade");
        }


        [Fact]
        public void short_stock_when_there_is_not_enough_stock_on_hand()
        {
            this
                .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
                .And(_ => _.given_trade_qty_of(10))
                .And(_ => _.given_trailing_of(0.01M), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(5))
                .When(_ => _.when_trades_are(15, 18, 21, 18),
                    "When the market values go up and suddenly reverse")
                .Then(_ => _.the_stock_on_hand_should_be_less_than(0), "The quantity should be {0}")
                .And(_ => _.the_funding_should_be_more_than(100), "The funds should be ${0}")
                .And(_ => _.the_safety_bands_should_be(ActionSide.Buy, 24.17m, 24.97m, 25.91m, 27.02m, 28.38m, 30.08m, 32.34m, 35.58m, 40.92m, 53.20m), "the safety bands should be set as stop orders")
                .BDDfy("if there not enough stock on hand sort the stock");
        }

        [Fact]
        public void do_not_short_stock_when_there_is_not_enough_purchase_power()
        {
            this
                .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
                .And(_ => _.given_trade_qty_of(10))
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(-100))
                .When(_ => _.when_trades_are(15, 18, 32, 30),
                    "When the market values go up and suddenly reverse")
                .Then(_ => _.the_quantity_should_be(-100), "The quantity should be {0}")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun),
                    "the contract state should still be in a trigger run")
                .And(_ => _.the_funds_should_be(100M), "The funds should be ${0}")
                .BDDfy("do not short stock if there is not enough purchase power");
        }

        // [Fact]

        //[Fact]
    }

}