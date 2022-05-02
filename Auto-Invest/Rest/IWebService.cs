using Auto_Invest_Strategy;

namespace Auto_Invest.Rest
{
    public interface IWebService
    {
        Task<AccountDetails> GetAccountDetailsAsync();
        Task<ContractDetails> GetContractDetailsAsync(string symbol);
        Task CancelOrder(string accountId, int orderId);
        Task<ContractResult> PlaceStopLimit(ContractExtended contract, StopLimit stopLimit);
    }
}
