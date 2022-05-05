using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

namespace Auto_Invest.DynamoDb
{
    public class ContractDataService : IContractDataService
    {
        private readonly LocalServerConfig _serverConfig;

        private static readonly AmazonDynamoDBClient Client = new();

        public ContractDataService(LocalServerConfig serverConfig)
        {
            _serverConfig = serverConfig;
        }

        #region Implementation of IContractDataService

        public async Task<ContractData[]> GetContractDataAsync(CancellationToken cancellation = default)
        {
            var context = new DynamoDBContext(Client);
            var results = await context.QueryAsync<ContractData>(_serverConfig.Environment).GetRemainingAsync(cancellation);
            return results.ToArray();
        }

        public async Task SaveContract(ContractData contractData, CancellationToken cancellation = default)
        {
            var context = new DynamoDBContext(Client);
            await context.SaveAsync(contractData, cancellation);
        }

        public async Task DeleteContract(ContractData contractData, CancellationToken cancellation = default)
        {
            var context = new DynamoDBContext(Client);
            await context.DeleteAsync(contractData, cancellation);
        }

        #endregion
    }
}
