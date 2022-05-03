using Auto_Invest;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddSingleton(new LocalServerConfig
            {
                ResultsFile = Environment.GetEnvironmentVariable(""),
                HostUrl = Environment.GetEnvironmentVariable("")
            })
            .AddScoped<IMediator, AsyncMediator>()
            .AddHostedService<SetupEnvironmentWorker>()
            .AddHostedService<ContractChangesWorker>()
            .AddHostedService<OrderCompletedWorker>()
            .AddHostedService<TickWorker>()
            .AddHostedService<WebSocketWorker>();
    })
    .Build();

await host.RunAsync();