using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IRecordTick
    {
        Task Tick(TickPosition tick);
    }
}
