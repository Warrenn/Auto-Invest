using System.Net.WebSockets;
using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public interface IMediator
    {
        Task<ClientWebSocket> GetWebSocketAsync();
        Task<ChannelReader<CompletedOrder>> CompletedOrderReaderAsync();
        Task<IEnumerable<ContractExtended>> GetContractsAsync();

        void RegisterCompletedOrderChannel(Channel<CompletedOrder> channel);
        void RegisterTickPositionChannel(Channel<TickPosition> channel);
        void RegisterContracts(IEnumerable<ContractExtended> extendedList);
    }
}
