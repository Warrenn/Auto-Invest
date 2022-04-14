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
    public class TestContractManagement
    {
        private const string SYMBOL = "SPGI";
        private decimal _funds = 1000;
        private decimal _tradeQty = 10;
        private decimal _initialAmount;
        private uint _safetyBands = 10;
        private decimal _trailing = 1;
        private decimal _marginProtection = 0.01M;
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

        public void the_funds_should_be(decimal funds) => Round(_contract.Funding, 2).ShouldBe(funds);

        public void the_quantity_should_be(decimal amount) => Round(_contract.QuantityOnHand, 2).ShouldBe(amount);

        public void there_should_be_no_sell_limit_value() => _contract.SellOrderLimit.ShouldBe(-1);

        public void there_should_be_no_buy_limit_value() => _contract.BuyOrderLimit.ShouldBe(-1);

        public void the_safety_bands_should_be(ActionSide side, params decimal[] bands)
        {
            _contract.EmergencyOrders.Count().ShouldBe(bands.Length);

            _contract.EmergencyOrders.ShouldAllBe(_ => _.Action == side);
            var prices = _contract.EmergencyOrders.Select(_ => Math.Round(_.PricePerUnit, 2)).ToArray();

            foreach (var band in bands)
            {
                prices.ShouldContain(band);
            }
        }

        public void given_funds_of(decimal funds) { _funds = funds; }

        public void given_trade_qty_of(decimal amount) { _tradeQty = amount; }

        public void given_initial_amount_of(decimal amount) { _initialAmount = amount; }

        public void given_trailing_of(decimal trailing) { _trailing = trailing; }

        public void the_stock_on_hand_should_be_more_than(decimal value) => _contract.QuantityOnHand.ShouldBeGreaterThan(value);

        public void the_stock_on_hand_should_be_less_than(decimal value) => _contract.QuantityOnHand.ShouldBeLessThan(value);

        public void the_funding_should_be_less_than(decimal funding) => _contract.Funding.ShouldBeLessThan(funding);

        public void the_average_should_be(decimal value) => Round(_contract.AveragePrice, 2).ShouldBe(value);

        public void the_funding_should_be_more_than(decimal funding) => _contract.Funding.ShouldBeGreaterThan(funding);

        public void the_upper_bound_should_be_more_than(decimal value) =>
            _contract.UpperBound.ShouldBeGreaterThan(value);

        public void the_lower_bound_should_be_less_than(decimal value) =>
            _contract.LowerBound.ShouldBeLessThan(value);

        public void the_upper_bound_should_be_less_than(decimal value) => _contract.UpperBound.ShouldBeLessThan(value);

        public void the_runstate_should_be(RunState runState) => _contract.RunState.ShouldBe(runState);

        public void the_trailing_stop_limit_should_be(ActionSide side, decimal stopLimit)
        {
            _stopLimits.Count.ShouldBe(1);
            var limit = _stopLimits.First().Value;
            limit.Side.ShouldBe(side);
            if (side == ActionSide.Sell) limit.StopPrice.ShouldBeLessThan(stopLimit);
            else limit.StopPrice.ShouldBeGreaterThan(stopLimit);
            if (side == ActionSide.Sell) _contract.TrailingSellOrderId.ShouldBeGreaterThan(0);
            else _contract.TrailingBuyOrderId.ShouldBeGreaterThan(0);
        }

        public async Task when_trades_are(params decimal[] trades) => await simulate_trades(trades);

        public async Task simulate_trades(IEnumerable<decimal> trades)
        {
            IOrderCompletion orderCompletion = null;
            var orderId = 1;
            _stopLimits = new Dictionary<int, StopLimit>();

            _contract = new Contract(SYMBOL, _funds, _trailing, _tradeQty, _initialAmount, safetyBands: _safetyBands, marginProtection: _marginProtection);
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

            TrailingBuySellStrategy.MovingAverageSize = 1;
            _manager = new ContractManager(_contractClientMock.Object);
            _manager.RegisterContract(_contract);
            _strategy = new TrailingBuySellStrategy(_manager);

            var previousTrade = -1M;
            foreach (var trade in trades)
            {
                if (previousTrade == -1) previousTrade = trade;

                var min = Min(trade, previousTrade);
                var max = Max(trade, previousTrade);
                var limits = _stopLimits.Values.ToArray();

                foreach (var limit in limits)
                {
                    if (limit == null) continue;
                    if (limit.StopPrice < min || limit.StopPrice > max) continue;
                    var slippage = limit.Side == ActionSide.Sell ? -0.1M : 0.1M;
                    var price = limit.StopPrice + slippage;
                    var orderCost = price * limit.Quantity;
                    var commission = Max(1M, limit.Quantity * 0.02M);

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
                    Symbol = SYMBOL
                });

                previousTrade = trade;
            }
            _contract = await _manager.GetContractState(SYMBOL);
        }

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

        public IEnumerable<decimal> PolygonValues(string symbol, DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, "Data\\Polygon-SPGI");
            var random = new Random((int)DateTime.UtcNow.Ticks);

            var dateIndex = start;
            while (true)
            {
                if (dateIndex >= endDate) break;

                var jsonFileName = FileName(dateIndex);
                dateIndex = dateIndex.AddDays(1);

                if (!File.Exists(jsonFileName)) continue;

                var content = File.ReadAllText(jsonFileName);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var jsonData = DeserializeObject<TickFileData>(content);
                if (jsonData?.results == null || !jsonData.results.Any()) continue;

                foreach (var candle in jsonData.results)
                {
                    yield return candle.o;

                    if (random.Next(0, 2) == 0)
                    {
                        yield return candle.l;
                        yield return candle.h;
                    }
                    else
                    {
                        yield return candle.h;
                        yield return candle.l;
                    }

                    yield return candle.c;
                }

            }

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{symbol.ToUpper()}-{fileDate:yyyy-MM-dd}.json");
        }

        public IEnumerable<decimal> TickValues(string symbol, DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, "Data\\Tick-SPGI");
            var random = new Random((int)DateTime.UtcNow.Ticks);

            var dateIndex = start;
            while (true)
            {
                if (dateIndex > endDate) break;

                var fileName = FileName(dateIndex);
                dateIndex = dateIndex.AddMonths(1);

                if (!File.Exists(fileName)) continue;

                using var stream = File.OpenRead(fileName);
                var reader = new StreamReader(stream);
                reader.ReadLine();
                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(',');
                    if (parts.Length != 7)
                    {
                        line = reader.ReadLine();
                        continue;
                    }

                    yield return decimal.Parse(parts[2]);

                    if (random.Next(0, 2) == 0)
                    {
                        yield return decimal.Parse(parts[4]);
                        yield return decimal.Parse(parts[3]);
                    }
                    else
                    {
                        yield return decimal.Parse(parts[3]);
                        yield return decimal.Parse(parts[4]);
                    }

                    yield return decimal.Parse(parts[5]);

                    line = reader.ReadLine();
                }
            }

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{symbol.ToUpper()}-{fileDate:yyyy-MM}.csv");
        }

        // [Fact]
        public async Task back_testing_SPGI_polygon()
        {
            _trailing = 0.1M;
            _marginProtection = 1M;
            _funds = 1000M;

            Trace.WriteLine($"start funding:{_funds:C} ");

            var enumTicks = PolygonValues("SPGI", new DateTime(2021, 2, 1), new DateTime(2022, 4, 10));
            await simulate_trades(enumTicks);

            var checkC = await _manager.GetContractState(SYMBOL);
            var netp = (checkC.Funding + (checkC.QuantityOnHand * checkC.AveragePrice) - _funds) / _funds;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.QuantityOnHand:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice:C}");
            Trace.WriteLine($"total % :{(checkC.Funding - _funds) / _funds:P} net with assets % :{netp:P} ");
            Trace.WriteLine("DONE");
        }

        //[Fact]
        public async Task back_testing_SPGI_tick()
        {
            _trailing = 0.1M;
            _marginProtection = 1M;
            _funds = 1000M;

            Trace.WriteLine($"start funding:{_funds:C} ");

            var enumTicks = TickValues("SPGI", new DateTime(2019, 12, 1), new DateTime(2022, 3, 1));
            await simulate_trades(enumTicks);

            var checkC = await _manager.GetContractState(SYMBOL);
            var netp = (checkC.Funding + (checkC.QuantityOnHand * checkC.AveragePrice) - _funds) / _funds;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.QuantityOnHand:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice:C}");
            Trace.WriteLine($"total % :{(checkC.Funding - _funds) / _funds:P} net with assets % :{netp:P} ");
            Trace.WriteLine("DONE");
        }
    }

}