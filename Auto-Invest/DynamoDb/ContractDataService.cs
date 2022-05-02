using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

namespace Auto_Invest.DynamoDb
{
    public class ContractDataService: IContractDataService
    {

        private static readonly AmazonDynamoDBClient Client = new();

        #region Implementation of IContractDataService

        public async Task<ContractData[]> GetContractDataAsync()
        {
            var context = new DynamoDBContext(Client);
            var results = await context.ScanAsync<ContractData>(Enumerable.Empty<ScanCondition>()).GetRemainingAsync();
            return results.ToArray();
        }

        public async Task SaveContract(ContractData contractData)
        {
            var context = new DynamoDBContext(Client);
            await context.SaveAsync(contractData);
        }

        public async Task DeleteContract(ContractData contractData)
        {
            var context = new DynamoDBContext(Client);
            await context.DeleteAsync(contractData);
        }

        #endregion
    }
}
