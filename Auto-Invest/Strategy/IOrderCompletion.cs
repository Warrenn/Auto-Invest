using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IOrderCompletion
    {
        Task OrderCompleted(CompletedOrder order);
    }
}