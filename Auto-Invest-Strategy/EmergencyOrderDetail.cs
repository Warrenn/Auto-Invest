namespace Auto_Invest_Strategy
{
    public class EmergencyOrderDetail
    {
        public decimal PricePerUnit { get; set; }
        public decimal Size { get; set; }
        public ActionSide Action { get; set; }
        public int OrderId { get; set; }
    }
}
