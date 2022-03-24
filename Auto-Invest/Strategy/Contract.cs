using System;

namespace Auto_Invest.Strategy
{
    public class Contract
    {
        public const decimal InitialMargin = 0.5M;
        public const decimal MaintenanceMargin = 0.3M;

        public Contract(
            string symbol,
            decimal funding,
            decimal tradeQuantity,
            decimal trailingOffset,
            uint safetyLayers = 10,
            decimal initialQuantity = 0,
            decimal marginRisk = 0)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
            if (funding == 0 && initialQuantity == 0) throw new ArgumentException($"{nameof(funding)} and {nameof(initialQuantity)} cannot both be 0", nameof(funding));
            if (trailingOffset == 0) throw new ArgumentException($"{nameof(trailingOffset)} cannot be 0", nameof(trailingOffset));

            Symbol = symbol.ToUpper();
            Funding = funding;
            SafetyLayers = safetyLayers;
            Quantity = initialQuantity;
            TrailingOffset = trailingOffset;
            MarginRisk = Math.Abs(marginRisk);
            TradeQty = Math.Abs(tradeQuantity);

            if (TrailingOffset <= 0) TrailingOffset = 1;
            if (MarginRisk <= 0) MarginRisk = TrailingOffset;
            if (SafetyLayers == 0) SafetyLayers = 1;
        }

        /// <summary>
        /// This is the stock symbol it is refereed to as contract ID in IBKR
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// What is the current running streak of the contract is it waiting to hit a trigger or are we trailing a buy or trailing a sell
        /// </summary>
        public RunState RunState { get; private set; }

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
        public decimal Quantity { get; private set; }

        /// <summary>
        /// The amount of liquid cash held in the account held for the contract
        /// </summary>
        public decimal Funding { get; private set; }

        public uint SafetyLayers { get; }

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
        public decimal SellOrderLimit { get; private set; }

        /// <summary>
        /// The price limit of the market that will trigger a buy order. Determined by using the offset against the market price.
        /// </summary>
        public decimal BuyOrderLimit { get; private set; }

        /// <summary>
        /// The amount of stock to sell or buy when the sell or buy order is triggered.
        /// </summary>
        public decimal TradeQty { get; }

        /// <summary>
        /// The tracking numbers of the orders placed for buy orders
        /// </summary>
        public int BuyOrderId { get; private set; }

        /// <summary>
        /// The tracking numbers of the orders placed for sell orders
        /// </summary>
        public int SellOrderId { get; private set; }

        /// <summary>
        /// The absolute hard limit of currency exposure when determining the quantity of a sell or buy order
        /// </summary>
        public decimal MarginRisk { get; }


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
            public void SetQuantity(decimal newValue) => _state.Quantity = newValue;
            public void SetFunding(decimal newValue) => _state.Funding = newValue;
            public void SetUpperBound(decimal newValue) => _state.UpperBound = newValue;
            public void SetLowerBound(decimal newValue) => _state.LowerBound = newValue;
            public void SetSellLimit(decimal newValue) => _state.SellOrderLimit = newValue;
            public void SetBuyLimit(decimal newValue) => _state.BuyOrderLimit = newValue;
            public void SetBuyOrderId(int newValue) => _state.BuyOrderId = newValue;
            public void SetSellOrderId(int newValue) => _state.SellOrderId = newValue;
        }
    }
}