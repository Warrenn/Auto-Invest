using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Auto_Invest;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }

        ITargetBlock<int> a;
        

        var client = new HttpClient();
        var results = await client.PostAsync("", new StringContent(""), stoppingToken);
        var content = results.Content.ReadAsStreamAsync(stoppingToken);
        var dess = await JsonSerializer.DeserializeAsync<object>(await content, cancellationToken: stoppingToken);

        var clientSocket = new ClientWebSocket();
        await clientSocket.ConnectAsync(new Uri(""), stoppingToken);
        var tickChannel = Channel.CreateUnbounded<object>();
        var orderChannel = Channel.CreateBounded<object>(3);

        var bb = new BroadcastBlock<object>(o => o);
        
    }
}
