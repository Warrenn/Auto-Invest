using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public interface IMediator
    {
        Task<ChannelReader<CompletedOrder>> GetCompletedOrderReaderAsync();
        Task<ChannelReader<TickPosition>> GetTickPositionReaderAsync();
        Task<IEnumerable<ChannelReader<Contract>>> GetContractChangesReaderAsync();
        Task<IEnumerable<ContractExtended>> GetContractsAsync();
        Task<IDictionary<string, IOrderCompletion>> GetCompletionCallbacksAsync();
        Task<IDictionary<string, IRecordTick>> GetContractStrategiesAsync();

        void RegisterCompletedOrderReader(ChannelReader<CompletedOrder> channel);
        void RegisterTickPositionReader(ChannelReader<TickPosition> channel);
        void RegisterContracts(IEnumerable<ContractExtended> extendedList);
        void RegisterContractChanges(IEnumerable<ChannelReader<Contract>> contractChanges);
        void RegisterStrategies(IDictionary<string, IRecordTick> strategies);
        void RegisterCompletionCallbacks(IDictionary<string, IOrderCompletion> clientCompletion);
    }
}
