using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Auto_Invest;

//var s = new Span<int>(new[] { 1, 2, 3, 4 });
//var b = new Span<int>(new[] {5, 6, 7, 8});
//var s2 = new Span<int>(new int[s.Length + b.Length]);
//s.CopyTo(s2);
//b.CopyTo(s2[s.Length..]);
//Console.WriteLine(s2.ToArray());
//var t = s[..];
//s = new Span<int>(new int[t.Length]);

//Console.ReadLine();


//var host = Host.CreateDefaultBuilder(args)
//    .ConfigureServices(services =>
//    {
//        services
//            .AddHostedService<Worker>();
//    })
//    .Build();

var bb = new TaskCompletionSource<object>();

var t1 = Task.Run(async () =>
{
    Console.WriteLine($"wait 1 {Thread.CurrentThread.ManagedThreadId}");
    var w1 = await bb.Task;
    Console.WriteLine($"wait 1 done {w1.GetHashCode()}");
});


var t2 = Task.Run(async () =>
{
    Console.WriteLine($"wait 2 {Thread.CurrentThread.ManagedThreadId}");
    var w2 = await bb.Task;
    Console.WriteLine($"wait 2 done {w2.GetHashCode()}");
});

var t4 = Task.Run(async () =>
{
    Console.WriteLine($"wait 4 {Thread.CurrentThread.ManagedThreadId}");
    var w2 = await bb.Task;
    Console.WriteLine($"wait 4 done {w2.GetHashCode()}");
});

var t5 = Task.Run(async () =>
{
    Console.WriteLine($"wait 5 {Thread.CurrentThread.ManagedThreadId}");
    var w2 = await bb.Task;
    Console.WriteLine($"wait 5 done {w2.GetHashCode()}");
});

var t6 = Task.Run(async () =>
{
    Console.WriteLine($"wait 6 {Thread.CurrentThread.ManagedThreadId}");
    var w2 = await bb.Task;
    Console.WriteLine($"wait 6 done {w2.GetHashCode()}");
});

var t3 = new Task(() =>
{
    Console.WriteLine("Sending");
    Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
    bb.SetResult(new object());
    Console.WriteLine("Sent");
});

var s = new TaskCompletionSource<object>();

Console.WriteLine("Started");
Console.ReadLine();
t3.RunSynchronously();
Task.WaitAll(t1, t2, t3, t4, t5, t6);
Console.WriteLine("Done");
Console.ReadLine();


//await host.RunAsync();
