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

        /// <summary>
        /// The highest market price that a buy order can be completed for the desired trade quantity when borrowing funds
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The initial margin percentage</param>
        /// <param name="tradeQty">The amount of stock that will be traded on the next order</param>
        /// <param name="qtyOnHand">The amount of stock on hand at this moment</param>
        /// <returns>The highest market price that a buy order can be completed for the desired trade quantity when borrowing funds</returns>
        public static decimal HighestAffordableBuyPrice(decimal funds, decimal marginPct, decimal tradeQty, decimal qtyOnHand) =>
            funds / (marginPct * tradeQty + marginPct * qtyOnHand - qtyOnHand);

        /// <summary>
        /// The highest market price that we can afford to short the desired trade quantity given the initial margin requirements
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The initial margin percentage</param>
        /// <param name="tradeQty">The amount of stock that will be traded on the next order</param>
        /// <param name="qtyOnHand">The amount of stock on hand at this moment</param>
        /// <returns>The highest market price that we can afford to short the desired trade quantity given the initial margin requirements</returns>
        public static decimal HighestAffordableSellPrice(decimal funds, decimal marginPct, decimal tradeQty, decimal qtyOnHand) =>
            -funds * (marginPct - 1) / (marginPct * tradeQty - qtyOnHand);

        /// <summary>
        /// The highest market price that we can hold the position at, above which a margin call will be made
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The maintenance margin percentage</param>
        /// <param name="quantity">The amount of stock to be traded on the next order</param>
        /// <returns>The highest market price above which a margin call will be made</returns>
        public static decimal HighestMaintainablePrice(decimal funds, decimal marginPct, decimal quantity) =>
            funds * (marginPct - 1) / quantity;

        /// <summary>
        /// The lowest possible market price we can hold the position at, below which a margin call will be made
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The maintenance margin percentage</param>
        /// <param name="quantity">The amount of stock to be traded on the next order</param>
        /// <returns>The lowest possible market price we can hold the position at, below which a margin call will be made</returns>
        public static decimal LowestMaintainablePrice(decimal funds, decimal marginPct, decimal quantity) =>
            funds / ((marginPct - 1) * quantity);

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

            // This is the cost of a trade right now 
            var costOfTrade = marketPrice * contractState.TradeQty;

            // If we can't afford to complete a buy order now then we need to borrow funds
            if (contractState.Funding < costOfTrade)
            {
                // The initial long price is the highest market price that a buy order can be completed
                // for the desired trade quantity when borrowing funds
                var highestAffordableBuyPrice = HighestAffordableBuyPrice(contractState.Funding, Contract.InitialMargin,
                    contractState.TradeQty, contractState.QuantityOnHand);

                // If the lower bound is higher than the highest affordable market price then we need to
                // reset the lower bound and include trailing offset so that a buy can always be afforded
                if (highestAffordableBuyPrice < lowerBound) lowerBound = highestAffordableBuyPrice - contractState.TrailingOffset;
                if (lowerBound < 0) lowerBound = 0;
            }

            // If we don't have enough stock on hand to complete a sale order we need to short
            if (contractState.QuantityOnHand < contractState.TradeQty)
            {
                // The highest affordable market price is the highest market price that
                // we can afford to short the desired trade quantity given the initial margin requirements
                maxSellPrice = HighestAffordableSellPrice(contractState.Funding, Contract.InitialMargin, contractState.TradeQty, contractState.QuantityOnHand);

                // If the market price is over what we can afford to sell at we need to
                // place a max sell market order and exit
                if (marketPrice > maxSellPrice)
                {
                    await _contractManager.PlaceMaxSellOrder(new MarketOrder
                    {
                        Symbol = symbol,
                        Quantity = contractState.TradeQty,
                        PricePerUnit = maxSellPrice
                    });
                    return;
                }

            }

            // If we are borrowing funds and the market price drops below
            // the maintenance requirements we will get liquidated via
            // a margin call. To protect the portfolio we sell some of the
            // stock before a margin call. To reduce the loss we sell in
            // small batches instead of a large amount in the hopes of
            // buying time for a recovery
            if (contractState.Funding < 0)
            {
                // This is the safety strategy to avoid a margin call. To avoid a complete loss
                // we rather sell in smaller units so as to buy more time for the market
                // to recover instead of realizing a complete loss when the margin price is hit
                var batchQty = Math.Abs(contractState.QuantityOnHand) / contractState.SafetyBands;
                var funds = contractState.Funding;
                var quantity = contractState.QuantityOnHand;

                for (var i = 0; i < contractState.SafetyBands; i++)
                {
                    // The lowest maintainable price is the lowest market price below which
                    // a margin call will be made
                    var price = LowestMaintainablePrice(funds, Contract.MaintenanceMargin, quantity);

                    // We need to include the margin safety to execute earlier to avoid a margin call
                    price += contractState.MarginSafety;

                    // Placing a stop order ahead of time
                    await _contractManager.PlaceEmergencySellOrder(new MarketOrder
                    {
                        PricePerUnit = price,
                        Quantity = batchQty,
                        Symbol = symbol,
                    });

                    quantity -= batchQty;
                    funds += batchQty * price;
                }

            }

            // If we are shorting stock and the market price rises above
            // the maintenance requirements we will get liquidated via
            // a margin call. To protect the portfolio we buy some of the
            // stock before a margin call. To reduce the loss we buy in
            // small batches instead of a large amount in the hopes of
            // buying time for a recovery
            if (contractState.QuantityOnHand < 0)
            {
                // This is the safety strategy to avoid a margin call. To avoid a complete loss
                // we rather buy in smaller units so as to buy more time for the market
                // to recover instead of realizing a complete loss when the margin price is hit
                var batchQty = Math.Abs(contractState.QuantityOnHand) / contractState.SafetyBands;
                var funds = contractState.Funding;
                var quantity = contractState.QuantityOnHand;

                for (var i = 0; i < contractState.SafetyBands; i++)
                {
                    // The highest maintainable price is the highest market price above which
                    // a margin call will be made
                    var price = HighestMaintainablePrice(funds, Contract.MaintenanceMargin, quantity);

                    // We need to include the margin safety to execute earlier to avoid a margin call
                    price -= contractState.MarginSafety;

                    // Placing a stop order ahead of time
                    await _contractManager.PlaceEmergencySellOrder(new MarketOrder
                    {
                        PricePerUnit = price,
                        Quantity = batchQty,
                        Symbol = symbol,
                    });

                    quantity += batchQty;
                    funds -= batchQty * price;
                }

            }

            if (contractState.QuantityOnHand >= 0 && contractState.Funding >= 0)
            {
                // If we are not trading on margin we should cleanup any emergency stop orders
                // since we are not likely to get a margin call
                await _contractManager.ClearEmergencyOrders(symbol);
            }

            if (marketPrice > upperBound)
            {
                // If the market price is higher than the upper limit the contract run state goes into a sell run
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

            if (marketPrice < lowerBound)
            {
                // If the market price is lower than the lower limit the contract run state goes into a buy run
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

            // Create the triggers that will trigger the contract run state to go into
            // a sell run if the upper limit is hit, a buy run when the lower limit is hit
            // and a sell capped state if the max sell price is hit
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