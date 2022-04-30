using System.Net.WebSockets;
using System.Text.Json;
using Auto_Invest;
using Microsoft.AspNetCore.SignalR.Client;

var s = new Span<int>(new[] { 1, 2, 3, 4 });
var b = new Span<int>(new[] {5, 6, 7, 8});
var s2 = new Span<int>(new int[s.Length + b.Length]);
s.CopyTo(s2);
b.CopyTo(s2[s.Length..]);
Console.WriteLine(s2.ToArray());
var t = s[..];
s = new Span<int>(new int[t.Length]);

Console.ReadLine();
var client = new HttpClient();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddHostedService<Worker>();
    })
    .Build();
new FileSystemWatcher("")
    .WaitForChanged(WatcherChangeTypes.Changed);

var de = await
    JsonSerializer.DeserializeAsync<int>("");

var results = await client.PostAsync("", new StringContent(""));
var dess = await JsonSerializer.DeserializeAsync<object>(await results.Content.ReadAsStreamAsync());

var connection = new WebSocket4Net.WebSocket("");
connection.MessageReceived += Connection_MessageReceived;

void Connection_MessageReceived(object? sender, WebSocket4Net.MessageReceivedEventArgs e)
{
    e.Message
}

await host.RunAsync();
