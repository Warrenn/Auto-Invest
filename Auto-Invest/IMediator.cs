using System.Net.WebSockets;
using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public interface IMediator
    {
        Task<ChannelReader<CompletedOrder>> GetCompletedOrderReaderAsync();
        Task<IEnumerable<ContractExtended>> GetContractsAsync();
        Task<IDictionary<string, IOrderCompletion>> GetCompletionCallbacksAsync();
        Task<IDictionary<string, IRecordTick>> GetContractStrategiesAsync();
        Task<ChannelReader<TickPosition>> GetTickPositionReaderAsync();

        void RegisterCompletedOrderChannel(Channel<CompletedOrder> channel);
        void RegisterTickPositionChannel(Channel<TickPosition> channel);
        void RegisterContracts(IEnumerable<ContractExtended> extendedList);
    }
}
