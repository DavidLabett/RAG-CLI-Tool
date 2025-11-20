using SecondBrain.Jobs;
using SecondBrain.Models;
using SecondBrain.Services;
using SecondBrain.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Quartz;
using Serilog;
using System.Configuration;
using System.Text.Json;
using Spectre.Console.Cli;

namespace SecondBrain.Services
{
    public static class ServiceCollectionExtensions
    {
        // Extension method to register services
        public static IServiceCollection AddRAGKnowledgeBase(this IServiceCollection services, IConfiguration config)
        {
            try
            {
                // Bind & Register (IOptions<AppSettings>) from configuration
                services.AddOptions<AppSettings>()
                    .Bind(config.GetRequiredSection("AppSettings"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                //Register TimeProvider for global time access
                services.AddSingleton<TimeProvider>(TimeProvider.System);

                // Register RAG Services:
                // HttpClient for Ollama API
                services.AddHttpClient<OllamaService>((serviceProvider, client) =>
                {
                    var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
                    var ollamaUrl = appSettings.RAG.OllamaUrl?.TrimEnd('/');
                    client.BaseAddress = new Uri(ollamaUrl + "/");
                    client.Timeout = TimeSpan.FromSeconds(500); // Ollama requests can take longer
                });
                // Note: OllamaService is registered via AddHttpClient above
                services.AddTransient<RagChatService>();
                services.AddTransient<LlmChatService>();
                services.AddTransient<DocumentEmbeddingService>();
                services.AddTransient<KernelMemoryService>();
                services.AddSingleton<RagResultService>();
                // Register Jobs
                services.AddTransient<StartupTestJob>();

                services.AddSingleton<Microsoft.KernelMemory.IKernelMemory>(serviceProvider =>
                {
                    var kernelMemoryService = serviceProvider.GetRequiredService<KernelMemoryService>();
                    return kernelMemoryService.Build();
                });


                // Register Logging with "Logging" configuration from appsettings.json
                services.AddLogging(builder =>
                {
                    builder
                    .AddConfiguration(config.GetSection("Logging"))
                    .AddSerilog(new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning) // *Ignore HTTP Client Logs*
                    .WriteTo.Console()
                    .CreateLogger());
                });

                // Register Json parsing service
                services.AddSingleton<JsonSerializerOptions>(serviceProvider =>
                {
                    var options = new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true };
                    return options;
                });

                services.AddSingleton<SyncState>(serviceProvider =>
                {
                    var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
                    var logger = serviceProvider.GetRequiredService<ILogger<SyncState>>();
                    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
                    return new SyncState(appSettings, logger, timeProvider);
                });
                
                // Register DocumentSyncService with folder path from config
                services.AddTransient<DocumentSyncService>(serviceProvider =>
                {
                    var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
                    var logger = serviceProvider.GetRequiredService<ILogger<DocumentSyncService>>();
                    var documentEmbeddingService = serviceProvider.GetRequiredService<DocumentEmbeddingService>();
                    var syncState = serviceProvider.GetRequiredService<SyncState>();
                    var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
                    return new DocumentSyncService(logger, documentEmbeddingService, syncState, timeProvider, appSettings.RAG.DocumentFolderPath);
                });
                services.AddTransient<DocumentSyncJob>();

                // Quartz Add & Configuration
                // Note: UseMicrosoftDependencyInjectionJobFactory is now the default, no need to call it
                services.Configure<QuartzOptions>(config.GetSection("Quartz"));
                services.AddQuartz(q =>
                {
                    q.UseXmlSchedulingConfiguration(x =>
                    {
                        x.Files = new[] { "quartz_jobs.xml" };
                        x.ScanInterval = TimeSpan.FromSeconds(2);
                        x.FailOnFileNotFound = true;
                        x.FailOnSchedulingError = true;
                    });
                });

                // Register IScheduler
                services.AddSingleton<IScheduler>(provider =>
                {
                    var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
                    return schedulerFactory.GetScheduler().Result;
                });
            }
            // Transform exceptions to ConfigurationErrorsException with context
            catch (OptionsValidationException optEx)
            {
                throw new ConfigurationErrorsException($"Configuration validation failed: {string.Join(", ", optEx.Failures)}", optEx);
            }
            catch (InvalidOperationException invOpEx) when (invOpEx.Message.Contains("service"))
            {
                throw new ConfigurationErrorsException($"Service registration failed: {invOpEx.Message}", invOpEx);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Unexpected error - Service registration failed: {ex.Message}", ex);
            }

            return services;
        }

