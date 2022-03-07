using System;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class TrailingBuySellStrategy
    {
        private readonly IContractManager _contractManager;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount - offset;

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount + offset;

        public TrailingBuySellStrategy(IContractManager contractManager)
        {
            _contractManager = contractManager;
        }
        private const decimal StockQty = 10M;
        private const decimal MarginPercent = 0.5M;

        public Func<Contract, decimal, decimal> BuyQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price > contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                //if (contract.Funding > 0) return contract.Funding / price;

                var margin = MarginPercent * price * (Math.Abs(contract.Quantity) + StockQty);
                var equity = contract.Quantity * price + contract.Funding;
                return (margin > equity) ? 0 :StockQty;
            };

        public Func<Contract, decimal, decimal> SellQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price < contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                //if (contract.Quantity > 0 ) return contract.Quantity;

                //var margin = MarginPercent * price * Math.Abs(contract.Quantity);
                //var equity = contract.Quantity * price + contract.Funding;
                //return margin > equity ? 0 : (equity / MarginPercent) / price;
                var margin = MarginPercent * price * (Math.Abs(contract.Quantity) + StockQty);
                var equity = contract.Quantity * price + contract.Funding;
                return (margin > equity) ? 0 : StockQty;
            };

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);

            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.BuyOrderLimit) return;

                var qty = BuyQtyStrategy(contractState, limit);
                await _contractManager.PlaceBuyStopOrder(new MarketOrder
                {
                    Quantity = qty,
                    ConId = tick.ConId,
                    PricePerUnit = limit
                });

                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.SellOrderLimit) return;

                var qty = SellQtyStrategy(contractState, limit);
                await _contractManager.PlaceSellStopOrder(new MarketOrder
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
            var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
            if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = SellQtyStrategy(contractState, limit);
            await _contractManager.PlaceSellStopOrder(new MarketOrder
            {
                Quantity = qty,
                ConId = tick.ConId,
                PricePerUnit = limit
            });
        }


        public async Task LowerTriggerHit(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.ConId);
            var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
            if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = BuyQtyStrategy(contractState, limit);

            await _contractManager.PlaceBuyStopOrder(new MarketOrder
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

            var upperBound = UpperLimit(average, contractState.TriggerRange);
            var lowerBound = LowerLimit(average, contractState.TriggerRange);

            if (tick.Position > upperBound)
            {
                // Place a sell stop order
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceSellStopOrder(new MarketOrder
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
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceBuyStopOrder(new MarketOrder
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