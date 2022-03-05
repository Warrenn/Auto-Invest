using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public interface IContractManager
    {
        public Task<Contract> GetContractState(string conId);
        public Task CreateTrigger(TriggerDetails details);
        public Task<decimal> GetContractsAverageValue(string conId);
        public Task PlaceBuyStopOrder(StopOrder order);
        public Task PlaceSellStopOrder(StopOrder order);
        public void InitializeContract(TickPosition tick);
    }
}