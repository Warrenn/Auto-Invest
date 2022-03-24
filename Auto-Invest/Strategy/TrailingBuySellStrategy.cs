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
            uint safetyLayers)
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

        public static decimal BorrowPower(decimal funds, decimal quantity, decimal price, decimal margin) =>
            (quantity * price + funds) / margin;

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

                var orderCost = price * contract.TradeQty + contract.MarginRisk;

                // if we have the funds and don't need to borrow do the trade
                if (contract.Funding > orderCost) return contract.TradeQty;

                var purchasePower = BorrowPower(contract.Funding, contract.Quantity, price, Contract.InitialMargin) + contract.Funding;
                //if we have the purchase power to loan do the trade
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

                var qty = contract.TradeQty;
                // if we have the stocks and don't need to borrow do the trade
                if (contract.Quantity >= qty) return contract.TradeQty;

                // if we don't have enough stocks workout how much we can short
                var purchasePower = BorrowPower(contract.Funding, contract.Quantity, price, Contract.InitialMargin);

                // if I have some stocks then I need to only short the difference
                if (contract.Quantity > 0)
                {
                    qty -= contract.Quantity;
                }
                else
                {
                    // My purchase power is reduced if I have already shorted some stocks
                    purchasePower -= Math.Abs(contract.Quantity) * price;
                }

                var orderCost = price * qty + contract.MarginRisk;
                //if we have the purchase power to short do the trade
                return purchasePower > orderCost ? contract.TradeQty : 0;
            };

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.Symbol);

            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.BuyOrderLimit) return;

                var qty = BuyQtyStrategy(contractState, limit);
                await _contractManager.PlaceBuyStopOrder(new MarketOrder
                {
                    Quantity = qty,
                    Symbol = tick.Symbol,
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
                    Symbol = tick.Symbol,
                    Quantity = qty,
                    PricePerUnit = limit
                });
            }
        }

        public async Task UpperTriggerHit(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.Symbol);
            var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
            if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = SellQtyStrategy(contractState, limit);
            await _contractManager.PlaceSellStopOrder(new MarketOrder
            {
                Quantity = qty,
                Symbol = tick.Symbol,
                PricePerUnit = limit
            });
        }


        public async Task LowerTriggerHit(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.Symbol);
            var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
            if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;

            var qty = BuyQtyStrategy(contractState, limit);

            await _contractManager.PlaceBuyStopOrder(new MarketOrder
            {
                Quantity = qty,
                Symbol = tick.Symbol,
                PricePerUnit = limit
            });
        }

        public async Task OrderFilled(TickPosition tick)
        {
            // cancel any existing trade orders -limit protection, trigger->trail->stops

            // If we are trading on margin either long or short
            // get the emergency stop layers and place the stop orders
            // the upper bound for a sell run is the trailing offset
            // but the lower bound for a buy run is the highest value below
            // average that an order can be completely fulled if purchase power is
            // less than the full price of a full purchase order

            var contractState = await _contractManager.GetContractState(tick.Symbol);
            var average = await _contractManager.GetContractsAverageValue(tick.Symbol);

            var upperBound = UpperLimit(average, contractState.TrailingOffset);
            var lowerBound = LowerLimit(average, contractState.TrailingOffset);
            var maxSellLimit = 0M;

            var costOfTrade = average * contractState.TradeQty + contractState.TrailingOffset;
            var buyingPower =
                BorrowPower(contractState.Funding, contractState.Quantity, average, Contract.InitialMargin) +
                contractState.Funding;

            if (buyingPower < costOfTrade)
            {
                lowerBound = buyingPower / contractState.Quantity - contractState.TrailingOffset;
            }

            if (contractState.Quantity < contractState.TradeQty)
            {
                var purchasePower = BorrowPower(contractState.Funding, contractState.Quantity, average, Contract.InitialMargin);
                var qty = contractState.TradeQty;

                // if I have some stocks then I need to only short the difference
                if (contractState.Quantity > 0)
                {
                    qty -= contractState.Quantity;
                }
                else
                {
                    // My purchase power is reduced if I have already shorted some stocks
                    purchasePower -= Math.Abs(contractState.Quantity) * average;
                }

                maxSellLimit = purchasePower / qty - contractState.MarginRisk;
            }

            //if we are trading on margin going long or short we need to put the protection limits in place for maintenance margin
            if (contractState.Funding < 0 || contractState.Quantity < 0)
            {
                var qty = Math.Abs(contractState.Quantity) / contractState.SafetyLayers;
                foreach (var threshold in SafetyThresholds(
                    contractState.Funding,
                    contractState.Quantity, Contract.MaintenanceMargin,
                    contractState.MarginRisk,
                    contractState.SafetyLayers))
                {
                    //for each threshold put a stop limit for the threshold price and qty
                }
            }


            if (maxSellLimit != 0 && tick.Position > maxSellLimit)
            {
                // the current market value is too high put a stop limit order to sell at max sellLimit
            }

            if (tick.Position > upperBound)// && you don't have a stop limit because the market value is too high
            {
                // Place a sell stop order
                // its a trail->stop at max if it has a value or
                // or just a trail if max does not have a value

                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceSellStopOrder(new MarketOrder
                {
                    Symbol = tick.Symbol,
                    Quantity = qty,
                    PricePerUnit = limit
                });
                return;
            }

            if (tick.Position < lowerBound) //lower bound being the value that includes the max amount that can be borrowed
            {
                // Place a buy stop order
                // its a trail order starting at lowerbound using trailing offset
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                var qty = BuyQtyStrategy(contractState, limit);

                await _contractManager.PlaceBuyStopOrder(new MarketOrder
                {
                    Symbol = tick.Symbol,
                    Quantity = qty,
                    PricePerUnit = limit
                });
                return;
            }

            // do both sell and buy triggers
            // for a sell place a trigger->trail->stop if max has a value
            // or just trigger->trail if not
            // for a buy its a trigger->trail starting at lowerBound

            await _contractManager.CreateTrigger(new TriggerDetails
            {
                ConId = tick.Symbol,
                UpperLimit = upperBound,
                LowerLimit = lowerBound
            });
        }
    }
}