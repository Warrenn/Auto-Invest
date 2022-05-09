namespace Auto_Invest
{
    public class TickWorker : BackgroundService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TickWorker> _logger;

        public TickWorker(IMediator mediator, ILogger<TickWorker> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TickWorker Started");
            var strategies = await _mediator.GetContractStrategiesAsync();
            var tickPositions = await _mediator.GetTickPositionReaderAsync();
            _logger.LogInformation("Reading ticks");

            await foreach (var position in tickPositions.ReadAllAsync(stoppingToken))
            {
                if (!strategies.ContainsKey(position.Symbol)) continue;
                var recordTick = strategies[position.Symbol];
                await recordTick.Tick(position);
            }

        }
    }
}
