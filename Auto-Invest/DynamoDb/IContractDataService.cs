namespace Auto_Invest.DynamoDb
{
    public interface IContractDataService
    {
        Task<ContractData[]> GetContractDataAsync();
        Task SaveContract(ContractData contractData);
        Task DeleteContract(ContractData contractData);
    }
}
