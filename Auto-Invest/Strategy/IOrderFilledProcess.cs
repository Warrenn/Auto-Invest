using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IOrderFilledProcess
    {
        Task OrderFilled(MarketOrder order);
    }
}