﻿namespace Auto_Invest.Strategy
{
    public class TriggerDetails
    {
        public string Symbol { get; set; }
        public decimal UpperLimit { get; set; }
        public decimal LowerLimit { get; set; }
        public decimal MaxSellPrice { get; set; }
    }
}