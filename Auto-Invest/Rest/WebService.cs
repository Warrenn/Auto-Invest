using System.Net;
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

        private static void AddDefaultHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue(Encoding.UTF8.BodyName));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Auto-Invest", "1.0"));
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

        private static HttpClient CreateClient()
        {
            var httpClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var httpClient = new HttpClient(httpClientHandler);
            AddDefaultHeaders(httpClient);
            return httpClient;
        }

        public WebService(LocalServerConfig serverConfig)
        {
            _serverConfig = serverConfig;
        }

        public async Task<AccountDetails> GetAccountDetailsAsync(CancellationToken stoppingToken = default)
        {
            using var client = CreateClient();
            var url = $"{_serverConfig.HostUrl}/v1/api/iserver/accounts";
            var accountData = await GetAsync<JsonElement>(client, url, stoppingToken);
            var accountId = accountData.GetProperty("accounts")[0].GetString() ?? "";
            if (string.IsNullOrWhiteSpace(accountId)) throw new Exception("Account Id is not found");
            return new AccountDetails
            {
                AccountId = accountId
            };
        }

        public async Task<ContractDetails> GetContractDetailsAsync(string symbol, CancellationToken stoppingToken = default)
        {
            using var client = CreateClient();
            symbol = symbol.ToUpper();
            var url = $"{_serverConfig.HostUrl}/v1/api/trsrv/stocks?symbols={symbol}";
            var accountData = await GetAsync<JsonElement>(client, url, stoppingToken);
            var conId =
                accountData.GetProperty(symbol)[0].GetProperty("contracts")[0].GetProperty("conid").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(conId)) throw new Exception("Contract Id is not found");
            return new ContractDetails
            {
                ContractId = conId,
                ContractSymbol = symbol
            };
        }

        public async Task CancelOrder(string accountId, int orderId, CancellationToken stoppingToken = default)
        {
            using var client = CreateClient();
            var url = $"{_serverConfig.HostUrl}/v1/api/iserver/account/{accountId}/order/{orderId}";
            await DeleteAsync<JsonElement>(client, url, stoppingToken);
        }

        public async Task<ContractResult> PlaceStopLimit(ContractExtended contract, StopLimit stopLimit, CancellationToken stoppingToken = default)
        {
            using var client = CreateClient();
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
                var response = await PostAsync<object, JsonElement>(client, request, url, stoppingToken);
                var orderIdString = response[0].GetProperty("id").GetString() ?? "";
                if (!int.TryParse(orderIdString, out var orderId)) orderId = 0;
                return new ContractResult
                {
                    OrderId = orderId
                };
            }

            var modifyUrl = $"{_serverConfig.HostUrl}/v1/api/iserver/account/{contract.AccountId}/order/{stopLimit.OrderId}";
            var modifyResponse = await PostAsync<object, JsonElement>(client, orderDetails, modifyUrl, stoppingToken);
            var modifyOrderIdString = modifyResponse[0].GetProperty("order_id").GetString() ?? "";
            if (!int.TryParse(modifyOrderIdString, out var modifyOrderId)) modifyOrderId = 0;
            return new ContractResult
            {
                OrderId = modifyOrderId
            };
        }
    }
}
