using System.Collections.Generic;

namespace Auto_Invest.Strategy
{
    public class ContractState
    {
        public string ConId { get; set; }
        public RunState RunState { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Quantity { get; set; }
        public decimal Funding { get; set; }
        public decimal UpperBound { get; set; }
        public decimal LowerBound { get; set; }
        public decimal Offset { get; set; }
        public decimal SellLimit { get; set; }
        public decimal BuyLimit { get; set; }
        public decimal SellQty { get; set; }
        public decimal BuyQty { get; set; }
        public decimal FundingRisk { get; set; }
        public decimal BuyBaseLine { get; set; }
        public decimal ShortFund { get; set; }
        public List<int> BuyOrderIds { get; set; } = new List<int>();
        public List<int> SellOrderIds { get; set; } = new List<int>();
    }
}