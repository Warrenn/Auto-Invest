using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class EmergencyOrderDetail
    {
        public decimal PricePerUnit { get; set; }
        public ActionSide Action { get; set; }
        public int OrderId { get; set; }
    }
}
