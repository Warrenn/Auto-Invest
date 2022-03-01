using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class TrailingBuySellStrategy
    {
        private readonly IContractService _contractService;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount - offset;

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount + offset;

        public TrailingBuySellStrategy(IContractService contractService)
        {
            _contractService = contractService;
        }

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _contractService.GetContractState(tick.ConId);
            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.Offset);
                if (limit > contractState.BuyLimit) return;

                await _contractService.PlaceBuyStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });

                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.Offset);
                if (limit < contractState.SellLimit) return;

                await _contractService.PlaceSellStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });
            }
        }

        public async Task UpperTriggerHit(TickPosition tick)
        {
            var contractState = await _contractService.GetContractState(tick.ConId);
            var limit = LowerLimit(tick.Position, contractState.Offset);

            await _contractService.PlaceSellStopOrder(new StopOrder
            {
                ConId = tick.ConId,
                PricePerUnit = limit
            });
        }


        public async Task LowerTriggerHit(TickPosition tick)
        {
            var contractState = await _contractService.GetContractState(tick.ConId);
            var limit = UpperLimit(tick.Position, contractState.Offset);

            await _contractService.PlaceBuyStopOrder(new StopOrder
            {
                ConId = tick.ConId,
                PricePerUnit = limit
            });
        }

        public async Task OrderFilled(TickPosition tick)
        {
            var contractState = await _contractService.GetContractState(tick.ConId);
            var average = await _contractService.GetContractsAverageValue(tick.ConId);

            var upperBound = UpperLimit(average, contractState.Offset);
            var lowerBound = LowerLimit(average, contractState.Offset);

            if (tick.Position > upperBound)
            {
                // Place a sell stop order
                var limit = LowerLimit(tick.Position, contractState.Offset);
                await _contractService.PlaceSellStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });
                return;
            }

            if (tick.Position < lowerBound)
            {
                // Place a buy stop order
                var limit = UpperLimit(tick.Position, contractState.Offset);
                await _contractService.PlaceBuyStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });
                return;
            }

            await _contractService.CreateTrigger(new TriggerDetails
            {
                ConId = tick.ConId,
                UpperLimit = upperBound,
                LowerLimit = lowerBound
            });
        }
    }
}