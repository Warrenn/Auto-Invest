using System;
using System.Collections.Generic;
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

        public static IEnumerable<decimal> SafetyThresholds(
            decimal funds,
            decimal quantity,
            decimal marginPct,
            decimal safetyOffset,
            int safetyLayers)
        {
            var quantityChange = quantity / safetyLayers;
            for (var i = 0; i < safetyLayers; i++)
            {
                var price = SafetyPrice(funds, quantity, marginPct, safetyOffset);
                yield return price;
                quantity -= quantityChange;
                funds += quantityChange * price;
            }
        }

        public static decimal SafetyPrice(decimal funds, decimal quantity, decimal marginPct, decimal safetyOffset) =>
            funds / (Math.Abs(quantity) * marginPct - quantity) + safetyOffset * Math.Sign(quantity);

        public static decimal PurchasePower(decimal funds, decimal quantity, decimal price, decimal margin) =>
            (quantity * price + funds) / margin - Math.Abs(quantity) * price;

        public static decimal MarginCallPrice(decimal price, decimal initialMargin, decimal maintenanceMargin) =>
            price * (1 - initialMargin) / (1 - maintenanceMargin);

        public Func<Contract, decimal, decimal> BuyQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                // never buy above the average price
                if (contract.AveragePrice > 0 &&
                    price > contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                var orderCost = price * contract.TradeQty;

                // if we have the funds and don't need to borrow do the trade
                if (contract.Funding > orderCost) return contract.TradeQty;

                var purchasePower = PurchasePower(contract.Funding, contract.Quantity, price, Contract.InitialMargin);
                //if we have the purchase power do the trade
                return purchasePower > orderCost ? contract.TradeQty : 0;
            };

        public Func<Contract, decimal, decimal> SellQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                // never sell below the average price that was bought
                if (contract.AveragePrice > 0 &&
                    price < contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                // if we have the stocks and don't need to borrow do the trade
                if (contract.Quantity >= contract.TradeQty) return contract.TradeQty;

                var orderCost = price * contract.TradeQty;
                var purchasePower = PurchasePower(contract.Funding, contract.Quantity, price, Contract.InitialMargin);
                //if we have the purchase power do the trade
                return purchasePower > orderCost ? contract.TradeQty : 0;
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

            var upperBound = UpperLimit(average, contractState.TrailingOffset);
            var lowerBound = LowerLimit(average, contractState.TrailingOffset);

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