using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IContractService
    {
        public Task<ContractState> GetContractState(string conId);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string conId);
        public Task PlaceBuyStopOrder(StopOrder order);
        public Task PlaceSellStopOrder(StopOrder order);
    }
}