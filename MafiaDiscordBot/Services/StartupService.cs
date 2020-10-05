using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Discord;
using Serilog;
using System.IO;
using System.Linq;

namespace MafiaDiscordBot.Services
{
    class StartupService
    {
        private struct ServiceProps
        {
            private int? _lastGuildsCount;
            public int lastGuildsCount
            {
                get => _lastGuildsCount ?? -1;
                set => _lastGuildsCount = value;
            }
        }
        
        private readonly DiscordSocketClient _discord;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _services;
        private ServiceProps _props;

        public StartupService(IServiceProvider services)
        {
            _services = services;
            _config = _services.GetRequiredService<IConfigurationRoot>();
            _discord = _services.GetRequiredService<DiscordSocketClient>();
            _props = new ServiceProps();
            
            // load services
            services.GetRequiredService<LocalizationService>().LoadAllAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            services.GetRequiredService<DatabaseService>();
            
            // set AppDomain exit handler
            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                if (_discord.ConnectionState == ConnectionState.Connected)
                    _discord.StopAsync().GetAwaiter().GetResult();
            };

            // configure timers
            var updateGameStatusTimer = new Timer(1000 * 30); // run the timer each 30 seconds 
            updateGameStatusTimer.Elapsed += UpdateGameStatus;

            // set discord connect and disconnect handlers
            _discord.Connected += () => {
                updateGameStatusTimer.Start();
                return Task.CompletedTask;
            };
            _discord.Disconnected += exception =>
            {
                updateGameStatusTimer.Stop();
                return Task.CompletedTask;
            };

            // install bot commands & prepare incoming message handler
            services.GetRequiredService<IncomingMessagesHandlerService>().InstallCommandsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            string discordToken = _config["bot_token"];
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                Log.Fatal("You have to provide a token for your bot");
                Environment.Exit(0);
                return;
            }

            await _discord.LoginAsync(TokenType.Bot, discordToken).ConfigureAwait(false);
            await _discord.StartAsync().ConfigureAwait(false);
        }

        private void UpdateGameStatus(object sender, DateTime signalTime)
        {
            if (_props.lastGuildsCount != _discord.Guilds.Count)
                _discord.SetGameAsync($"Mafia on {_props.lastGuildsCount = _discord.Guilds.Count - _config.GetSection("emote_guilds_ids").GetChildren().Count()} servers").ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
