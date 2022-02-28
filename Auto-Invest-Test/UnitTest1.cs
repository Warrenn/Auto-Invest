using System;
using System.Collections.Generic;
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
                    {"SPGI", new ContractState
                    {
                        ConId = "SPGI",
                        RunState = RunState.TriggerRun,
                        Offset = 0.01M,
                        Quantity = 0,
                        Funding = 100000
                    }}
                });
            var strategy = new TrailingBuySellStrategy(contractService);
        }
    }

}