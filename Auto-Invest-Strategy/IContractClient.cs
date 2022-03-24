using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public interface IContractClient
    {
        Task CancelOrder(int orderId);
        Task<ContractResult> UpdateStopLmtBuy(StopLimitUpdate stopLimitUpdate);
        Task<ContractResult> PlaceStopLmtBuy(StopLimit stopLimit);
        Task<ContractResult> UpdateStopLmtSell(StopLimitUpdate stopLimitUpdate);
        Task<ContractResult> PlaceStopLmtSell(StopLimit stopLimit);
        void ListenForCompletion(string symbol, IOrderCompletion orderCompletion);
        Task<decimal> GetMarketPrice(string symbol);
    }
}
