using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class SellMarketOrder : MarketOrder
    {
        public decimal MaxSellPrice { get; set; }
    }
}
