using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MafiaDiscordBot.Services
{
    public class IncomingMessagesHandlerService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly IConfigurationRoot _configuration;
        private readonly LocalizationService _localization;
        private readonly DatabaseService _database;

        private ImmutableArray<Func<SocketMessage, Task>> _messageReceivedEventHandlers = ImmutableArray.Create< Func<SocketMessage, Task>>();
        public event Func<SocketMessage, Task> MessageReceived {
            add => _messageReceivedEventHandlers = _messageReceivedEventHandlers.Add(value);
            remove => _messageReceivedEventHandlers = _messageReceivedEventHandlers.Remove(value);
        }

        public int InstalledModulesCount => _commands?.Modules?.Count() ?? 0;
        
        public IncomingMessagesHandlerService(IServiceProvider services)
        {
            _services = services;
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _configuration = services.GetRequiredService<IConfigurationRoot>();
            _localization = services.GetRequiredService<LocalizationService>();
            _database = services.GetRequiredService<DatabaseService>();
            
            _commands = new CommandService();

            _discord.MessageReceived += messageReceived;
        }
        
        public async Task<IncomingMessagesHandlerService> InstallCommandsAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services).ConfigureAwait(false);
            return this;
        }

        private async Task<IResult> HandleCommandAsync(SocketUserMessage message, int argPos)
        {
            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_discord, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            var res = await _commands.ExecuteAsync(
                context,
                argPos,
                _services);
            if (!res.IsSuccess && res is ExecuteResult er && er.Exception != null) throw er.Exception;
            return res;
        }
        
        private async Task messageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage sum && !sum.Author.IsBot)
            {
                try
                {
                    var prefix = _configuration["default_prefix"];

                    if (arg.Channel is ITextChannel guildTextChannel)
                        prefix = (await _database.Guilds.GetGuild(guildTextChannel).ConfigureAwait(false))?.Prefix ?? prefix;
                    
                    var argPos = -1;
                    if (sum.HasStringPrefix(prefix, ref argPos) ||
                        sum.HasMentionPrefix(_discord.CurrentUser, ref argPos))
                    {
                        var res = await HandleCommandAsync(sum, argPos);
                        if (!res.IsSuccess) argPos = -1;
                    }

                    // The command not found if the argPos is -1
                    if (argPos == -1 && _messageReceivedEventHandlers.Length != 0)
                    {
                        foreach (var handler in _messageReceivedEventHandlers)
                            await handler.Invoke(arg).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e,
                        "Exception thrown when tried to process incoming message {message_id} that has been received on channel {channel_id}",
                        arg.Id, arg.Channel.Id);
                    if (!Directory.Exists("logs/message_exceptions"))
                        Directory.CreateDirectory("logs/message_exceptions");
                    
                    var fileOutput = new StringBuilder();

                    void AppendException(Exception exception, int level, bool isInner = false)
                    {
                        void AppendTabs()
                        {
                            for (var i = 0; i < level - 1; i++)
                                fileOutput.Append("\t");
                        }

                        AppendTabs();
                        fileOutput.AppendLine($"{(isInner ? "Inner exception" : "Exception")} details: {exception.GetType().FullName}");
                        AppendTabs();
                        fileOutput.AppendLine($"\tMessage: {exception.Message}");
                        AppendTabs();
                        fileOutput.AppendLine($"\tStacktrace: {exception.StackTrace}");
                        if (exception.InnerException != null)
                            AppendException(exception.InnerException, level + 1, true);
                    }
                    
                    AppendException(e, 1);
                    fileOutput.AppendLine();
                    fileOutput.AppendLine();
                    fileOutput.AppendLine($"Message ID: {arg.Id}");
                    fileOutput.AppendLine($"Message author: {arg.Author} ({arg.Author.Id})");
                    fileOutput.AppendLine($"Message datetime: {arg.Timestamp:d/M/yyyy hh:mm:ss}");
                    fileOutput.Append("Message channel: ");
                    if (arg.Channel is ITextChannel messageTextChannel)
                    {
                        var guildCurrentUser =
                            messageTextChannel.Guild == null
                                ? null
                                : await messageTextChannel.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                        fileOutput.AppendLine("Guild Text Channel");
                        fileOutput.AppendLine($"\tChannel ID: {messageTextChannel.Id}");
                        fileOutput.AppendLine("\tChannel Guild:");
                        fileOutput.AppendLine($"\t\tID: {messageTextChannel.GuildId}");
                        fileOutput.AppendLine($"\t\tName: {messageTextChannel.Guild?.Name ?? "Unknown"}");
                        fileOutput.AppendLine(
                            $"\t\tBot nickname: {(guildCurrentUser == null ? "Unknown" : guildCurrentUser.Nickname ?? "-")}");
                        fileOutput.Append("\t\tAuthor nickname: ");
                        if (arg.Author is IGuildUser authorGuildObject)
                            fileOutput.AppendLine(authorGuildObject.Nickname ?? "-");
                        if (messageTextChannel.Guild == null)
                            fileOutput.AppendLine("Unknown");
                        else
                        {
                            authorGuildObject = await messageTextChannel.Guild.GetUserAsync(arg.Author.Id)
                                .ConfigureAwait(false);
                            if (authorGuildObject == null)
                                fileOutput.AppendLine("Unknown");
                            else
                                fileOutput.AppendLine(authorGuildObject.Nickname ?? "-");
                        }

                        fileOutput.AppendLine($"\tChannel name: {messageTextChannel.Name}");
                        fileOutput.AppendLine(
                            $"\tChannel permissions: {guildCurrentUser?.GetPermissions(messageTextChannel).RawValue.ToString() ?? "Unknown"}");
                    }
                    else fileOutput.AppendLine($"Text Channel {arg.Channel.Id}");

                    fileOutput.AppendLine();
                    fileOutput.AppendLine();
                    fileOutput.AppendLine(string.IsNullOrWhiteSpace(arg.Content) ? "No message content" : arg.Content);
                    
                    await File.WriteAllTextAsync($"logs/message_exceptions/msg{arg.Id}.log", fileOutput.ToString()).ConfigureAwait(false);

                    string discordMessageLocalization = null;
                    if ((messageTextChannel = arg.Channel as ITextChannel) != null)
                    {
                        discordMessageLocalization = (await _database.Guilds.GetGuild(messageTextChannel.GuildId).ConfigureAwait(false))?.Localization;
                        
                        var guildCurrentUser =
                            messageTextChannel.Guild == null
                                ? null
                                : await messageTextChannel.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                        if (guildCurrentUser?.GetPermissions(messageTextChannel).SendMessages ?? false)
                        {
                            if (!guildCurrentUser.GetPermissions(messageTextChannel).EmbedLinks)
                                goto string_message;
                        }
                    }
                    
                    var (title, description) = _localization.GetLocalizedString(discordMessageLocalization, "MESSAGE_PROCESS_EXCEPTION_TITLE", "MESSAGE_PROCESS_EXCEPTION_DESCRIPTION");
                        
                    var builder = new EmbedBuilder()
                        .WithDescription(description)
                        .WithColor(new Color(uint.Parse(_configuration["color"])))
                        .WithAuthor(author =>
                        {
                            author
                                .WithName(title)
                                .WithIconUrl(_discord.CurrentUser.GetAvatarUrl() ??
                                             _discord.CurrentUser.GetDefaultAvatarUrl());
                        });
                    var embed = builder.Build();
                    await arg.Channel.SendMessageAsync(
                            null,
                            embed: embed)
                        .ConfigureAwait(false);
                    goto skip_string_message;
                        
                    string_message:
                    (title, description) = _localization.GetLocalizedString(discordMessageLocalization, "MESSAGE_PROCESS_EXCEPTION_TITLE", "MESSAGE_PROCESS_EXCEPTION_DESCRIPTION");
                    await arg.Channel.SendMessageAsync($"**{title}**\n{description}").ConfigureAwait(false);
                    
                    skip_string_message: ;
                }
            }
        }
    }
}