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
        private decimal _profitPercentage = 0M;
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

        public void the_limit_order_should_not_be_called() =>
            _contractClientMock.Verify(_ => _.PlaceStopLimit(It.IsAny<StopLimit>()), Times.Never);

        public void the_funds_should_be(decimal funds) => _contract.Funding.ShouldBe(funds);

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

        public void the_upper_bound_should_be(decimal upperBound) => _contract.UpperBound.ShouldBe(upperBound);

        public void the_lower_bound_should_be(decimal lowerBound) => _contract.LowerBound.ShouldBe(lowerBound);

        public void the_average_should_be(decimal average) => _contract.AveragePrice.ShouldBe(average);

        public void the_runstate_should_be(RunState runstate) { _contract.RunState.ShouldBe(runstate); }

        public void there_should_be_no_safety_bands() => _stopLimits.ShouldBeEmpty();

        public void the_sell_limit_should_be(decimal sellLimit) => _contract.SellOrderLimit.ShouldBe(sellLimit);

        public void the_trailing_stop_limit_should_be(ActionSide side, decimal stopLimit)
        {
            _stopLimits.ShouldContain(_ => _.Value.Side == side && _.Value.StopPrice == stopLimit);
            if (side == ActionSide.Sell) _contract.TrailingSellOrderId.ShouldBeGreaterThan(0);
            else _contract.TrailingBuyOrderId.ShouldBeGreaterThan(0);
        }

        public void given_safety_bands_of(uint safetyBands) => _safetyBands = safetyBands;

        public async Task when_trades_are(params decimal[] trades) => await simulate_trades(trades);

        public async Task simulate_trades(IEnumerable<decimal> trades)
        {
            IOrderCompletion orderCompletion = null;
            var orderId = 1;
            _stopLimits = new Dictionary<int, StopLimit>();

            _contract = new Contract(SYMBOL, _funds, _trailing, _tradeQty, _initialAmount, safetyBands: _safetyBands, marginProtection: _marginProtection, profitPercentage: _profitPercentage);
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
            _strategy = new TrailingBuySellStrategy(_manager, _manager);

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
                .BDDfy("trailing sell should move with the market price");
        }

        [Fact]
        public void sell_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(0), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(10), "And an initial amount of {0}")
                .And(_ => _.given_trade_qty_of(1M))
                .When(_ => _.when_trades_are(10, 20, 30, 29), "When the market values runs up and then suddenly reverses down")
                .Then(_ => _.the_upper_bound_should_be(29.9M), "The Upper Bound should be reset")
                .And(_ => _.the_lower_bound_should_be(27.9M), "The Lower Bound should be reset")
                .And(_ => _.the_average_should_be(28.9M), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_quantity_should_be(0), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be((28.9M * 10) - 1), "The funds should be ${0}")
                .BDDfy("a sell run should trigger a sell order on a reversal");
        }

        [Fact]
        public void buy_stocks_when_there_is_a_reversal()
        {
            this
                .Given(_ => _.given_funds_of(1000), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1M), "And a trail of ${0}")
                .And(_ => _.given_trade_qty_of(0.02M))
                .When(_ => _.when_trades_are(30, 20, 9, 11), "When the market values runs down and then suddenly reverses up")
                .Then(_ => _.the_upper_bound_should_be(11.1M), "The Upper Bound should be reset")
                .And(_ => _.the_lower_bound_should_be(9.1M), "The Lower Bound should be reset")
                .And(_ => _.the_average_should_be(10.1M), "The Average for the contract should be ${0}")
                .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "The Contract RunState should be TriggerRun")
                .And(_ => _.there_should_be_no_buy_limit_value())
                .And(_ => _.there_should_be_no_sell_limit_value())
                .And(_ => _.the_quantity_should_be(2M), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be(978.8m), "The funds should be ${0}")
                .BDDfy("a buy run should trigger a buy order on a reversal");
        }

        [Fact]
        public void borrow_when_funds_are_insufficient()
        {
            this
                .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
                .And(_ => _.given_trailing_of(1), "And a trail of $S{0}")
                .And(_ => _.given_trade_qty_of(1))
                .And(_ => _.given_initial_amount_of(1))
                .When(_ => _.when_trades_are(30, 25, 19, 22),
                    "When the market values runs down and then suddenly reverses up")
                .Then(_ => _.the_quantity_should_be(7), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be(-21.6M), "The funds should be ${0}")
                .And(_ => _.the_safety_bands_should_be(ActionSide.Sell, 4.42m, 4.21m, 3.98m, 3.73m, 3.47m, 3.17m, 2.83m, 2.42m, 1.90m, 1.07m), "the safety bands should be set as stop orders")
                .BDDfy("if there not enough funds but enough purchase power borrow funds to buy");
        }


        /// <summary>
        /// since we are trading a percentage of the liquidity we will never have insufficient funds for a trade
        /// </summary>
        //[Fact]
        //public void do_not_borrow_when_purchase_power_is_insufficient()
        //{
        //    this
        //        .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
        //        .And(_ => _.given_trade_qty_of(1))
        //        .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
        //        .And(_ => _.given_safety_bands_of(10))
        //        .And(_ => _.given_initial_amount_of(1))
        //        .When(_ => _.when_trades_are(30, 25, 27, 22),
        //            "When the market values runs down and then suddenly reverses up")
        //        .Then(_ => _.the_quantity_should_be(1), "The quantity should be the same {0}")
        //        .And(_ => _.the_funds_should_be(100), "The funds should be the same ${0}")
        //        .And(_ => _.there_should_be_no_safety_bands(), "the safety bands should not be set")
        //        .And(_ => _.the_lower_bound_should_be(29))
        //        .And(_ => _.the_upper_bound_should_be(31))
        //        .And(_ => _.there_should_be_no_buy_limit_value())
        //        .And(_ => _.there_should_be_no_sell_limit_value())
        //        .And(_ => _.the_runstate_should_be(RunState.TriggerRun), "the contract state should still be in a trigger run")
        //        .And(_ => _.the_limit_order_should_not_be_called())
        //        .BDDfy("if there not enough funds and not enough purchase power don't do the trade");
        //}


        [Fact]
        public void short_stock_when_there_is_not_enough_stock_on_hand()
        {
            this
                .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
                .And(_ => _.given_trade_qty_of(10))
                .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
                .And(_ => _.given_initial_amount_of(5))
                .When(_ => _.when_trades_are(15, 18, 21, 18),
                    "When the market values go up and suddenly reverse")
                .Then(_ => _.the_quantity_should_be(-5), "The quantity should be {0}")
                .And(_ => _.the_funds_should_be(100M + (19.9M * 10M) - 1M), "The funds should be ${0}")
                .And(_ => _.the_safety_bands_should_be(ActionSide.Buy, 41.71m, 43.10m, 44.72m, 46.64m, 48.97m, 51.91m, 55.81m, 61.39m, 70.60m, 91.80m), "the safety bands should be set as stop orders")
                .BDDfy("if there not enough stock on hand sort the stock");
        }


        /// <summary>
        /// since we are trading a percentage of the liquidity we will never have insufficient funds for a trade
        /// </summary>
        //[Fact]
        //public void do_not_short_stock_when_there_is_not_enough_purchase_power()
        //{
        //    this
        //        .Given(_ => _.given_funds_of(100), "Given funds of ${0}")
        //        .And(_ => _.given_trade_qty_of(10))
        //        .And(_ => _.given_trailing_of(1), "And a trail of ${0}")
        //        .And(_ => _.given_initial_amount_of(3))
        //        .When(_ => _.when_trades_are(15, 18, 32, 30),
        //            "When the market values go up and suddenly reverse")
        //        .Then(_ => _.the_quantity_should_be(3), "The quantity should be {0}")
        //        .And(_ => _.there_should_be_no_buy_limit_value())
        //        .And(_ => _.the_sell_limit_should_be(17))
        //        .And(_ => _.the_runstate_should_be(RunState.SellRun),
        //            "the contract state should still be in a sell run")
        //        .And(_ => _.the_funds_should_be(100M), "The funds should be ${0}")
        //        .BDDfy("do not short stock if there is not enough purchase power");
        //}

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

        [Fact]
        public async Task back_testing_SPGI_polygon()
        {
            _trailing = 50M;
            _tradeQty = 1M;
            TrailingBuySellStrategy.SimulateCommission = (size, price, contract) =>
            {
                if (contract.QuantityOnHand == 0) return 0;
                return  size * 0.02M;
            };

            _initialAmount = 0; 
            _marginProtection = 5M;
            _funds = 1000M;
            _profitPercentage = -0.01M;

            Trace.WriteLine($"start funding:{_funds:C} ");

            var enumTicks = PolygonValues("SPGI", new DateTime(2021, 2, 1), new DateTime(2022, 4, 10));
            await simulate_trades(enumTicks);

            var checkC = await _manager.GetContractState(SYMBOL);
            var netp = (checkC.Funding + (checkC.QuantityOnHand * checkC.AveragePrice) - _funds) / _funds;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.QuantityOnHand:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice:C}");
            Trace.WriteLine($"total % :{(checkC.Funding - _funds) / _funds:P} net with assets % :{netp:P} ");
            Trace.WriteLine("DONE");
        }

        [Fact]
        public async Task back_testing_SPGI_tick()
        {
            _trailing = 50M;
            _tradeQty = 1M;
            _marginProtection = 5M;
            _funds = 1000M;
            _profitPercentage = -0.01M;

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