namespace Auto_Invest.Rest
{
    public interface IWebService
    {
        Task<AccountDetails> GetAccountDetailsAsync();
        Task<ContractDetails> GetContractDetailsAsync(string symbol);
    }
}
