using System;
using System.Collections.Generic;
using System.Linq;

namespace Auto_Invest_Strategy
{
    public class Contract
    {
        public const decimal InitialMargin = 0.5M;
        public const decimal MaintenanceMargin = 0.3M;
        private IList<EmergencyOrderDetail> _emergencyOrders = new List<EmergencyOrderDetail>();

        public Contract(
            string symbol,
            decimal funding,
            decimal trailingOffset,
            decimal tradePercentage = 0.02M,
            decimal initialQuantity = 0,
            decimal averagePrice = 0,
            uint safetyBands = 10,
            decimal marginProtection = 0)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
            if (funding == 0 && initialQuantity == 0) throw new ArgumentException($"{nameof(funding)} and {nameof(initialQuantity)} cannot both be 0", nameof(funding));
            if (trailingOffset == 0) throw new ArgumentException($"{nameof(trailingOffset)} cannot be 0", nameof(trailingOffset));

            Symbol = symbol.ToUpper();
            Funding = funding;
            SafetyBands = safetyBands;
            QuantityOnHand = initialQuantity;
            TrailingOffset = trailingOffset;
            MarginProtection = Math.Abs(marginProtection);
            TradePercent = Math.Abs(tradePercentage % 1);
            AveragePrice = Math.Abs(averagePrice);

            if (TrailingOffset < 0) TrailingOffset = 0;
            if (MarginProtection < 0) MarginProtection = TrailingOffset;
            if (SafetyBands == 0) SafetyBands = 1;
            if (TradePercent == 0) TradePercent = 1;
            if (AveragePrice > 0 && QuantityOnHand > 0) TotalCost = QuantityOnHand * AveragePrice;

        }

        /// <summary>
        /// This is the stock symbol it is refereed to as contract ID in IBKR
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// What is the current running streak of the contract is it waiting to hit a trigger
        /// trailing a buy, trailing a sell or waiting to hit a sell limit
        /// </summary>
        public RunState RunState { get; private set; } = RunState.TriggerRun;

        /// <summary>
        /// The average price of the stock held on hand at this moment or the average price of the stock owed at this moment
        /// </summary>
        public decimal AveragePrice { get; private set; }

        /// <summary>
        /// The aggregated total cost of the stock on hand at this moment or the total aggregated cost of the stock that is owed
        /// </summary>
        public decimal TotalCost { get; private set; }

        /// <summary>
        /// The amount of stock that is on hand for this contract or the total amount of stock owed
        /// </summary>
        public decimal QuantityOnHand { get; private set; }

        /// <summary>
        /// The amount of liquid cash held in the account held for the contract
        /// </summary>
        public decimal Funding { get; private set; }

        /// <summary>
        /// To avoid a margin call a buy or sell stop order is placed ahead of the
        /// margin price. To avoid a complete loss the stop orders are place in
        /// smaller units. The SafetyBands is how many times to divide up the quantity
        /// of stock on hand to work out the size of the orders
        /// </summary>
        public uint SafetyBands { get; }

        /// <summary>
        /// The upper limit price for the trigger that when hit will put the contract into a sell run.
        /// </summary>
        public decimal UpperBound { get; private set; }

        /// <summary>
        /// The lower limit price for the trigger that when hit will put the contract into a buy run.
        /// </summary>
        public decimal LowerBound { get; private set; }

        /// <summary>
        /// The offset expressed as a fraction of market price. Used to determine the trailing stop
        /// buy price for a buy run or trailing stop sell price for a sell run
        /// </summary>
        public decimal TrailingOffset { get; }

        /// <summary>
        /// The price limit of the market that will trigger a sell order. Determined by using the offset against the market price.
        /// </summary>
        public decimal SellOrderLimit { get; private set; } = -1;

        /// <summary>
        /// The price limit of the market that will trigger a buy order. Determined by using the offset against the market price.
        /// </summary>
        public decimal BuyOrderLimit { get; private set; } = -1;

        /// <summary>
        /// The amount of stock to sell or buy when the sell or buy order is triggered.
        /// </summary>
        public decimal TradePercent { get; }

        /// <summary>
        /// The tracking number of the order placed for a buy order
        /// </summary>
        public int TrailingBuyOrderId { get; private set; } = -1;

        /// <summary>
        /// The tracking numbers of the order placed for sell order
        /// </summary>
        public int TrailingSellOrderId { get; private set; } = -1;

        /// <summary>
        /// The tracking numbers of the emergency orders
        /// </summary>
        public IEnumerable<EmergencyOrderDetail> EmergencyOrders => _emergencyOrders.AsEnumerable();

        /// <summary>
        /// The the safety amount to offset against a margin price to avoid a margin call
        /// </summary>
        public decimal MarginProtection { get; }

        public void RegisterEditor(IRegisterContractEditor register) => register.RegisterEditor(this, new ContractEditor(this));

        private class ContractEditor : IContractEditor
        {
            private readonly Contract _state;

            internal ContractEditor(Contract state)
            {
                _state = state;
            }

            public void SetRunState(RunState newState) => _state.RunState = newState;
            public void SetAveragePrice(decimal newValue) => _state.AveragePrice = newValue;
            public void SetTotalCost(decimal newValue) => _state.TotalCost = newValue;
            public void SetQuantity(decimal newValue) => _state.QuantityOnHand = newValue;
            public void SetFunding(decimal newValue) => _state.Funding = newValue;
            public void SetUpperBound(decimal newValue) => _state.UpperBound = newValue;
            public void SetLowerBound(decimal newValue) => _state.LowerBound = newValue;
            public void SetSellLimit(decimal newValue) => _state.SellOrderLimit = newValue;
            public void SetBuyLimit(decimal newValue) => _state.BuyOrderLimit = newValue;
            public void RemoveEmergencyOrderId(int orderId)
            {
                var order = _state.EmergencyOrders.First(_ => _.OrderId == orderId);
                ((IList<EmergencyOrderDetail>)_state.EmergencyOrders).Remove(order);
            }

            public void SetTrailingBuyOrderId(int newValue) => _state.TrailingBuyOrderId = newValue;
            public void SetTrailingSellOrderId(int newValue) => _state.TrailingSellOrderId = newValue;
            public void ResetEmergencyOrders() => _state._emergencyOrders = new List<EmergencyOrderDetail>();
            public void AddEmergencyOrder(EmergencyOrderDetail detail) => _state._emergencyOrders.Add(detail);
        }
    }
}