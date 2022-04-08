using System;
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
        /// Workout if there is still liquidity after a sale of stock given the initial margin requirements and
        /// what the portfolio would be worth at the given price point after the sale
        /// </summary>
        /// <param name="funds">Amount of funds on hand at this moment</param>
        /// <param name="qtyOnHand">Amount of stock on hand at this moment</param>
        /// <param name="pricePerStock">Price per stock at which the sale will be executed</param>
        /// <param name="tradeQty">Amount of stock traded in the execution of the sale</param>
        /// <param name="marginPct">Initial margin percentage</param>
        /// <returns>Amount still available to be borrowed after the execution of the sale</returns>
        public static decimal LiquidityAfterSale(
            decimal funds,
            decimal qtyOnHand,
            decimal pricePerStock,
            decimal tradeQty,
            decimal marginPct)
        {
            var quantityAfterSale = qtyOnHand - tradeQty;
            var assetValueAfterSale = pricePerStock * quantityAfterSale;
            var fundsFromSale = tradeQty * pricePerStock;
            var equityAfterSale = assetValueAfterSale + fundsFromSale + funds;
            var purchasePowerAfterSale = equityAfterSale / marginPct;
            var borrowableAfterSale = (1 - marginPct) * purchasePowerAfterSale;
            var leftToStillBorrow = borrowableAfterSale + assetValueAfterSale;
            return leftToStillBorrow;
        }

        /// <summary>
        /// Workout if there is still liquidity after the purchasing of stock given the initial margin requirements and
        /// what the portfolio would be worth at the given price point after the purchase
        /// </summary>
        /// <param name="funds">Amount of funds on hand at this moment</param>
        /// <param name="qtyOnHand">Amount of stock on hand at this moment</param>
        /// <param name="pricePerStock">Price per stock at which the buy order will be executed</param>
        /// <param name="tradeQty">Amount of stock purchased in the execution of the order</param>
        /// <param name="marginPct">Initial margin percentage</param>
        /// <returns>Amount still available to be borrowed after the execution of the buy order</returns>
        public static decimal LiquidityAfterBuy(
        decimal funds,
            decimal qtyOnHand,
            decimal pricePerStock,
            decimal tradeQty,
            decimal marginPct)
        {
            var quantityAfterBuy = qtyOnHand + tradeQty;
            var assetValueAfterBuy = pricePerStock * quantityAfterBuy;
            var purchaseCost = tradeQty * pricePerStock;
            var fundsAfterBuy = funds - purchaseCost;
            var equityAfterBuy = assetValueAfterBuy + fundsAfterBuy;
            var purchasePowerAfterBuy = equityAfterBuy / marginPct;
            var borrowableAfterBuy = (1 - marginPct) * purchasePowerAfterBuy;
            var leftToStillBorrow = borrowableAfterBuy + fundsAfterBuy;
            return leftToStillBorrow;
        }

        /// <summary>
        /// Determine if a buy order is still possible at the given price and current contract position
        /// </summary>
        /// <param name="price">Price to purchase stock at</param>
        /// <param name="contract">Portfolio's position and contract info</param>
        /// <returns>true if the purchase is possible otherwise false</returns>
        public static bool PurchasePossible(decimal price, Contract contract)
        {
            var purchaseCost = price * contract.TradeQty;
            if (contract.Funding > purchaseCost) return true;
            var fundsAfterPurchase = LiquidityAfterBuy(contract.Funding, contract.QuantityOnHand, price,
                contract.TradeQty, Contract.InitialMargin);
            return fundsAfterPurchase > 0;
        }

        /// <summary>
        /// Determine if a sale order is still possible at the given price and current contract position
        /// </summary>
        /// <param name="price">Price to sell stock at</param>
        /// <param name="contract">Portfolio's position and contract info</param>
        /// <returns>true if the purchase is possible otherwise false</returns>
        public static bool SalePossible(decimal price, Contract contract)
        {
            if (contract.QuantityOnHand > contract.TradeQty) return true;
            var fundsAfterSale = LiquidityAfterSale(contract.Funding, contract.QuantityOnHand, price, contract.TradeQty,
                Contract.InitialMargin);
            return fundsAfterSale > 0;
        }

        /// <summary>
        /// The highest market price that we can hold the position at, above which a margin call will be made.
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The maintenance margin percentage</param>
        /// <param name="quantity">The amount of stock on hand at this moment</param>
        /// <returns>The highest market price above which a margin call will be made</returns>
        public static decimal HighestMaintainablePrice(decimal funds, decimal marginPct, decimal quantity) =>
            funds * (marginPct - 1) / quantity;

        /// <summary>
        /// The lowest possible market price we can hold the position at, below which a margin call will be made
        /// </summary>
        /// <param name="funds">The amount of funds on hand at this moment</param>
        /// <param name="marginPct">The maintenance margin percentage</param>
        /// <param name="quantity">The amount of stock on hand at this moment</param>
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

            if (contractState.RunState == RunState.BuyRun)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.BuyOrderLimit) return;
                if (!PurchasePossible(limit, contractState)) return;

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
                if (!SalePossible(limit, contractState)) return;

                await _contractManager.PlaceTrailingSellOrder(new MarketOrder
                {
                    Symbol = tick.Symbol,
                    Quantity = contractState.TradeQty,
                    PricePerUnit = limit
                });

                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position >= contractState.UpperBound)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;
                if (!SalePossible(limit, contractState)) return;

                await _contractManager.PlaceTrailingSellOrder(new MarketOrder
                {
                    Quantity = contractState.TradeQty,
                    Symbol = tick.Symbol,
                    PricePerUnit = limit
                });
                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position <= contractState.LowerBound)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;
                if (!PurchasePossible(limit, contractState)) return;

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

            // The upper bound trigger value for a sell run is the market price plus the trailing offset
            var upperBound = UpperLimit(average, contractState.TrailingOffset);

            // The lower bound for a buy run is the highest value below
            // average that an order can be completely fulled less the trailing offset
            var lowerBound = LowerLimit(average, contractState.TrailingOffset);

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

                    // We need to include the margin safety so that it the order executes earlier to avoid a margin call
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

                    // We need to include the margin safety in order to execute earlier to avoid a margin call
                    price -= contractState.MarginSafety;

                    // Placing a stop order ahead of time
                    await _contractManager.PlaceEmergencyBuyOrder(new MarketOrder
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

            // Create the triggers that will trigger the contract run state to go into
            // a sell run if the upper limit is hit, a buy run when the lower limit is hit
            // and a sell capped state if the max sell price is hit
            await _contractManager.CreateTrigger(new TriggerDetails
            {
                Symbol = symbol,
                UpperLimit = upperBound,
                LowerLimit = lowerBound
            });
        }
    }
}