        /// <summary>
        /// Configures and registers the Spectre.Console CommandApp with dependency injection
        /// </summary>
        public static IServiceCollection AddCommandApp(this IServiceCollection services, IConfiguration config)
        {
            // Register commands in DI container so they can have dependencies injected
            services.AddTransient<VersionCommand>();
            services.AddTransient<SyncCommand>();
            services.AddTransient<StatusCommand>();
            services.AddTransient<RagChatCommand>();
            services.AddTransient<LlmCommand>();
            services.AddTransient<ConfigCommand>();
            services.AddTransient<ListCommand>();
            services.AddTransient<TreeCommand>();
            services.AddTransient<ModeCommand>();
            
            // Register CommandApp as a singleton
            services.AddSingleton<CommandApp>(serviceProvider =>
            {
                var app = new CommandApp(new TypeRegistrar(serviceProvider));
                var appSettings = serviceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
                var cliSettings = appSettings.CLI;

                app.Configure(cliConfig =>
                {
                    cliConfig.SetApplicationName(cliSettings.ApplicationName);
                    cliConfig.SetApplicationVersion(cliSettings.ApplicationVersion);
                    
                    // Register commands - these will be resolved through DI
                    cliConfig.AddCommand<VersionCommand>("version")
                        .WithDescription("Show version information")
                        .WithAlias("v");
                    
                    cliConfig.AddCommand<SyncCommand>("sync")
                        .WithDescription("Sync documents from folder to knowledge base");
                    
                    cliConfig.AddCommand<StatusCommand>("status")
                        .WithDescription("Check system status and health");
                    
                    cliConfig.AddCommand<RagChatCommand>("rag")
                        .WithDescription("Start interactive chat session with RAG knowledge base");
                    
                    cliConfig.AddCommand<LlmCommand>("llm")
                        .WithDescription("Start interactive chat session with a direct LLM");
                    
                    cliConfig.AddCommand<ConfigCommand>("config")
                        .WithDescription("Manage configuration interactively");
                    
                    cliConfig.AddCommand<ListCommand>("list")
                        .WithDescription("List all documents and show sync status");
                    
                    cliConfig.AddCommand<TreeCommand>("tree")
                        .WithDescription("Display the latest RAG sources and chunks as a tree structure");
                    
                    cliConfig.AddCommand<ModeCommand>("mode")
                        .WithDescription("Switch between 'local' (Ollama) and 'online' (CloudFlare) modes");
                    
                    // Validate examples
                    cliConfig.ValidateExamples();
                });

                return app;
            });

            return services;
        }
    }

    /// <summary>
    /// Type registrar for Spectre.Console.Cli to use with dependency injection
    /// </summary>
    internal sealed class TypeRegistrar : ITypeRegistrar
    {
        private readonly IServiceProvider _serviceProvider;

        public TypeRegistrar(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITypeResolver Build()
        {
            return new TypeResolver(_serviceProvider);
        }

        public void Register(Type service, Type implementation)
        {
            // Services are already registered in DI container
        }

        public void RegisterInstance(Type service, object implementation)
        {
            // Services are already registered in DI container
        }

        public void RegisterLazy(Type service, Func<object> factory)
        {
            // Services are already registered in DI container
        }
    }

    /// <summary>
    /// Type resolver for Spectre.Console.Cli to use with dependency injection
    /// </summary>
    internal sealed class TypeResolver : ITypeResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public TypeResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object? Resolve(Type? type)
        {
            if (type == null)
                return null;
            
            // Check if the type is a CommandSettings - these should be instantiated by Spectre.Console
            if (typeof(CommandSettings).IsAssignableFrom(type))
            {
                return null; // Let Spectre.Console handle settings instantiation
            }
            
            // Try to resolve from DI container, return null if not registered
            // This allows Spectre.Console to fall back to default instantiation
            var service = _serviceProvider.GetService(type);
            return service;
        }
    }
}

