using Auto_Invest.DynamoDb;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class ContractChangesWorker : BackgroundService
    {
        private readonly IContractDataService _contractDataService;
        private readonly IMediator _mediator;
        private readonly LocalServerConfig _serverConfig;
        private readonly ILogger<ContractChangesWorker> _logger;

        public ContractChangesWorker(
            IContractDataService contractDataService,
            IMediator mediator,
            LocalServerConfig serverConfig,
            ILogger<ContractChangesWorker> logger)
        {
            _contractDataService = contractDataService;
            _mediator = mediator;
            _serverConfig = serverConfig;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Started but awaiting Contracts and Contracts Reader");
            var contracts = (await _mediator.GetContractsAsync()).ToArray();
            var readers = await _mediator.GetContractChangesReaderAsync();
            _logger.LogInformation("Reading contract changes");

            foreach (var channelReader in readers)
            {
                await foreach (var contract in channelReader.ReadAllAsync(stoppingToken))
                {
                    var extendedContract = contracts.First(_ => _.Symbol == contract.Symbol);
                    _logger.LogTrace("Contract Updated {ConId} {Symbol} {RunStatue}", extendedContract.ConId, contract.Symbol, contract.RunState);

                    var contractData = new ContractData
                    {
                        Environment = _serverConfig.Environment,
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

                    await _contractDataService.SaveContract(contractData, stoppingToken);
                }
            }
        }
    }
}
