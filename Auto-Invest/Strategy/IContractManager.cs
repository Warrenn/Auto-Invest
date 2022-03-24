using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IContractManager
    {
        public Task<Contract> GetContractState(string symbol);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string symbol);
        public Task PlaceBuyStopOrder(MarketOrder order);
        public Task PlaceSellStopOrder(MarketOrder order);
    }
}