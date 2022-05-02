using System.Net.Http.Headers;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class TradeWorker : BackgroundService, IContractClient
    {
        private readonly IMediator _mediator;

        public TradeWorker(string baseUrl, IMediator mediator)
        {
            _baseUrl = baseUrl;
            _mediator = mediator;
            _httpClient = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var orders = await _mediator.CompletedOrderReaderAsync();
            var completion = await _source.Task;

            await foreach (var completedOrder in orders.ReadAllAsync(stoppingToken))
            {
                if (completedOrder.Symbol != _symbol) continue;

                await completion.OrderCompleted(completedOrder);
            }

        }

        public async Task CancelOrder(int orderId)
        {
            var req = new HttpRequestMessage(HttpMethod.Post,
                new Uri($"{_baseUrl}/iserver/account/{accountId}/orders"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        }

        public async Task<ContractResult> PlaceStopLimit(StopLimit stopLimit)
        {
            throw new NotImplementedException();
        }

        public void ListenForCompletion(string symbol, IOrderCompletion orderCompletion)
        {
            _symbol = symbol;
            _source.SetResult(orderCompletion);
        }
    }
}
