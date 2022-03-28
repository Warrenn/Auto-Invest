using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IContractClient
    {
        Task CancelOrder(int orderId);
        Task<ContractResult> PlaceStopLimit(StopLimit stopLimit);
        void ListenForCompletion(string symbol, IOrderCompletion orderCompletion);
        Task<decimal> GetMarketPrice(string symbol);
    }
}
