using System.Threading.Channels;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class AsyncMediator : IMediator
    {
        private readonly TaskCompletionSource<ChannelReader<CompletedOrder>> _completedOrderReaderSource =
            new();

        private readonly TaskCompletionSource<ChannelReader<TickPosition>> _tickPositionReader =
            new();

        private readonly TaskCompletionSource<IEnumerable<ChannelReader<Contract>>> _contractChangesReader =
            new();

        private readonly TaskCompletionSource<IEnumerable<ContractExtended>> _contracts =
            new();

        private readonly TaskCompletionSource<IDictionary<string, IOrderCompletion>> _completionCallbacks =
            new();

        private readonly TaskCompletionSource<IDictionary<string, IRecordTick>> _strategies =
            new();

        public Task<ChannelReader<CompletedOrder>> GetCompletedOrderReaderAsync() =>
            _completedOrderReaderSource.Task;

        public Task<ChannelReader<TickPosition>> GetTickPositionReaderAsync() =>
            _tickPositionReader.Task;

        public Task<IEnumerable<ChannelReader<Contract>>> GetContractChangesReaderAsync() =>
            _contractChangesReader.Task;

        public Task<IEnumerable<ContractExtended>> GetContractsAsync() =>
            _contracts.Task;

        public Task<IDictionary<string, IOrderCompletion>> GetCompletionCallbacksAsync() =>
            _completionCallbacks.Task;

        public Task<IDictionary<string, IRecordTick>> GetContractStrategiesAsync() =>
            _strategies.Task;

        public void RegisterCompletedOrderReader(ChannelReader<CompletedOrder> channelReader) =>
            _completedOrderReaderSource.SetResult(channelReader);

        public void RegisterTickPositionReader(ChannelReader<TickPosition> channelReader) =>
            _tickPositionReader.SetResult(channelReader);

        public void RegisterContracts(IEnumerable<ContractExtended> extendedList) =>
            _contracts.SetResult(extendedList);

        public void RegisterContractChanges(IEnumerable<ChannelReader<Contract>> contractChanges) =>
            _contractChangesReader.SetResult(contractChanges);

        public void RegisterStrategies(IDictionary<string, IRecordTick> strategies) =>
            _strategies.SetResult(strategies);

        public void RegisterCompletionCallbacks(IDictionary<string, IOrderCompletion> clientCompletion) =>
            _completionCallbacks.SetResult(clientCompletion);
    }
}