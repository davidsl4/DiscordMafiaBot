using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace MafiaDiscordBot.Modules
{
    [Group("help")]
    public class Help : ModuleBase<SocketCommandContext>
    {
        private static IConfigurationRoot _config;
        private static LocalizationService _localization;
        private static DatabaseService _database;
        
        public Help(IServiceProvider service)
        {
            _config = service.GetRequiredService<IConfigurationRoot>();
            _localization ??= service.GetRequiredService<LocalizationService>();
            _database ??= service.GetRequiredService<DatabaseService>();
        }

        [RequireBotPermission(ChannelPermission.SendMessages)]
        [RequireBotPermission(ChannelPermission.EmbedLinks)]
        [RequireContext(ContextType.Guild)]
        [Command]
        public async Task Main()
        {
            // Get the guild information from the database
            var dbGuild = await _database.Guilds.GetGuild(Context.Guild).ConfigureAwait(false);
            // Translate embed titles & category titles to the guild's prefered language
            // Build the embed object
            var prefix = dbGuild?.Prefix ?? _config["default_prefix"];
            var sb = new StringBuilder();
            string GetCommandsCategoryFieldText(params (string command, string description)[] commands)
            {
                sb.Clear();
                foreach (var (command, description) in commands)
                    sb.AppendLine($"`{prefix}{command}` - {description}");
                return sb.ToString();
            }

            var builder = new EmbedBuilder()
                .WithCurrentTimestamp()
                .WithAuthor(_localization.GetLocalizedString(dbGuild, "HELP_COMMAND_TITLE"),
                    Context.Client.CurrentUser.GetAvatarUrl() ?? Context.Client.CurrentUser.GetDefaultAvatarUrl())
                .WithColor(uint.Parse(_config["color"]))
                .AddField(_localization.GetLocalizedString(dbGuild, "HELP_COMMAND_CATEGORY_GENERAL"),
                    GetCommandsCategoryFieldText(
                        ("help", _localization.GetLocalizedString(dbGuild, "COMMAND_DESCRIPTION_HELP"))
                    ))
                .AddField(_localization.GetLocalizedString(dbGuild, "HELP_COMMAND_CATEGORY_GAME"),
                    GetCommandsCategoryFieldText(
                        ("new-lobby", _localization.GetLocalizedString(dbGuild, "COMMAND_DESCRIPTION_CREATE_GAME_LOBBY")),
                        ("join-lobby", _localization.GetLocalizedString(dbGuild, "COMMAND_DESCRIPTION_JOIN_GAME_LOBBY"))
                    ));
            var embed = builder.Build();
            await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
    }
}