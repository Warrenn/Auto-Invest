namespace Auto_Invest.Strategy
{
    public class StopLimit
    {
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal StopPrice { get; set; }
    }
}