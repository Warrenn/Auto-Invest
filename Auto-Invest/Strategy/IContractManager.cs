using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IContractManager
    {
        public Task<Contract> GetContractState(string conId);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string conId);
        public Task PlaceBuyStopOrder(MarketOrder order);
        public Task PlaceSellStopOrder(MarketOrder order);
        //public Task UpdatePosition(TickPosition position);
        //public Task PlaceBuyOrder(MarketOrder order);
        //public Task PlaceSellOrder(MarketOrder order);
    }
}