namespace Auto_Invest.Strategy
{
    public class StopOrder
    {
        public string ConId { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal Quantity { get; set; }
    }
}