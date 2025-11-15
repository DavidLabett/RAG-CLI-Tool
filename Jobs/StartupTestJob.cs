using Microsoft.Extensions.Logging;
using Quartz;

namespace SecondBrain.Jobs
{
    [DisallowConcurrentExecution]
    public class StartupTestJob : IJob
    {
        private readonly ILogger<StartupTestJob> _logger;

        public StartupTestJob(ILogger<StartupTestJob> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task Execute(IJobExecutionContext context)
        {
            if (context == null)
            {
                _logger.LogError("IJobExecutionContext is null");
                throw new ArgumentNullException(nameof(context));
            }

            JobDataMap data = context.MergedJobDataMap; // combines JobDetail and Trigger data maps

            string? msg = data?.GetString("MessageToLog") ?? "StartupTestJob executed successfully!";

            _logger.LogInformation(msg);

            return Task.CompletedTask;
        }
    }
}