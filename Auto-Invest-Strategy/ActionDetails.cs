namespace Auto_Invest_Strategy
{
    public class ActionDetails
    {
        public int OrderId { get; set; }
        public string Symbol { get; set; }
        public decimal Qty { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal CostOfOrder { get; set; }
        public decimal Commission { get; set; }
    }
}
