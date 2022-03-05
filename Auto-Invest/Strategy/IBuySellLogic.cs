using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IBuySellLogic
    {
        Task BuyActionComplete(ActionDetails details);
        Task SellActionComplete(ActionDetails details);
    }
}
