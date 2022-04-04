using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public class TrailingBuySellStrategy : IOrderFilledProcess, IRecordTick
    {
        private readonly IContractManager _contractManager;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount - offset;

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount + offset;
        public TrailingBuySellStrategy(IContractManager contractManager)
        {
            _contractManager = contractManager;
            contractManager.RegisterForOrderFilled(this);
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

        public async Task Tick(TickPosition tick)
        {
            var contractState = await _contractManager.GetContractState(tick.Symbol);

            if (contractState.AveragePrice == 0)
            {
                _contractManager.InitializeContract(tick);
                await OrderFilled(new MarketOrder
                {
                    PricePerUnit = tick.Position,
                    Quantity = 0,
                    Symbol = tick.Symbol
                });
                return;
            }

            if (contractState.RunState != RunState.SellCapped &&
                contractState.MaxSellPrice > 0 &&
                tick.Position >= contractState.MaxSellPrice)
            {
                await _contractManager.PlaceMaxSellOrder(new MarketOrder
                {
                    Quantity = contractState.TradeQty,
                    PricePerUnit = contractState.MaxSellPrice,
                    Symbol = tick.Symbol
                });
                return;
            }

            if (contractState.RunState == RunState.SellCapped) return;

            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.BuyOrderLimit) return;

                await _contractManager.PlaceTrailingBuyOrder(new MarketOrder
                {
                    Quantity = contractState.TradeQty,
                    Symbol = tick.Symbol,
                    PricePerUnit = limit
                });

                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.SellOrderLimit) return;

                await _contractManager.PlaceTrailingSellOrder(new SellMarketOrder
                {
                    Symbol = tick.Symbol,
                    Quantity = contractState.TradeQty,
                    PricePerUnit = limit,
                    MaxSellPrice = contractState.MaxSellPrice
                });

                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position >= contractState.UpperBound)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;

                await _contractManager.PlaceTrailingSellOrder(new SellMarketOrder
                {
                    Quantity = contractState.TradeQty,
                    Symbol = tick.Symbol,
                    PricePerUnit = limit,
                    MaxSellPrice = contractState.MaxSellPrice
                });
                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position <= contractState.LowerBound)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;

                await _contractManager.PlaceTrailingBuyOrder(new MarketOrder
                {
                    Quantity = contractState.TradeQty,
                    Symbol = tick.Symbol,
                    PricePerUnit = limit
                });
            }
        }


        public async Task OrderFilled(MarketOrder order)
        {
            var symbol = order.Symbol;
            var contractState = await _contractManager.GetContractState(symbol);
            var average = await _contractManager.GetContractsAverageValue(symbol);
            var marketPrice = order.PricePerUnit;

            // The upper bound trigger value for a sell run is the market price plus the trailing offset
            var upperBound = UpperLimit(average, contractState.TrailingOffset);

            // The lower bound for a buy run is the highest value below
            // average that an order can be completely fulled less the trailing offset
            var lowerBound = LowerLimit(average, contractState.TrailingOffset);

            // If we are not shorting stock then this value is negative otherwise its the highest
            // possible price that we can short the stock
            var maxSellPrice = -1M;

            // This is the cost of a trade right now and it includes the trailing offset
            var costOfTrade = marketPrice * contractState.TradeQty + contractState.TrailingOffset;

            var borrowPower = BorrowPower(contractState.Funding, contractState.QuantityOnHand, marketPrice, Contract.InitialMargin);

            // The buying power is what ever amount we can borrow plus whatever funds we have or
            // less what we have already borrowed (in which case Funding would be < 0)
            var buyingPower = borrowPower + contractState.Funding;

            // If we can't afford to buy the stock now then we need to find the highest price we can buy at
            if (buyingPower < costOfTrade)
            {
                // The lower bound is the highest price we can afford to buy at and it must include the margin risk and trailing offset
                lowerBound = LowerLimit(buyingPower / contractState.TradeQty, contractState.TrailingOffset) - contractState.MarginRisk;
                lowerBound = lowerBound < 0 ? 0 : lowerBound;
            }

            // If we don't have enough stock on hand to do a sell then we need to short
            // The maxSellPrice gives us the amount of the highest possible price we can short at
            if (contractState.QuantityOnHand < contractState.TradeQty)
            {
                var purchasePower = borrowPower;
                var qty = contractState.TradeQty;

                // If I have some stocks then I need to only short the difference
                if (contractState.QuantityOnHand > 0)
                {
                    qty -= contractState.QuantityOnHand;
                }
                else
                {
                    // My purchase power is reduced if I have already shorted some stocks
                    purchasePower -= Math.Abs(contractState.QuantityOnHand) * marketPrice;
                }

                // Include the MarginRisk when obtaining the max sell limit
                maxSellPrice = purchasePower / qty - contractState.MarginRisk;
            }

            // If we are trading on margin going long or short we need to put the protection
            // limits in place to avoid a margin call
            if (contractState.Funding < 0 || contractState.QuantityOnHand < 0)
            {
                // This is the safety strategy to avoid a margin call. To avoid a complete loss
                // we rather sell or buy in smaller units so as to buy more time for the market
                // to recover instead of realizing a complete loss when the margin price is hit
                var qty = Math.Abs(contractState.QuantityOnHand) / contractState.SafetyBands;

                // If we are loaning money we need to sell some of our stock before the price
                // drops too far down and if we are shorting stock we need to buy some before the
                // price rises too high
                var action = (contractState.Funding < 0)
                    // Since funding is < 0 we are loaning money which means we need to sell
                    ? (Func<MarketOrder, Task>)_contractManager.PlaceEmergencySellOrder
                    // If we are not loaning money then we are shorting stock which
                    // means we need to buy
                    : _contractManager.PlaceEmergencyBuyOrder;

                // Using the maintenance margin we can work out what values
                // are needed for the stop orders
                foreach (var threshold in SafetyThresholds(
                    contractState.Funding,
                    contractState.QuantityOnHand,
                    Contract.MaintenanceMargin,
                    contractState.MarginRisk,
                    contractState.SafetyBands))
                {
                    await action(new MarketOrder
                    {
                        PricePerUnit = threshold,
                        Quantity = qty,
                        Symbol = contractState.Symbol
                    });
                }
            }
            else
            {
                // If we are not trading on margin we should cleanup any emergency stop orders
                // since we are not likely to get a margin call
                await _contractManager.ClearEmergencyOrders(symbol);
            }

            // The market price is too high to short the stock and we are in a shorting position
            if (contractState.QuantityOnHand < contractState.TradeQty && marketPrice > maxSellPrice)
            {
                await _contractManager.PlaceMaxSellOrder(new MarketOrder
                {
                    Symbol = symbol,
                    Quantity = contractState.QuantityOnHand,
                    PricePerUnit = maxSellPrice
                });
                return;
            }

            if (marketPrice > upperBound)
            {
                // Place a sell stop order
                // its a trail->stop at max if it has a value or
                // or just a trail if max does not have a value
                var limit = LowerLimit(marketPrice, contractState.TrailingOffset);
                if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;

                await _contractManager.PlaceTrailingSellOrder(new SellMarketOrder
                {
                    Symbol = symbol,
                    Quantity = contractState.TradeQty,
                    PricePerUnit = limit,
                    MaxSellPrice = maxSellPrice
                });
                return;
            }

            // lowerBound being the highest market price we can buy at even if we were to borrow
            if (marketPrice < lowerBound)
            {
                // Place a buy stop order
                // its a trail order starting at lowerBound using trailing offset
                var limit = UpperLimit(marketPrice, contractState.TrailingOffset);
                if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;

                await _contractManager.PlaceTrailingBuyOrder(new MarketOrder
                {
                    Symbol = symbol,
                    Quantity = contractState.TradeQty,
                    PricePerUnit = limit
                });
                return;
            }

            // Do both sell and buy triggers
            // For a sell place a trigger->trail->stop if max has a value
            // or just trigger->trail if not
            // For a buy its a trigger->trail starting at lowerBound
            await _contractManager.CreateTrigger(new TriggerDetails
            {
                Symbol = symbol,
                UpperLimit = upperBound,
                LowerLimit = lowerBound,
                MaxSellPrice = maxSellPrice
            });
        }
    }
}