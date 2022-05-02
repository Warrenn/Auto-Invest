using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public interface IMediator
    {
        Task<ClientWebSocket> GetWebSocketAsync();
        Task<ChannelReader<CompletedOrder>> CompletedOrderReaderAsync();

        void RegisterCompletedOrderChannel(Channel<CompletedOrder> channel);
        void RegisterTickPositionChannel(Channel<TickPosition> channel);
        Task<IEnumerable<ContractExtended>> GetContractsAsync();
        Task<string> GetAccountIdAsync();
        void RegisterContracts(IEnumerable<ContractExtended> extendedList);
    }
}
