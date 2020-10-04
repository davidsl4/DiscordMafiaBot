using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace MafiaDiscordBot
{
    class Program
    {
        private static IConfigurationRoot _configuration;
        internal static IServiceProvider services { get; private set; }

        static async Task Main(string[] args)
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
                .AddSingleton<Stopwatch>()
                // add a configuration
                .AddSingleton(_configuration = new ConfigurationBuilder()
                    .AddJsonFile(Path.GetFullPath(Environment.GetEnvironmentVariable("settings file") ?? "config.json", AppContext.BaseDirectory))
                    .SetFileLoadExceptionHandler(e =>
                    {
                        Log.Fatal(e.Exception, "An exception thrown when tried to load configuration file {path}", e.Provider.Source.Path);
                        Environment.Exit(1);
                        return;
                    })
                    .Build()
                )
                
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<IncomingMessagesHandlerService>()
                .AddSingleton<StartupService>()
                .AddSingleton<LocalizationService>()
                .AddSingleton<SerialKeyValidatorService>()
                
                // add modules to the service collection
                .AddSingleton<Modules.Developer>()
                // build the service collection
                .BuildServiceProvider();

            await services.GetRequiredService<StartupService>().StartAsync().ConfigureAwait(false);

            await Task.Delay(-1).ConfigureAwait(false);
        }
    }
}