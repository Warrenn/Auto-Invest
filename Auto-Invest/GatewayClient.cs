using Auto_Invest.Rest;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class GatewayClient : IContractClient
    {
        private readonly IWebService _webService;
        private readonly ContractExtended[] _contracts;
        private readonly string _accountId;

        public IDictionary<string, IOrderCompletion> Completion { get; } =
            new Dictionary<string, IOrderCompletion>();

        public GatewayClient(IWebService webService, ContractExtended[] contracts)
        {
            _webService = webService;
            _contracts = contracts;
            _accountId = contracts.First().AccountId ?? throw new NullReferenceException("Account id cannot be null");
        }

        public async Task CancelOrder(int orderId) => await _webService.CancelOrder(_accountId, orderId);

        public async Task<ContractResult> PlaceStopLimit(StopLimit stopLimit)
        {
            var contract = _contracts.First(_ => _.Symbol == stopLimit.Symbol);
            return await _webService.PlaceStopLimit(contract, stopLimit);
        }

        public void ListenForCompletion(string symbol, IOrderCompletion orderCompletion) =>
            Completion[symbol] = orderCompletion;
    }
}
