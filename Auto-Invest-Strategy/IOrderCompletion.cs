using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IOrderCompletion
    {
        Task OrderCompleted(CompletedOrder order);
    }
}