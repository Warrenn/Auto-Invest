using System;
using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public class TrailingBuySellStrategy : IOrderFilledProcess, IRecordTick
    {
        private readonly IContractManager _contractManager;
        private readonly IBuySellLogic _buySellLogic;
        private readonly MovingAverage _movingAverage;

        private static decimal LowerLimit(decimal baseAmount, decimal offset)
            => baseAmount - offset;

        private static decimal UpperLimit(decimal baseAmount, decimal offset)
            => baseAmount + offset;

        public static Func<decimal, decimal, Contract, decimal> SimulateCommission { get; set; } =
            (size, _, _) => size * 0.02M;

        public static int MovingAverageSize { get; set; } = 20;

        public TrailingBuySellStrategy(IContractManager contractManager, IBuySellLogic buySellLogic)
        {
            _contractManager = contractManager;
            _buySellLogic = buySellLogic;
            contractManager.RegisterForOrderFilled(this);
            _movingAverage = new MovingAverage(MovingAverageSize);
        }

        /// <summary>
        /// Workout the purchase power amount using the given margin requirements, less whatever has already been borrowed
        /// </summary>
        /// <param name="funds">Amount of funds on hand either owed or as cash</param>
        /// <param name="qtyOnHand">Amount of stock on hand at this moment or amount currently shorted</param>
        /// <param name="pricePerStock">Current price per unit of stock at this moment</param>
        /// <param name="marginPct">Required margin percentage</param>
        /// <returns>How much funds are still accessible at this moment</returns>
        public static decimal FundsAvailable(
            decimal funds,
            decimal qtyOnHand,
            decimal pricePerStock,
            decimal marginPct)
            => (1 - marginPct) * (qtyOnHand * pricePerStock + funds) / marginPct + funds;

        /// <summary>
        /// Workout the value of stocks that can be sold given the margin requirements,
        /// less the amount of stocks already shorted
        /// </summary>
        /// <param name="funds">Amount of funds on hand either owed or as cash</param>
        /// <param name="qtyOnHand">Amount of stock on hand at this moment or amount currently shorted</param>
        /// <param name="pricePerStock">Current price per unit of stock at this moment</param>
        /// <param name="marginPct">Required margin percentage</param>
        /// <returns>Stock value that can be sold at this moment</returns>
        public static decimal StockValueAvailable(
            decimal funds,
            decimal qtyOnHand,
            decimal pricePerStock,
            decimal marginPct)
            => (qtyOnHand * pricePerStock + funds) / marginPct + (qtyOnHand < 0 ? qtyOnHand * pricePerStock : 0);

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

        public Contract SimulateBuy(Contract contract, decimal tradeSize, decimal price)
        {
            var simulator = new Simulator(contract);
            var simulatedContract = simulator.Contract;
            var action = new ActionDetails
            {
                Commission = SimulateCommission(tradeSize, price, contract),
                CostOfOrder = tradeSize * price,
                PricePerUnit = price,
                Qty = tradeSize,
                Symbol = contract.Symbol
            };

            _buySellLogic.BuyComplete(action, simulatedContract, simulator.Editor);

            return simulatedContract;
        }

        public Contract SimulateSell(Contract contract, decimal tradeSize, decimal price)
        {
            var simulator = new Simulator(contract);
            var simulatedContract = simulator.Contract;
            var action = new ActionDetails
            {
                Commission = SimulateCommission(tradeSize, price, contract),
                CostOfOrder = tradeSize * price,
                PricePerUnit = price,
                Qty = tradeSize,
                Symbol = contract.Symbol
            };

            _buySellLogic.SellComplete(action, simulatedContract, simulator.Editor);

            return simulatedContract;
        }

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
                await PlaceOrder(limit, FundsAvailable, _contractManager.PlaceTrailingBuyOrder, SimulateBuy);
                return;
            }

            if (contractState.RunState == RunState.SellRun)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.SellOrderLimit) return;
                await PlaceOrder(limit, StockValueAvailable, _contractManager.PlaceTrailingSellOrder, SimulateSell);
                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position >= contractState.UpperBound)
            {
                var limit = LowerLimit(tick.Position, contractState.TrailingOffset);
                if (limit < contractState.AveragePrice) limit = contractState.AveragePrice;
                await PlaceOrder(limit, StockValueAvailable, _contractManager.PlaceTrailingSellOrder, SimulateSell);
                return;
            }

            if (contractState.RunState == RunState.TriggerRun &&
                tick.Position <= contractState.LowerBound)
            {
                var limit = UpperLimit(tick.Position, contractState.TrailingOffset);
                if (limit > contractState.AveragePrice) limit = contractState.AveragePrice;
                await PlaceOrder(limit, FundsAvailable, _contractManager.PlaceTrailingBuyOrder, SimulateBuy);
            }

            async Task PlaceOrder(
                decimal limit,
                Func<decimal, decimal, decimal, decimal, decimal> calculateAvailableValue,
                Func<MarketOrder, Task> placeOrder,
                Func<Contract, decimal, decimal, Contract> simulateTrade)
            {
                var availableValue = calculateAvailableValue(contractState.Funding, contractState.QuantityOnHand, limit,
                    Contract.InitialMargin);

                if (availableValue <= 0) return;

                var tradeSize = (availableValue * contractState.TradePercent) / limit;

                var equityBefore = contractState.AveragePrice * contractState.QuantityOnHand + contractState.Funding;
                var simulatedContract = simulateTrade(contractState, tradeSize, limit);
                var equityAfter = simulatedContract.AveragePrice * simulatedContract.QuantityOnHand +
                                  simulatedContract.Funding;

                var tradeProfit = (equityAfter - equityBefore) / equityBefore;

                if (tradeProfit < contractState.ProfitPercentage) return;

                await placeOrder(new MarketOrder
                {
                    Quantity = tradeSize,
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
                await _contractManager.ClearEmergencyOrders(symbol);

                for (var i = 0; i < contractState.SafetyBands; i++)
                {
                    // The lowest maintainable price is the lowest market price below which
                    // a margin call will be made
                    var price = LowestMaintainablePrice(funds, Contract.MaintenanceMargin, quantity);

                    // We need to include the margin safety so that it the order executes earlier to avoid a margin call
                    price += contractState.MarginProtection;

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
                await _contractManager.ClearEmergencyOrders(symbol);

                for (var i = 0; i < contractState.SafetyBands; i++)
                {
                    // The highest maintainable price is the highest market price above which
                    // a margin call will be made
                    var price = HighestMaintainablePrice(funds, Contract.MaintenanceMargin, quantity);

                    // We need to include the margin safety in order to execute earlier to avoid a margin call
                    price -= contractState.MarginProtection;

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