using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IContractManager
    {
        public Task<Contract> GetContractState(string symbol);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string symbol);
        public Task PlaceTrailingBuyOrder(MarketOrder order);
        public Task PlaceTrailingSellOrder(SellMarketOrder order);
        public Task PlaceEmergencySellOrder(MarketOrder order);
        public Task PlaceEmergencyBuyOrder(MarketOrder order);
        public Task PlaceMaxSellOrder(MarketOrder order);
        Task ClearEmergencyOrders(string symbol);
    }
}