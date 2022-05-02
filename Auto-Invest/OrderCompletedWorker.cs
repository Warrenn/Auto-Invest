using System.Net.Http.Headers;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class OrderCompletedWorker : BackgroundService
    {
        private readonly IMediator _mediator;

        public OrderCompletedWorker(IMediator mediator)
        {
            _mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var completions = await _mediator.GetCompletionCallbacksAsync();
            var orders = await _mediator.GetCompletedOrderReaderAsync();

            await foreach (var completedOrder in orders.ReadAllAsync(stoppingToken))
            {
                if (!completions.ContainsKey(completedOrder.Symbol)) continue;
                var completion = completions[completedOrder.Symbol];
                await completion.OrderCompleted(completedOrder);
            }

        }
    }
}
