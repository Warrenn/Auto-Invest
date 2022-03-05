using System;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class TrailingBuySellStrategy
    {
        private readonly IContractManager _contractManager;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount * (1 - offset);

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount * (1 + offset);

        public TrailingBuySellStrategy(IContractManager contractManager)
        {
            _contractManager = contractManager;
        }

        public Func<Contract, decimal, decimal> BuyQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price > contract.AveragePrice) return 0;

                var funding =
                        (contract.Funding + contract.Quantity * contract.AveragePrice) * contract.FundingRisk;

                if (funding <= 0) return 0;

                var magnification = contract.BuyMagnification == 0 ? 1 : contract.BuyMagnification;
                var baseLine = contract.BuyBaseLine == 0 ? 1 : contract.BuyBaseLine;
                var ratio = contract.AveragePrice == 0
                    ? 1
                    : (contract.AveragePrice - price) / (contract.AveragePrice * baseLine);

                funding *= ratio;
                var qty = (funding / price) * magnification;

                var netCost = contract.Funding - qty * price;
                if (netCost > (contract.DebtCeiling * -1)) return qty;

                funding = (contract.Funding + contract.DebtCeiling) * contract.FundingRisk;
                if (funding < 0) return 0;
                qty = funding / price;

                return qty;
            };

        public Func<Contract, decimal, decimal> SellQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price < contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                var sellMargin = (price - contract.AveragePrice) / contract.AveragePrice;

                if (sellMargin <= 1 && contract.Quantity > 0) return contract.Quantity * sellMargin;

                var qty = 0M;
                if (sellMargin > 1 && contract.Quantity > 0)
                {
                    qty = contract.Quantity;
                    sellMargin -= 1;
                }

                sellMargin = Math.Min(1, sellMargin);

                var availableDebt = contract.DebtCeiling * contract.DebtRisk;

                if (contract.Quantity < 0) availableDebt += contract.Quantity * contract.AveragePrice;
                if (contract.Funding < 0) availableDebt += contract.Funding;
                if (availableDebt < 0) return 0;

                qty += availableDebt * sellMargin / price;
                return qty;
            };

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);

            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.Offset);
                if (limit > contractState.BuyOrderLimit) return;

                var qty = BuyQtyStrategy(contractState, limit);
                await _contractManager.PlaceBuyStopOrder(new StopOrder
                {
                    Quantity = qty,
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });

                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.Offset);
                if (limit < contractState.SellOrderLimit) return;

                var qty = SellQtyStrategy(contractState, limit);
                await _contractManager.PlaceSellStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    Quantity = qty,
                    PricePerUnit = limit
                });
            }
        }

        public async Task UpperTriggerHit(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);
            var limit = LowerLimit(tick.Position, contractState.Offset);
            if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = SellQtyStrategy(contractState, limit);
            await _contractManager.PlaceSellStopOrder(new StopOrder
            {
                Quantity = qty,
                ConId = tick.ConId,
                PricePerUnit = limit
            });
        }


        public async Task LowerTriggerHit(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);
            var limit = UpperLimit(tick.Position, contractState.Offset);
            if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = BuyQtyStrategy(contractState, limit);

            await _contractManager.PlaceBuyStopOrder(new StopOrder
            {
                Quantity = qty,
                ConId = tick.ConId,
                PricePerUnit = limit
            });
        }

        public async Task OrderFilled(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);
            var average = await _contractManager.GetContractsAverageValue(tick.ConId);

            var upperBound = UpperLimit(average, contractState.TriggerMargin);
            var lowerBound = LowerLimit(average, contractState.TriggerMargin);

            if (tick.Position > upperBound)
            {
                // Place a sell stop order
                var limit = LowerLimit(tick.Position, contractState.Offset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceSellStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    Quantity = qty,
                    PricePerUnit = limit
                });
                return;
            }

            if (tick.Position < lowerBound)
            {
                // Place a buy stop order
                var limit = UpperLimit(tick.Position, contractState.Offset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceBuyStopOrder(new StopOrder
                {
                    ConId = tick.ConId,
                    Quantity = qty,
                    PricePerUnit = limit
                });
                return;
            }

            await _contractManager.CreateTrigger(new TriggerDetails
            {
                ConId = tick.ConId,
                UpperLimit = upperBound,
                LowerLimit = lowerBound
            });
        }
    }
}