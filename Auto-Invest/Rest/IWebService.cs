using Auto_Invest_Strategy;

namespace Auto_Invest.Rest
{
    public interface IWebService
    {
        Task<AccountDetails> GetAccountDetailsAsync(CancellationToken stoppingToken = default);
        Task<ContractDetails> GetContractDetailsAsync(string symbol, CancellationToken stoppingToken = default);
        Task CancelOrder(string accountId, int orderId, CancellationToken stoppingToken = default);
        Task<ContractResult> PlaceStopLimit(ContractExtended contract, StopLimit stopLimit, CancellationToken stoppingToken = default);
    }
}
