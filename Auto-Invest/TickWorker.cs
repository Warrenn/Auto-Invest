namespace Auto_Invest
{
    public class TickWorker : BackgroundService
    {
        private readonly IMediator _mediator;

        public TickWorker(IMediator mediator)
        {
            _mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var strategies = await _mediator.GetContractStrategiesAsync();
            var tickPositions = await _mediator.GetTickPositionReaderAsync();

            await foreach (var position in tickPositions.ReadAllAsync(stoppingToken))
            {
                if (!strategies.ContainsKey(position.Symbol)) continue;
                var recordTick = strategies[position.Symbol];
                await recordTick.Tick(position);
            }

        }
    }
}
