namespace Auto_Invest.Strategy
{
    public struct TriggerDetails
    {
        public string ConId { get; set; }
        public decimal UpperLimit { get; set; }
        public decimal LowerLimit { get; set; }

        public TriggerDetails(string conId)
        {
            ConId = conId;
            UpperLimit = 0;
            LowerLimit = 0;
        }
    }
}