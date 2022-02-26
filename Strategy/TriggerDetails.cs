namespace Auto_Invest.Strategy
{
    public struct TriggerDetails
    {
        public string ConId { get; set; }
        public decimal UpperLimit { get; set; }
        public decimal LowerLimit { get; set; }
    }
}