using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Auto_Invest;
using Auto_Invest.DynamoDb;
using Auto_Invest.Rest;


FallbackCredentialsFactory.CredentialsGenerators.Insert(0, () =>
{
    var profileName = Environment.GetEnvironmentVariable("AWS_PROFILE");
    if (string.IsNullOrWhiteSpace(profileName)) return null;
    var chain = new CredentialProfileStoreChain();
    var attempt = chain.TryGetProfile(profileName, out var profile);
    return !attempt || profile == null ? null : new BasicAWSCredentials(profile.Options.AccessKey, profile.Options.SecretKey);
});

var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddSingleton(new LocalServerConfig
            {
                ResultsFile =
                    $"{Environment.GetEnvironmentVariable("IBEAM_RESULTS_DIR")}/{Environment.GetEnvironmentVariable("IBEAM_RESULTS_FILENAME")}",
                HostUrl = Environment.GetEnvironmentVariable("GATEWAY_URL"),
                WebSocketUrl = Environment.GetEnvironmentVariable("WEBSOCKET_URL"),
                Environment = Environment.GetEnvironmentVariable("ENVNAME") ?? throw new Exception("ENVNAME is not set")
            })
            .AddSingleton<IMediator, AsyncMediator>()
            .AddSingleton<IContractDataService, ContractDataService>()
            .AddSingleton<IWebService, WebService>()
            .AddHostedService<SetupEnvironmentWorker>()
            .AddHostedService<WebSocketResultsWorker>()
            .AddHostedService<ContractChangesWorker>()
            .AddHostedService<OrderCompletedWorker>()
            .AddHostedService<TickWorker>()
            .AddHostedService<WebSocketWorker>();
    })
    .Build();

await host.RunAsync();