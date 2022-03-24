using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IOrderFilledProcess
    {
        Task OrderFilled(MarketOrder order);
    }
}