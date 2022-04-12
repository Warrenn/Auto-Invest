using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auto_Invest_Test
{
    public class TickFileData
    {
        public string ticker { get; set; }
        public int queryCount { get; set; }
        public int resultsCount { get; set; }
        public bool adjusted { get; set; }
        public string status { get; set; }
        public string request_id { get; set; }
        public int count { get; set; }
        public TickData[] results { get; set; }
    }

    public class TickData
    {
        public decimal v { get; set; }
        public decimal vw { get; set; }
        public decimal o { get; set; }
        public decimal c { get; set; }
        public decimal h { get; set; }
        public decimal l { get; set; }
        public decimal t { get; set; }
        public ulong n { get; set; }
    }
}
