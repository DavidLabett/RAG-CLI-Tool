using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecondBrain.Services;
using Spectre.Console.Cli;
using System.Globalization;
using System.Threading;

namespace SecondBrain
{
    class Program
    {
        static int Main(string[] args)
        {
            // Set culture to English for help text
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json")
                    .AddEnvironmentVariables() // Environment variables override JSON settings
                    .Build();

                var serviceCollection = new ServiceCollection();
                var serviceProvider = serviceCollection
                    .AddRAGKnowledgeBase(config)
                    .AddCommandApp(config)
                    .BuildServiceProvider();

                var app = serviceProvider.GetRequiredService<CommandApp>();
                return app.Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
