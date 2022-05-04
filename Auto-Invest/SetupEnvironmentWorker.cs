using System.Text.Json;
using System.Threading.Channels;
using Auto_Invest.DynamoDb;
using Auto_Invest.Rest;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class SetupEnvironmentWorker : BackgroundService
    {
        private readonly LocalServerConfig _serverConfig;
        private readonly IWebService _webService;
        private readonly IContractDataService _contractDataService;
        private readonly IMediator _mediator;

        public class GateWayResult
        {
            public bool Authenticated { get; set; }
            public bool ShutDown { get; set; }
        }

        public SetupEnvironmentWorker(
            LocalServerConfig serverConfig,
            IWebService webService,
            IContractDataService contractDataService,
            IMediator mediator)
        {
            _serverConfig = serverConfig;
            _webService = webService;
            _contractDataService = contractDataService;
            _mediator = mediator;
        }

        #region Overrides of BackgroundService

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_serverConfig.ResultsFile)) throw new ArgumentNullException(nameof(_serverConfig.ResultsFile));
            if (string.IsNullOrWhiteSpace(_serverConfig.HostUrl)) throw new ArgumentNullException(nameof(_serverConfig.HostUrl));

            //if (!File.Exists(_serverConfig.ResultsFile))
            //{
            //    var watcher = new FileSystemWatcher(Path.GetDirectoryName(_serverConfig.ResultsFile) ?? string.Empty);
            //    watcher.WaitForChanged(WatcherChangeTypes.Created);
            //}

            //var gateWayResultJson = await File.ReadAllTextAsync(_serverConfig.ResultsFile, stoppingToken);
            //var gateWayResult = JsonSerializer.Deserialize<GateWayResult>(gateWayResultJson) ?? throw new Exception($"{_serverConfig.ResultsFile} failed Json conversion");
            //File.Delete(_serverConfig.ResultsFile);

            //if (gateWayResult.ShutDown) throw new Exception("Gateway is shut down");
            //if (!gateWayResult.Authenticated) throw new Exception("Gateway is not Authenticated");

            var contracts = await _contractDataService.GetContractDataAsync();
            var details = await _webService.GetAccountDetailsAsync();
            var accountId = details.AccountId;

            var extendedList = new List<ContractExtended>();
            foreach (var contract in contracts)
            {
                var saveChanges = false;

                if (string.IsNullOrWhiteSpace(contract.AccountId))
                {
                    saveChanges = true;
                    contract.AccountId = accountId;
                }

                if (string.IsNullOrWhiteSpace(contract.ConId))
                {
                    saveChanges = true;
                    var contractDetails = await _webService.GetContractDetailsAsync(contract.Symbol);
                    contract.ConId = contractDetails.ContractId;
                }

                if (saveChanges) await _contractDataService.SaveContract(contract);

                var extendedContract = new ContractConverter(contract).Contract;
                extendedList.Add(extendedContract);
            }

            var client = new GatewayClient(_webService, extendedList.ToArray());
            var strategies = new Dictionary<string, IRecordTick>();
            var contractChanges = new List<ChannelReader<Contract>>();
            foreach (var contract in extendedList)
            {
                var contractManager = new ContractManager(client);
                var strategy = new TrailingBuySellStrategy(contractManager);
                contractManager.RegisterContract(contract);

                strategies[contract.Symbol] = strategy;
                contractChanges.Add(contract.Changes);
            }

            _mediator.RegisterContractChanges(contractChanges);
            _mediator.RegisterStrategies(strategies);
            _mediator.RegisterCompletionCallbacks(client.Completion);
            _mediator.RegisterContracts(extendedList);
        }

        #endregion
    }
}
