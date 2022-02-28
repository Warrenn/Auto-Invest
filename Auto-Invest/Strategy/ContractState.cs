namespace Auto_Invest.Strategy
{
    public struct ContractState
    {
        public string ConId { get; set; }
        public RunState RunState { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Funding { get; set; }
        public decimal UpperBound { get; set; }
        public decimal LowerBound { get; set; }
        public decimal Offset { get; set; }
        public decimal SellLimit { get; set; }
        public decimal BuyLimit { get; set; }
        public decimal SellQty { get; set; }
        public decimal BuyQty { get; set; }
    }
}