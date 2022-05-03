using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Auto_Invest_Strategy;

namespace Auto_Invest.Rest
{
    public class WebService : IWebService
    {
        public const string JsonMediaType = "application/json";

        private static HttpClient AddDefaultHeadersAsync(HttpClient client)
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            return client;
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

        private static string ToQueryString(NameValueCollection nvc)
        {
            var array = (
                from key in nvc.AllKeys
                let vs = nvc.GetValues(key)
                from value in vs
                where !string.IsNullOrWhiteSpace(value)
                select $"{HttpUtility.UrlEncode(key)}={HttpUtility.UrlEncode(value)}"
            ).ToArray();

            return $"?{string.Join("&", array)}";
        }

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
        public Task<AccountDetails> GetAccountDetailsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ContractDetails> GetContractDetailsAsync(string symbol)
        {
            throw new NotImplementedException();
        }

        public Task CancelOrder(string accountId, int orderId)
        {
            throw new NotImplementedException();
        }

        public Task<ContractResult> PlaceStopLimit(ContractExtended contract, StopLimit stopLimit)
        {
            throw new NotImplementedException();
        }
    }
}
