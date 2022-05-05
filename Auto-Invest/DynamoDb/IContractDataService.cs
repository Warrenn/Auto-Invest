namespace Auto_Invest.DynamoDb
{
    public interface IContractDataService
    {
        Task<ContractData[]> GetContractDataAsync(CancellationToken cancellationToken = default);
        Task SaveContract(ContractData contractData, CancellationToken cancellation = default);
        Task DeleteContract(ContractData contractData, CancellationToken cancellation = default);
    }
}
