namespace Auto_Invest_Strategy
{
    public enum RunState
    {
        /// <summary>
        /// The contract market value is between
        /// the upper bound and the lower bound
        /// trigger values
        /// </summary>
        TriggerRun,
        /// <summary>
        /// The contract market value is going up and we are
        /// doing a Trailing Sell Stop Order
        /// </summary>
        SellRun,
        /// <summary>
        /// The contract market value is going down and we
        /// are doing a Trailing Buy Stop Order
        /// </summary>
        BuyRun
    }
}