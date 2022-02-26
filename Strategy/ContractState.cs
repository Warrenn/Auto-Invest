namespace Auto_Invest.Strategy
{
    public struct ContractState
    {
        public RunState RunState { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal UpperBound { get; set; }
        public decimal LowerBound { get; set; }
        public decimal Offset { get; set; }
        public decimal SellLimit { get; set; }
        public decimal BuyLimit { get; set; }
    }
}