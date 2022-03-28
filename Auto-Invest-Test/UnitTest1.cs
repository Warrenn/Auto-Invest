using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using NUnit.Framework;
using TestStack.BDDfy;
using YahooFinanceApi;

namespace Auto_Invest_Test
{
    [Story(
        AsA = "Trader",
        IWant = "To Automate my trades",
        SoThat = "I can have automatic trades")]
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        private void ShouldBeOK() { }

        private void TheFundsAre(decimal amt) { }

        private async Task TradesAre(params decimal[] trades) { }

        [Test]
        public async Task SetupTriggerStopLosses()
        {
            // G

            var trades = new[] { 10, 12 };

            //trigger a sale run
            //trigger a buy run
            //move a stop for buy
            //move a stop for sell
            //fill a contract
            // - the avereage is correct
            // - the 

            // //trigger a sell run
            // PerformTrade(cond, trades);

            //Assert short sale
            this
            .Given(_ => _.TheFundsAre(1000))
            .When(_ => _.TradesAre(10, 11, 12))
            .Then(_ => _.ShouldBeOK())
            .BDDfy();
        }

        [Test]
        public void GetTheCorrectMaxSellingPrice()
        {
            var offset = 1M;
            TrailingBuySellStrategy.SafetyPrice(75000, -1000, Contract.MaintenanceMargin, offset);

            var mc = TrailingBuySellStrategy.MarginCallPrice(50M, Contract.InitialMargin, Contract.MaintenanceMargin);
        }

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