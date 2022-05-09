namespace Auto_Invest
{
    public class OrderCompletedWorker : BackgroundService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OrderCompletedWorker> _logger;

        public OrderCompletedWorker(IMediator mediator, ILogger<OrderCompletedWorker> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Started OrderCompletedWorker awaiting dependencies");
            var completions = await _mediator.GetCompletionCallbacksAsync();
            var orders = await _mediator.GetCompletedOrderReaderAsync();
            _logger.LogInformation("dependencies met looping through all completed orders");

            await foreach (var completedOrder in orders.ReadAllAsync(stoppingToken))
            {
                if (!completions.ContainsKey(completedOrder.Symbol)) continue;
                var completion = completions[completedOrder.Symbol];
                await completion.OrderCompleted(completedOrder);
            }

        }
    }
}
