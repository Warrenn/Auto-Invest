using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IStrategy
    {
        public Task<ContractState> GetContractState(string conId);
        public Task UpdateContractState(ContractState newState);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string conId);
        public Task PlaceBuyStopOrder(StopOrder conId);
        public Task PlaceSellStopOrder(StopOrder conId);
    }
}