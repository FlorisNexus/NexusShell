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
    /// <param name="Role">The team role (Admin, Developer-Branch1, Developer-Branch2).</param>
    /// <param name="CloudSyncPath">The mock or real path for cloud memory synchronization.</param>
    public record NexusSettings(string ReposRoot, string ConductorRoot, string Version, string Role, string CloudSyncPath);

    /// <summary>
    /// Entry point for the Nexus Command Center AI-OS.
    /// </summary>
    public class Program
    {
        private const string APP_VERSION = "v19.0";

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
            string baseDir = AppContext.BaseDirectory;
            string configPath = Path.Combine(baseDir, "appsettings.json");

            if (!File.Exists(configPath)) {
                configPath = @"C:\Users\flori\source\repos\NexusShell\NexusShell\appsettings.json";
            }

            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            string reposRoot = config["ReposRoot"] ?? @"C:\Users\flori\source\repos";
            string conductorRoot = config["ConductorRoot"] ?? @"C:\Users\flori\source\repos\conductor";
            string role = config["Role"] ?? "Admin";
            string cloudSyncPath = config["CloudSyncPath"] ?? Path.Combine(reposRoot, "CloudStorage_Mock");

            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    // Configuration
                    var settings = new NexusSettings(reposRoot, conductorRoot, APP_VERSION, role, cloudSyncPath);
                    services.AddSingleton(settings);

                    // Core Services
                    services.AddSingleton<ILayoutService, LayoutService>();
                    services.AddSingleton<IContextService, ContextService>();
                    services.AddSingleton<IRegistryService, RegistryService>();
                    services.AddSingleton<ICliExecutionService, CliExecutionService>();
                    services.AddSingleton<ICloudSyncService, CloudSyncService>();
                    services.AddSingleton<ISessionOrchestrator, SessionOrchestrator>();
                    services.AddSingleton<IHistoryService>(sp => new HistoryService(sp.GetRequiredService<NexusSettings>().ConductorRoot));
                    services.AddSingleton<IProjectService>(sp => new ProjectService(
                        sp.GetRequiredService<NexusSettings>().ReposRoot,
                        sp.GetRequiredService<IHistoryService>(),
                        sp.GetRequiredService<IContextService>()));
                    services.AddSingleton<IChatPersistenceService, ChatPersistenceService>();
                    
                    services.AddSingleton<IPlanService>(sp => new PlanService(
                        sp.GetRequiredService<NexusSettings>(),
                        sp.GetRequiredService<IHistoryService>()));

                    // UI
                    services.AddSingleton<IUserInterface, UserInterface>(sp => new UserInterface(
                        sp.GetRequiredService<NexusSettings>(),
                        sp.GetRequiredService<IProjectService>(),
                        sp.GetRequiredService<IHistoryService>(),
                        sp.GetRequiredService<IRegistryService>(),
                        sp.GetRequiredService<ILayoutService>(),
                        sp.GetRequiredService<ISessionOrchestrator>(),
                        sp.GetRequiredService<IChatPersistenceService>(),
                        sp.GetRequiredService<ICliExecutionService>(),
                        sp.GetRequiredService<ICloudSyncService>(),
                        sp.GetRequiredService<IPlanService>()));
                });
        }
    }
}
