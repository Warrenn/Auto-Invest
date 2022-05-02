using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WebSocket4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using CoreBot;
using IBApi;
using Contract = Auto_Invest_Strategy.Contract;
using static Newtonsoft.Json.JsonConvert;
using static Auto_Invest.EventAwaiter;

namespace Auto_Invest
{
    public class Program
    {
        private WebSocket websocket;
        private ContractManager contractManager;
        private Contract contract;
        private IContractClient contractClient;
        private TrailingBuySellStrategy strategy;
        private IBClient ibClient;

        public static async Task Main(string[] args)
        {
            await new Program().Start();
        }

        public async Task Start()
        {
            this.contract = new Contract(
                "SPGI",
                100M,
                0.01M,
                1,
                marginProtection: 2M);

            ibClient = new IBClient(1, "127.0.0.1", 7497);
            await AwaitEvent(
                h => ibClient.ConnectAckEvent += h,
                h => ibClient.ConnectAckEvent -= h,
                () => ibClient.Connect());

            websocket = new WebSocket("wss://socket.polygon.io/stocks", sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);
            var args = await AwaitEvent(
                h => websocket.Opened += h,
                h => websocket.Opened -= h,
                () => websocket.Open());

            contractClient = new IBKRClientContract(ibClient);
            contractManager = new ContractManager(contractClient);
            strategy = new TrailingBuySellStrategy(contractManager);
            contractManager.RegisterContract(contract);

            ibClient.Run();
            Console.ReadKey();
        }

        private void IbClient_ConnectAckEvent()
        {

            this.websocket.Opened += websocket_Opened;
            this.websocket.Error += websocket_Error;
            this.websocket.Closed += websocket_Closed;
            this.websocket.MessageReceived += websocket_MessageReceived;
            this.websocket.Open();
            Console.ReadKey();
        }

        private void websocket_Opened(object sender, EventArgs e)
        {
            var polygonApiKey = Environment.GetEnvironmentVariable("POLYGON_APIKEY");
            Console.WriteLine("Connected!");
            this.websocket.Send($"{{\"action\":\"auth\",\"params\":\"{polygonApiKey}\"}}");
            this.websocket.Send("{\"action\":\"subscribe\",\"params\":\"A.SPGI\"}");
        }

        private void websocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Console.WriteLine("WebSocket Error");
            Console.WriteLine(e.Exception.Message);
        }
        private void websocket_Closed(object sender, EventArgs e)
        {
            Console.WriteLine("Connection Closed...");
            // Add Reconnect logic... this.Start()
        }
        private void websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var message = e.Message;
            Console.WriteLine(message);

            if (string.IsNullOrWhiteSpace(message)) return;

            var responses = DeserializeObject<WebSocketResponse[]>(message);

            foreach (var response in responses)
            {
                if (response == null || response.c == 0) continue;
                strategy.Tick(new TickPosition
                {
                    Symbol = "SPGI",
                    Position = response.c

                }).Start();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
