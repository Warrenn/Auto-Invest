using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using NUnit.Framework;
using YahooFinanceApi;

namespace Auto_Invest_Test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetTheCorrectMaxSellingPrice()
        {
            var cT = Task.Factory.CancellationToken;
            var mrst = ((ManualResetEvent) cT.WaitHandle).WaitOne();
            
            var offset = 0M;
            var p =
            TrailingBuySellStrategy.SafetyPrice(75000, -1000, Contract.MaintenanceMargin, offset);

            var mc = TrailingBuySellStrategy.MarginCallPrice(50M, Contract.InitialMargin, Contract.MaintenanceMargin);
            Assert.AreEqual(mc + offset, p);
        }

        [Test]
        public async Task Test1()
        {
            var Symbol = "NDAQ";
            var funding = 10000;
            var contract = new Contract(
                Symbol,
                funding,
                0.1M,
                0.0001M,
                marginRisk: 10.0M);

            var contractManager = new ContractManager(null);
            contractManager.RegisterContract(contract);

            var strategy = new TrailingBuySellStrategy(contractManager);

            var random = new Random((int)DateTime.UtcNow.Ticks);
            var checkC = await contractManager.GetContractState(Symbol);
            Trace.WriteLine($"start funding:{checkC.Funding:C} ");
            var start = new DateTime(2017, 1, 1);
            var end = start;

            while (true)
            {
                if (start >= new DateTime(2022, 1, 1)) break;

                end = start.AddMonths(1).Subtract(TimeSpan.FromDays(1));

                var hist = await Yahoo.GetHistoricalAsync(Symbol, start, end);

                foreach (var candle in hist)
                {
                    await processTick(candle.Open);
                    if (random.Next(0, 2) == 0)
                    {
                        await processTick(candle.Low);
                        await processTick(candle.High);
                    }
                    else
                    {
                        await processTick(candle.High);
                        await processTick(candle.Low);
                    }

                    await processTick(candle.Close);
                }

                start = start.AddMonths(1);
            }

            checkC = await contractManager.GetContractState(Symbol);
            var netp = (((checkC.Funding + (checkC.Quantity * checkC.AveragePrice)) - funding) / funding);
            var years = 5;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.Quantity:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + (checkC.Quantity * checkC.AveragePrice):C}");
            Trace.WriteLine($"total % :{((checkC.Funding - funding) / funding):P} net with assets % :{netp:P} average for {years} years {netp / years:P}");
            Trace.WriteLine("DONE");

            async Task processTick(decimal tick)
            {
                var contract = await contractManager.GetContractState(Symbol);

                var position = new TickPosition
                {
                    Symbol = Symbol,
                    Position = tick
                };

                if (contract.AveragePrice == 0)
                {
                    contractManager.InitializeContract(position);
                }


                if (contract.RunState == RunState.BuyRun &&
                    (tick >= contract.BuyOrderLimit))
                {
                    await contractManager.TrailingBuyComplete(new ActionDetails
                    {
                        Symbol = Symbol,
                        PricePerUnit = contract.BuyOrderLimit,
                        CostOfOrder = contract.TradeQty * tick,
                        Qty = contract.TradeQty
                    });
                    return;
                }

                if (contract.RunState == RunState.SellRun &&
                    (tick <= contract.SellOrderLimit))
                {
                    await contractManager.TrailingSellComplete(new ActionDetails
                    {
                        Symbol = Symbol,
                        PricePerUnit = contract.SellOrderLimit,
                        CostOfOrder = contract.TradeQty * tick,
                        Qty = contract.TradeQty
                    });
                    return;
                }

                await strategy.Tick(position);
            }
        }
    }

}