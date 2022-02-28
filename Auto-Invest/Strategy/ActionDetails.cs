﻿namespace Auto_Invest.Strategy
{
    public class ActionDetails
    {
        public string ConId { get; set; }
        public decimal Qty { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal CostOfOrder { get; set; }
    }
}