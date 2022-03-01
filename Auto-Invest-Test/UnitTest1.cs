using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest.Strategy;
using NUnit.Framework;

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
            var contractService = new ContractService(
                new Dictionary<string, ContractState>()
                {
                    {
                        "SPGI", new ContractState
                        {
                            ConId = "SPGI",
                            RunState = RunState.TriggerRun,
                            Offset = 0.01M,
                            Funding = 100000
                        }
                    }
                });
            var strategy = new TrailingBuySellStrategy(contractService);

            var ticks = new List<decimal> { 3, 4, 5, 4, 3, 2, 1, 2, 3, 4, 5 };
            var counter = 0;

            foreach (var tick in ticks)
            {
                counter++;
                var isLastTick = ticks.Count == counter;
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
                    continue;
                }
                if (contract.RunState == RunState.TriggerRun && tick <= contract.LowerBound)
                {
                    await strategy.LowerTriggerHit(position);
                    continue;
                }

                if (contract.RunState == RunState.BuyRun && 
                    (tick >= contract.BuyLimit || isLastTick))
                {
                    await contractService.BuyActionComplete(new ActionDetails
                    {
                        ConId = "SPGI",
                        PricePerUnit = tick,
                        CostOfOrder = contract.BuyQty * tick,
                        Qty = contract.BuyQty
                    });
                    await strategy.OrderFilled(position);
                    continue;
                }

                if (contract.RunState == RunState.SellRun &&
                    (tick <= contract.SellLimit || isLastTick))
                {
                    await contractService.SellActionComplete(new ActionDetails
                    {
                        ConId = "SPGI",
                        PricePerUnit = tick,
                        CostOfOrder = contract.SellQty * tick,
                        Qty = contract.SellQty
                    });
                    await strategy.OrderFilled(position);
                    continue;
                }

                await strategy.Tick(position);
            }

            var state = await contractService.GetContractState("SPGI");
        }
    }

}