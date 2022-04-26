using Auto_Invest;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
