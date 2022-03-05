using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest.Strategy;
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
        public async Task Test1()
        {
            var Symbol = "NDAQ";
            var funding = 10000;
            var contract = new Contract(
                Symbol,
                funding,
                0.01M,
                0.01M,
                debtCeiling: 27605,
                debtRisk: 0.8M,
                fundingRisk: 0.8M,
                buyBaseLine: 0.2M,
                sellMagnification: 100,
                buyMagnification: 100);

            var contractManager = new ContractManager(0);
            contractManager.RegisterContract(contract);

            var strategy = new TrailingBuySellStrategy(contractManager);

            var random = new Random((int)DateTime.UtcNow.Ticks);
            var checkC = await contractManager.GetContractState(Symbol);
            Trace.WriteLine($"start funding:{checkC.Funding:C} ");
            var start = new DateTime(2021, 10, 1);

            while (true)
            {
                if (start >= new DateTime(2022, 3, 1)) break;

                var end = start.AddMonths(1).Subtract(TimeSpan.FromDays(1));

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
            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.Quantity:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + (checkC.Quantity * checkC.AveragePrice):C}");
            Trace.WriteLine($"apy :{((checkC.Funding - funding) / funding):P} net apy:{(((checkC.Funding + (checkC.Quantity * checkC.AveragePrice)) - funding) / funding):P}");
            Trace.WriteLine("DONE");

            async Task processTick(decimal tick)
            {
                var contract = await contractManager.GetContractState(Symbol);

                var position = new TickPosition
                {
                    ConId = Symbol,
                    Position = tick
                };

                if (contract.AveragePrice == 0)
                {
                    contractManager.InitializeContract(position);
                    await strategy.OrderFilled(position);
                }

                if (contract.RunState == RunState.TriggerRun && tick >= contract.UpperBound)
                {
                    await strategy.UpperTriggerHit(position);
                    return;
                }

                if (contract.RunState == RunState.TriggerRun && tick <= contract.LowerBound)
                {
                    await strategy.LowerTriggerHit(position);
                    return;
                }

                if (contract.RunState == RunState.BuyRun &&
                    (tick >= contract.BuyOrderLimit))
                {
                    await contractManager.BuyActionComplete(new ActionDetails
                    {
                        ConId = Symbol,
                        PricePerUnit = tick,
                        CostOfOrder = contract.BuyQty * tick,
                        Qty = contract.BuyQty
                    });
                    await strategy.OrderFilled(position);
                    return;
                }

                if (contract.RunState == RunState.SellRun &&
                    (tick <= contract.SellOrderLimit))
                {
                    await contractManager.SellActionComplete(new ActionDetails
                    {
                        ConId = Symbol,
                        PricePerUnit = tick,
                        CostOfOrder = contract.SellQty * tick,
                        Qty = contract.SellQty
                    });
                    await strategy.OrderFilled(position);
                    return;
                }

                await strategy.Tick(position);
            }
        }
    }

}