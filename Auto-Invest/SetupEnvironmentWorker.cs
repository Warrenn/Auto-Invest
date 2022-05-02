using System.Text.Json;
using Auto_Invest.DynamoDb;
using Auto_Invest.Rest;

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

        public SetupEnvironmentWorker(LocalServerConfig serverConfig, IWebService webService, IContractDataService contractDataService, IMediator mediator)
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

            if (!File.Exists(_serverConfig.ResultsFile))
            {
                var watcher = new FileSystemWatcher(_serverConfig.ResultsFile);
                watcher.WaitForChanged(WatcherChangeTypes.Created);
                File.Delete(_serverConfig.ResultsFile);
            }

            var gateWayResultJson = await File.ReadAllTextAsync(_serverConfig.ResultsFile, stoppingToken);
            var gateWayResult = JsonSerializer.Deserialize<GateWayResult>(gateWayResultJson) ?? throw new Exception($"{_serverConfig.ResultsFile} failed Json conversion");

            if (gateWayResult.ShutDown) throw new Exception("Gateway is shut down");
            if (!gateWayResult.Authenticated) throw new Exception("Gateway is not Authenticated");

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

            //for each contract
            //create the contract manager
            ////contractManager = new ContractManager(contractClient);
            ////strategy = new TrailingBuySellStrategy(contractManager);
            ////contractManager.RegisterContract(contract);
            ///
            /// get the strategies as dictionary (tick Recordings)
            /// get the completions as dictionary
            /// get the changes reader for each contract
            ///
            /// _mediator(contractReaders);
            _mediator.RegisterContracts(extendedList);


        }

        #endregion
    }
}
