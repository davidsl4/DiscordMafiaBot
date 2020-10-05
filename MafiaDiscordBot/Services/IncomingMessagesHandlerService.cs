using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MafiaDiscordBot.Services
{
    public class IncomingMessagesHandlerService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly IConfigurationRoot _configuration;
        
        private ImmutableArray<Func<SocketMessage, Task>> _messageReceivedEventHandlers = ImmutableArray.Create< Func<SocketMessage, Task>>();
        public event Func<SocketMessage, Task> MessageReceivedEventHandlers {
            add => _messageReceivedEventHandlers = _messageReceivedEventHandlers.Add(value);
            remove => _messageReceivedEventHandlers = _messageReceivedEventHandlers.Remove(value);
        }

        public int InstalledModulesCount => _commands?.Modules?.Count() ?? 0;
        
        public IncomingMessagesHandlerService(IServiceProvider services)
        {
            _services = services;
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _configuration = services.GetRequiredService<IConfigurationRoot>();
            
            _commands = new CommandService();

            _discord.MessageReceived += messageReceived;
        }
        
        public async Task<IncomingMessagesHandlerService> InstallCommandsAsync()
        {
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: _services).ConfigureAwait(false);
            return this;
        }

        private async Task<IResult> HandleCommandAsync(SocketUserMessage message, int argPos)
        {
            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_discord, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            var res = await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: _services);
            if (!res.IsSuccess && res is ExecuteResult er && er.Exception != null) throw er.Exception;
            return res;
        }
        
        private async Task messageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage sum && !sum.Author.IsBot)
            {
                int argPos = -1;
                if (sum.HasStringPrefix(_configuration["default_prefix"], ref argPos) || sum.HasMentionPrefix(_discord.CurrentUser, ref argPos))
                {
                    try
                    {
                        var res = await HandleCommandAsync(sum, argPos);
                        if (!res.IsSuccess) argPos = -1;
                    }
                    catch (Exception e)
                    {
                        
                    }
                }

                // The command not found if the argPos is -1
                if (argPos == -1 && _messageReceivedEventHandlers.Length != 0)
                {
                    for (int i = 0; i < _messageReceivedEventHandlers.Length; i++)
                        await _messageReceivedEventHandlers[i].Invoke(arg).ConfigureAwait(false);
                }
            }
        }
    }
}