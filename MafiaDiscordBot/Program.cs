using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Discord.WebSocket;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace MafiaDiscordBot
{
    internal static class Program
    {
        private static IServiceProvider services { get; set; }

        private static async Task Main()
        {
            #region initialize logger
            Log.Logger = new LoggerConfiguration()
#if VERBOSE_DEBUG // check for the verbose debugging configuration (to output log messages with level below debug)
                .MinimumLevel.Verbose()
#elif DEBUG // if we are currently on the debug configuration log only debug+ level messages
                .MinimumLevel.Debug()
#else // on the release configuration we do not need (probably) all the debug messages, so we can disable them and output only from the information+ levels
                .MinimumLevel.Information()
#endif
                .WriteTo.Console(theme: SystemConsoleTheme.Colored)
                .CreateLogger();
            #endregion

            // create a service collection
            services = new ServiceCollection()
                // add a stopwatch to it
                .AddSingleton(((Func<Stopwatch>)(() =>
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    return sw;
                }))())
                // add a configuration
                .AddSingleton(new ConfigurationBuilder()
                    .AddJsonFile(Path.GetFullPath(Environment.GetEnvironmentVariable("settings file") ?? "config.json", AppContext.BaseDirectory))
                    .SetFileLoadExceptionHandler(e =>
                    {
                        Log.Fatal(e.Exception, "An exception thrown when tried to load configuration file {path}", e.Provider.Source.Path);
                        Environment.Exit(1);
                    })
                    .Build()
                )
                
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<IncomingMessagesHandlerService>()
                .AddSingleton<StartupService>()
                .AddSingleton<LocalizationService>()
                .AddSingleton<SerialKeyValidatorService>()
                .AddSingleton<SystemMetricsService>()
                .AddSingleton<Random>()
                
                // build the service collection
                .BuildServiceProvider();

            await services.GetRequiredService<StartupService>().StartAsync().ConfigureAwait(false);

            await Task.Delay(-1).ConfigureAwait(false);
        }
    }
}