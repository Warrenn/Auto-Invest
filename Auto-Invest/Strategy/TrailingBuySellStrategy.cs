using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class TrailingBuySellStrategy
    {
        private readonly IStrategy _strategy;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount * (1 - offset);

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount * (1 + offset);

        public TrailingBuySellStrategy(IStrategy strategy)
        {
            _strategy = strategy;
        }

        private async Task PlaceSellStopOrder(string conId, ContractState contractState, decimal limit)
        {
            contractState.SellLimit = limit;
            contractState.RunState = RunState.SellRun;

            await _strategy.UpdateContractState(contractState);
            await _strategy.PlaceSellStopOrder(new StopOrder
            {
                ConId = conId,
                Price = limit
            });
        }

        private async Task PlaceBuyStopOrder(string conId, ContractState contractState, decimal limit)
        {
            contractState.BuyLimit = limit;
            contractState.RunState = RunState.BuyRun;

            await _strategy.UpdateContractState(contractState);
            await _strategy.PlaceBuyStopOrder(new StopOrder
            {
                ConId = conId,
                Price = limit
            });
        }

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _strategy.GetContractState(tick.ConId);
            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.Offset);
                if (limit > contractState.BuyLimit) return;

                await PlaceBuyStopOrder(tick.ConId, contractState, limit);

                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.Offset);
                if (limit < contractState.SellLimit) return;

                await PlaceSellStopOrder(tick.ConId, contractState, limit);
            }
        }

        public async Task UpperTriggerHit(TickPosition tick)
        {
            var contractState = await _strategy.GetContractState(tick.ConId);
            var limit = LowerLimit(tick.Position, contractState.Offset);

            await PlaceSellStopOrder(tick.ConId, contractState, limit);
        }


        public async Task LowerTriggerHit(TickPosition tick)
        {
            var contractState = await _strategy.GetContractState(tick.ConId);
            var limit = UpperLimit(tick.Position, contractState.Offset);

            await PlaceBuyStopOrder(tick.ConId, contractState, limit);
        }

        public async Task OrderFilled(TickPosition tick)
        {
            var contractState = await _strategy.GetContractState(tick.ConId);
            var average = await _strategy.GetContractsAverageValue(tick.ConId);

            var upperBound = UpperLimit(average, contractState.Offset);
            var lowerBound = LowerLimit(average, contractState.Offset);

            contractState.UpperBound = upperBound;
            contractState.LowerBound = lowerBound;
            contractState.AveragePrice = average;

            if (tick.Position > upperBound)
            {
                // Place a sell stop order
                var limit = LowerLimit(tick.Position, contractState.Offset);
                await PlaceSellStopOrder(tick.ConId, contractState, limit);
                return;
            }

            if (tick.Position < lowerBound)
            {
                // Place a buy stop order
                var limit = UpperLimit(tick.Position, contractState.Offset);
                await PlaceBuyStopOrder(tick.ConId, contractState, limit);
                return;
            }

            contractState.RunState = RunState.TriggerRun;
            contractState.BuyLimit = average;
            contractState.SellLimit = average;

            await _strategy.UpdateContractState(contractState);
            await _strategy.CreateTrigger(new TriggerDetails
            {
                ConId = tick.ConId,
                UpperLimit = upperBound,
                LowerLimit = lowerBound
            });
        }
    }
}