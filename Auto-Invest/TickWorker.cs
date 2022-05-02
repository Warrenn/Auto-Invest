using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Auto_Invest
{
    public class TickWorker : BackgroundService
    {
        private readonly IMediator _mediator;

        public TickWorker(IMediator mediator)
        {
            _mediator = mediator;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}
