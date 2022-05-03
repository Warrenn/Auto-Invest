using Auto_Invest.DynamoDb;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class ContractChangesWorker : BackgroundService
    {
        private readonly IContractDataService _contractDataService;
        private readonly IMediator _mediator;

        public ContractChangesWorker(IContractDataService contractDataService, IMediator mediator)
        {
            _contractDataService = contractDataService;
            _mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var contracts = (await _mediator.GetContractsAsync()).ToArray();
            var readers = await _mediator.GetContractChangesReaderAsync();

            foreach (var channelReader in readers)
            {
                await foreach (var contract in channelReader.ReadAllAsync(stoppingToken))
                {
                    var extendedContract = contracts.First(_ => _.Symbol == contract.Symbol);
                    var contractData = new ContractData
                    {
                        Funding = contract.Funding,
                        AccountId = extendedContract.AccountId,
                        AveragePrice = contract.AveragePrice,
                        BuyOrderLimit = contract.BuyOrderLimit,
                        ConId = extendedContract.ConId,
                        EmergencyOrders = contract.EmergencyOrders?.ToArray() ?? Array.Empty<EmergencyOrderDetail>(),
                        LowerBound = contract.LowerBound,
                        MarginProtection = contract.MarginProtection,
                        QuantityOnHand = contract.QuantityOnHand,
                        RunState = contract.RunState,
                        SafetyBands = contract.SafetyBands,
                        SellOrderLimit = contract.SellOrderLimit,
                        Symbol = contract.Symbol,
                        TrailingOffset = contract.TrailingOffset,
                        TotalCost = contract.TotalCost,
                        TradePercent = contract.TradePercent,
                        TrailingBuyOrderId = contract.TrailingBuyOrderId,
                        TrailingSellOrderId = contract.TrailingSellOrderId,
                        UpperBound = contract.UpperBound
                    };

                    await _contractDataService.SaveContract(contractData);
                }
            }
        }
    }
}
