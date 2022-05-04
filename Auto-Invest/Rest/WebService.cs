using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Auto_Invest_Strategy;

namespace Auto_Invest.Rest
{
    public class WebService : IWebService
    {
        private readonly LocalServerConfig _serverConfig;
        public const string JsonMediaType = "application/json";
        public const string Utf8Charset = "utf-8";

        private static void AddDefaultHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue(Utf8Charset));
        }

        private static async Task<TResult?> PostAsync<TInput, TResult>(
            HttpClient client,
            TInput input,
            string url,
            CancellationToken stoppingToken = default)
        {
            var request = await Serialize(input, stoppingToken);

            return await ExecuteAsync<TResult>(
                () => client.PostAsync(url, new StringContent(request, Encoding.UTF8, JsonMediaType),
                    stoppingToken), stoppingToken);
        }

        private static Task<TResult?> GetAsync<TResult>(HttpClient client, string url, CancellationToken stoppingToken = default) =>
            ExecuteAsync<TResult>(() => client.GetAsync(url, stoppingToken), stoppingToken);


        private static Task<TResult?> DeleteAsync<TResult>(HttpClient client, string url, CancellationToken stoppingToken = default) =>
            ExecuteAsync<TResult>(() => client.DeleteAsync(url, stoppingToken), stoppingToken);

        private static async Task<string> Serialize<TInput>(TInput input, CancellationToken stoppingToken)
        {
            await using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, input, cancellationToken: stoppingToken);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var stringValue = await reader.ReadToEndAsync();

            return stringValue;
        }

        private static async Task<T?> ExecuteAsync<T>(Func<Task<HttpResponseMessage>> execute, CancellationToken stoppingToken = default)
        {
            var results = await execute();
            var content = results.Content.ReadAsStreamAsync(stoppingToken);
            var output = await JsonSerializer.DeserializeAsync<T>(await content, cancellationToken: stoppingToken);
            return output;
        }

        public WebService(LocalServerConfig serverConfig)
        {
            _serverConfig = serverConfig;
        }

        public async Task<AccountDetails> GetAccountDetailsAsync(CancellationToken stoppingToken = default)
        {
            using var client = new HttpClient();
            AddDefaultHeaders(client);
            var url = $"{_serverConfig.HostUrl}/v1/api/iserver/accounts";
            var accountData = await GetAsync<dynamic>(client, url, stoppingToken);
            var accountId = accountData?.accounts.First() ?? "";
            if (string.IsNullOrWhiteSpace(accountId)) throw new Exception("Account Id is not found");
            return new AccountDetails
            {
                AccountId = accountId
            };
        }

        public async Task<ContractDetails> GetContractDetailsAsync(string symbol, CancellationToken stoppingToken = default)
        {
            using var client = new HttpClient();
            symbol = symbol.ToUpper();
            AddDefaultHeaders(client);
            var url = $"{_serverConfig.HostUrl}/v1/api/trsrv/stocks?symbols={symbol}";
            var accountData = await GetAsync<dynamic>(client, url, stoppingToken) ?? throw new ArgumentNullException(nameof(symbol), $"Symbol {symbol} could not be found");
            var conId = accountData[symbol].First().contracts.First().conid ?? "";
            return new ContractDetails
            {
                ContractId = conId,
                ContractSymbol = symbol
            };
        }

        public async Task CancelOrder(string accountId, int orderId, CancellationToken stoppingToken = default)
        {
            using var client = new HttpClient();
            AddDefaultHeaders(client);
            var url = $"{_serverConfig.HostUrl}/v1/api/iserver/account/{accountId}/order/{orderId}";
            await DeleteAsync<dynamic>(client, url, stoppingToken);
        }

        public async Task<ContractResult> PlaceStopLimit(ContractExtended contract, StopLimit stopLimit, CancellationToken stoppingToken = default)
        {
            using var client = new HttpClient();
            AddDefaultHeaders(client);
            var orderDetails = new
            {
                conid = contract.ConId,
                secType = "STK",
                orderType = "STP",
                side = stopLimit.Side == ActionSide.Buy ? "BUY" : "SELL",
                quantity = stopLimit.Quantity,
                price = stopLimit.StopPrice,
                tif = "GTC"
            };

            if (stopLimit.OrderId > 0)
            {
                var url = $"{_serverConfig.HostUrl}/v1/api/iserver/account/{contract.AccountId}/orders";
                var request = new { orders = new[] { orderDetails } };
                var response = await PostAsync<dynamic, dynamic>(client, request, url, stoppingToken);
                var orderIdString = (response?.First().id ?? "") as string;
                if (!int.TryParse(orderIdString, out var orderid)) orderid = 0;
                return new ContractResult
                {
                    OrderId = orderid
                };
            }

            var modifyUrl = $"{_serverConfig.HostUrl}/v1/api/iserver/account/{contract.AccountId}/order/{stopLimit.OrderId}";
            var modifyResponse = await PostAsync<dynamic, dynamic>(client, orderDetails, modifyUrl, stoppingToken);
            var modifyOrderIdString = (modifyResponse?.First().order_id ?? "") as string;
            if (!int.TryParse(modifyOrderIdString, out var modifyOrderId)) modifyOrderId = 0;
            return new ContractResult
            {
                OrderId = modifyOrderId
            };
        }
    }
}
