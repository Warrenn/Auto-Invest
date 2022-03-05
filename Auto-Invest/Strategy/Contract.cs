using System;

namespace Auto_Invest.Strategy
{
    public class Contract
    {
        public Contract(
            string conId,
            decimal funding,
            decimal offset,
            decimal triggerMargin,
            decimal fundingRisk = 1,
            decimal buyBaseLine = 1,
            decimal buyMagnification = 1,
            decimal sellMagnification = 1,
            decimal debtCeiling = -1,
            decimal debtRisk = -1)
        {
            if (string.IsNullOrWhiteSpace(conId)) throw new ArgumentNullException(nameof(conId));
            if (funding == 0) throw new ArgumentException($"{nameof(funding)} cannot be 0", nameof(funding));
            if (sellMagnification == 0) throw new ArgumentException($"{nameof(sellMagnification)} cannot be 0", nameof(sellMagnification));
            if (buyMagnification == 0) throw new ArgumentException($"{nameof(buyMagnification)} cannot be 0", nameof(buyMagnification));
            if (triggerMargin == 0) throw new ArgumentException($"{nameof(triggerMargin)} cannot be 0", nameof(triggerMargin));
            if (offset == 0) throw new ArgumentException($"{nameof(offset)} cannot be 0", nameof(offset));

            ConId = conId.ToUpper();
            Funding = Math.Abs(funding);
            Offset = Math.Abs(offset % 1);
            FundingRisk = Math.Abs(fundingRisk % 1);
            BuyBaseLine = Math.Abs(buyBaseLine % 1);
            BuyMagnification = Math.Abs(buyMagnification);
            SellMagnification = Math.Abs(sellMagnification);
            TriggerMargin = Math.Abs(triggerMargin % 1);
            DebtRisk = Math.Abs(debtRisk % 1);
            DebtCeiling = Math.Abs(debtCeiling);

            if (FundingRisk == 0)
                FundingRisk = 1;
            if (BuyBaseLine == 0)
                BuyBaseLine = 1;
            if (TriggerMargin == 0)
                TriggerMargin = 1;
            if (DebtRisk == 0)
                DebtRisk = 1;
            if (debtCeiling < 0)
                DebtCeiling = Funding;
            if (debtRisk < 0)
                DebtRisk = 1;
        }
        /// <summary>
        /// This is the stock symbol it is refereed to as contract ID in IBKR
        /// </summary>
        public string ConId { get; }

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
        public decimal Offset { get; }

        /// <summary>
        /// The price limit of the market that will trigger a sell order. Determined by using the offset against the market price.
        /// </summary>
        public decimal SellOrderLimit { get; private set; }

        /// <summary>
        /// The price limit of the market that will trigger a buy order. Determined by using the offset against the market price.
        /// </summary>
        public decimal BuyOrderLimit { get; private set; }

        /// <summary>
        /// The amount of stock to sell when the sell order is triggered.
        /// </summary>
        public decimal SellQty { get; private set; }

        /// <summary>
        /// The amount of stock to buy when the sell order is triggered
        /// </summary>
        public decimal BuyQty { get; private set; }

        /// <summary>
        /// The fraction of how much of the funding to risk when making a buy order
        /// </summary>
        public decimal FundingRisk { get; }

        /// <summary>
        /// The fraction of the average price considered as 100% under value when making a buy order
        /// </summary>
        public decimal BuyBaseLine { get; }

        /// <summary>
        /// The tracking numbers of the orders placed for buy orders
        /// </summary>
        public int BuyOrderId { get; private set; }

        /// <summary>
        /// The tracking numbers of the orders placed for sell orders
        /// </summary>
        public int SellOrderId { get; private set; }

        /// <summary>
        /// A multiplier to exaggerate the quantity to buy when placing a buy order 
        /// </summary>
        public decimal BuyMagnification { get; }

        /// <summary>
        /// A multiplier to exaggerate the quantity to sell when placing a sell order
        /// </summary>
        public decimal SellMagnification { get; }

        /// <summary>
        /// The absolute hard limit of currency exposure when determining the quantity of a sell or buy order
        /// </summary>
        public decimal DebtCeiling { get; }

        /// <summary>
        /// The ratio of acceptable debt available to DebtCeiling when shorting stocks
        /// </summary>
        public decimal DebtRisk { get; }

        /// <summary>
        /// The fraction of the average value that determines the upper bound and lower bound of the trigger limits.
        /// </summary>
        public decimal TriggerMargin { get; }

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
            public void SetSellQty(decimal newValue) => _state.SellQty = newValue;
            public void SetBuyQty(decimal newValue) => _state.BuyQty = newValue;
            public void SetBuyOrderId(int newValue) => _state.BuyOrderId = newValue;
            public void SetSellOrderId(int newValue) => _state.SellOrderId = newValue;
        }
    }
}