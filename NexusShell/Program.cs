using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexusShell.Interfaces;
using NexusShell.Services;

namespace NexusShell
{
    /// <summary>
    /// Holds the global configuration for the Nexus Shell.
    /// </summary>
    /// <param name="ReposRoot">The root directory where projects are located.</param>
    /// <param name="ConductorRoot">The directory containing the conductor tools and NexusShell.</param>
    /// <param name="Version">The current application version.</param>
    public record NexusSettings(string ReposRoot, string ConductorRoot, string Version);

    /// <summary>
    /// Entry point for the Nexus Command Center AI-OS.
    /// </summary>
    public class Program
    {
        private const string APP_VERSION = "v11.1";

        /// <summary>
        /// Bootstraps the application, configures DI, and starts the UI.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var ui = host.Services.GetRequiredService<IUserInterface>();
            ui.Run();
        }

        /// <summary>
        /// Configures the host and service collection.
        /// </summary>
        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            // Find the conductor root by looking for appsettings.json in current or parent dirs
            // Default to a hardcoded safe fallback if not found, but we expect it in conductor root.
            string baseDir = AppContext.BaseDirectory;
            string configPath = Path.Combine(baseDir, "appsettings.json");
            
            // Fallback for development/production split
            if (!File.Exists(configPath)) {
                configPath = @"C:\Users\flori\source\repos\conductor\appsettings.json";
            }

            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            string reposRoot = config["ReposRoot"] ?? @"C:\Users\flori\source\repos";
            string conductorRoot = config["ConductorRoot"] ?? @"C:\Users\flori\source\repos\conductor";

            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    // Configuration
                    var settings = new NexusSettings(reposRoot, conductorRoot, APP_VERSION);
                    services.AddSingleton(settings);

                    // Core Services
                    services.AddSingleton<ILayoutService, LayoutService>();
                    services.AddSingleton<IHistoryService>(sp => new HistoryService(sp.GetRequiredService<NexusSettings>().ConductorRoot));
                    services.AddSingleton<IProjectService>(sp => new ProjectService(
                        sp.GetRequiredService<NexusSettings>().ReposRoot, 
                        sp.GetRequiredService<IHistoryService>()));
                    services.AddSingleton<ISessionOrchestrator, SessionOrchestrator>(sp => new SessionOrchestrator(sp.GetRequiredService<IHistoryService>()));
                    
                    // Feature Services
                    services.AddSingleton<IMarketingService>(sp => new MarketingService(
                        sp.GetRequiredService<NexusSettings>().ReposRoot,
                        sp.GetRequiredService<ILayoutService>()));
                    services.AddSingleton<IJournalService>(sp => new JournalService(
                        sp.GetRequiredService<NexusSettings>().ReposRoot,
                        sp.GetRequiredService<ILayoutService>()));
                    services.AddSingleton<INewProjectService>(sp => new NewProjectService(
                        sp.GetRequiredService<NexusSettings>(),
                        sp.GetRequiredService<ILayoutService>()));

                    // UI
                    services.AddSingleton<IUserInterface, UserInterface>(sp => new UserInterface(
                        sp.GetRequiredService<NexusSettings>().ReposRoot,
                        sp.GetRequiredService<NexusSettings>().ConductorRoot,
                        sp.GetRequiredService<IProjectService>(),
                        sp.GetRequiredService<IHistoryService>(),
                        sp.GetRequiredService<ISessionOrchestrator>(),
                        sp.GetRequiredService<IMarketingService>(),
                        sp.GetRequiredService<IJournalService>(),
                        sp.GetRequiredService<INewProjectService>(),
                        sp.GetRequiredService<ILayoutService>()));
                });
        }
    }
}
