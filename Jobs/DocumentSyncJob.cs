using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SecondBrain.Services;
using Microsoft.KernelMemory;

namespace SecondBrain.Jobs
{
    [DisallowConcurrentExecution]
    public class DocumentSyncJob : IJob
    {
        private readonly ILogger<DocumentSyncJob> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DocumentSyncJob(ILogger<DocumentSyncJob> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        public async Task Execute(IJobExecutionContext context)
        {
            if (context == null)
            {
                _logger.LogError("IJobExecutionContext is null");
                throw new ArgumentNullException(nameof(context));
            }

            _logger.LogInformation("Starting document sync job...");
            
            var syncService = _serviceProvider.GetRequiredService<DocumentSyncService>();
            var memory = _serviceProvider.GetRequiredService<IKernelMemory>();

            await syncService.SyncDocuments(memory);
            
            _logger.LogInformation("Document sync job completed successfully.");
        }
    }
}

