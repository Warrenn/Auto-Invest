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
        public async Task Test2()
        {
            var hist = await Yahoo.GetHistoricalAsync(
                "SPGI",
                new DateTime(),
                new DateTime(2017, 01, 31));

            foreach (var candle in hist)
            {
                Trace.Write(candle.AdjustedClose);
            }
        }

        [Test]
        public async Task Test1()
        {
            var contractService = new ContractService(
                new Dictionary<string, ContractState>()
                {
                    {
                        "SPGI", new ContractState
                        {
                            ConId = "SPGI",
                            RunState = RunState.TriggerRun,
                            Offset = 0.01M,
                            Funding = 100000,
                            FundingRisk = 1,
                            ShortFund = 100000,
                            BuyBaseLine = 0.2M
                        }
                    }
                });
            var strategy = new TrailingBuySellStrategy(contractService);

            var counter = 0;
            var year = 2017;
            var random = new Random((int)DateTime.UtcNow.Ticks);
            var checkC = await contractService.GetContractState("SPGI");
            Trace.WriteLine($"start funding:{checkC.Funding} ");
            var m = -1;

            while (true)
            {
                m++;
                m %= 12;
                if (m == 11) year++;
                if (year == 2021) break;

                var start = new DateTime(year, (m + 1), 1);
                var end = start.AddMonths(1).Subtract(TimeSpan.FromDays(1));

                var hist = await Yahoo.GetHistoricalAsync("SPGI", start, end);

                foreach (var candle in hist)
                {
                    await processTick(candle.Open);
                    if (random.Next(0, 1) == 0)
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
            }
            checkC = await contractService.GetContractState("SPGI");
            Trace.WriteLine($"end funding:{checkC.Funding} qty:{checkC.Quantity} ave:{checkC.AveragePrice}");
            Trace.WriteLine($"apy :{((checkC.Funding - 100000) / 100000) * 100} net apy:{(((checkC.Funding + (checkC.Quantity * checkC.AveragePrice)) - 100000) / 100000) * 100}");


            async Task processTick(decimal tick)
            {
                counter++;
                var contract = await contractService.GetContractState("SPGI");

                var position = new TickPosition
                {
                    ConId = "SPGI",
                    Position = tick
                };

                if (counter == 1)
                {
                    var qty = contractService.BuyQtyStrategy(contract, tick);
                    await contractService.BuyActionComplete(new ActionDetails
                    {
                        ConId = "SPGI",
                        PricePerUnit = tick,
                        CostOfOrder = tick * qty,
                        Qty = qty
                    });
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
                    (tick >= contract.BuyLimit))
                {
                    await contractService.BuyActionComplete(new ActionDetails
                    {
                        ConId = "SPGI",
                        PricePerUnit = tick,
                        CostOfOrder = contract.BuyQty * tick,
                        Qty = contract.BuyQty
                    });
                    await strategy.OrderFilled(position);
                    return;
                }

                if (contract.RunState == RunState.SellRun &&
                    (tick <= contract.SellLimit))
                {
                    await contractService.SellActionComplete(new ActionDetails
                    {
                        ConId = "SPGI",
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