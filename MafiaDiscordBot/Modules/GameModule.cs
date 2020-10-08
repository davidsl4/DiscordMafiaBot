using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MafiaDiscordBot.Extensions;
using MafiaDiscordBot.Models;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace MafiaDiscordBot.Modules
{
    public class GameModule : ModuleBase<SocketCommandContext>
    {
        private class GuildLobbyContext
        {
            public GuildLobbyContext(IGuild guild, IUser creator)
            {
                guildId = guild.Id;
                creatorId = creator.Id;
            }
            
            internal enum State
            {
                WaitingForMembers,
                WaitingForLobbyCreator,
                Playing,
                Paused
            }
            
            public readonly ulong guildId;
            public readonly ulong creatorId;
            public ulong killersChannelId;
            public ulong residentsChannelId;
            public ulong copsChannelId;
            public readonly ThreadSafeList<ulong> memberIds = new ThreadSafeList<ulong>();
            public int joinCode;
            public State state;
        }
        
        private static LocalizationService _localization;
        private static DatabaseService _db;
        private static ThreadSafeList<GuildLobbyContext> _guildLobbyContexts;
        private static Random _random;
        private static IConfigurationRoot _config;
        
        public GameModule(IServiceProvider service)
        {
            _localization ??= service.GetRequiredService<LocalizationService>();
            _db ??= service.GetRequiredService<DatabaseService>();
            _guildLobbyContexts ??= new ThreadSafeList<GuildLobbyContext>();
            _random ??= service.GetRequiredService<Random>();
            _config ??= service.GetRequiredService<IConfigurationRoot>();
        }

        [Command("new-lobby")]
        [Alias("create-lobby")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task CreateNewLobby()
        {
            var guildDatabase = await _db.Guilds.GetGuild(Context.Guild).ConfigureAwait(false);
            if (!Context.Guild.CurrentUser.GetPermissions(Context.Channel as IGuildChannel).EmbedLinks)
            {
                var (title, body, embedLinksPermissions) = _localization.GetLocalizedString(guildDatabase,
                    "BOT_PERMISSIONS_EXCEPTION_TITLE", "BOT_PERMISSIONS_EXCEPTION_BODY",
                    "BOT_PERMISSIONS_EXCEPTION_BODY_VAR:embed_links");
                body = body.Replace("{permissions}",
                    $"\U0000200B \U0000200B \U0000200B \U0000200B {embedLinksPermissions}");
                await Context.Channel.SendMessageAsync($"**{title}**\n{body}").ConfigureAwait(false);
                return;
            }
            ICategoryChannel gameCategory = guildDatabase == null || guildDatabase.GameCategoryId == 0 ? null : Context.Guild.GetCategoryChannel(guildDatabase.GameCategoryId);
            if (gameCategory == null)
            {
                await Context.Channel
                    .SendMessageAsync(_localization.GetLocalizedString(guildDatabase, "GUILD_NOT_CONFIGURED_EXCEPTION"))
                    .ConfigureAwait(false);
                if (guildDatabase != null && guildDatabase.GameCategoryId != 0)
                {
                    guildDatabase.GameCategoryId = 0;
                    await _db.Guilds.SaveGuild(guildDatabase).ConfigureAwait(false);
                }
            }
            else if (_guildLobbyContexts.Any(lobby =>
                lobby.creatorId == Context.User.Id || lobby.memberIds.Contains(Context.User.Id)))
                await Context.Channel
                    .SendMessageAsync(_localization.GetLocalizedString(guildDatabase, "ALREADY_MEMBER_OF_LOBBY_EXCEPTION")
                        .Replace("{action}",
                            _localization.GetLocalizedString(guildDatabase,
                                "ALREADY_MEMBER_OF_LOBBY_EXCEPTION_VAR:action:create"))
                        .Replace("{command_prefix}", guildDatabase.Prefix ?? _config["default_prefix"]))
                    .ConfigureAwait(false);
            else
            {
                var lobby = new GuildLobbyContext(Context.Guild, Context.User);
                do lobby.joinCode = _random.Next(10000, 99999 + 1);
                while (_guildLobbyContexts.Count > 0 &&
                       _guildLobbyContexts.Any(l => l.guildId == Context.Guild.Id && l.joinCode == lobby.joinCode));
                _guildLobbyContexts.Add(lobby);
                var builder = new EmbedBuilder()
                    .WithCurrentTimestamp()
                    .WithColor(uint.Parse(_config["color"]))
                    .WithAuthor(
                        _localization.GetLocalizedString(guildDatabase, "GAME_LOBBY_CREATED_NOTIFICATION_TITLE"),
                        Context.Client.CurrentUser.FixGetAvatarUrl())
                    .WithDescription(_localization.GetLocalizedString(guildDatabase,
                        "GAME_LOBBY_CREATED_NOTIFICATION_BODY").Replace("{command_prefix}",
                        guildDatabase.Prefix ?? _config["default_prefix"])
                        .Replace("{code}", lobby.joinCode.ToString()));
                var embed = builder.Build();
                await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            }
        }

        [Command("join-lobby")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task JoinLobby(int? code = null)
        {
            var guildDatabase = await _db.Guilds.GetGuild(Context.Guild).ConfigureAwait(false);
            if (!Context.Guild.CurrentUser.GetPermissions(Context.Channel as IGuildChannel).EmbedLinks)
            {
                var (title, body, embedLinksPermissions) = _localization.GetLocalizedString(guildDatabase,
                    "BOT_PERMISSIONS_EXCEPTION_TITLE", "BOT_PERMISSIONS_EXCEPTION_BODY",
                    "BOT_PERMISSIONS_EXCEPTION_BODY_VAR:embed_links");
                body = body.Replace("{permissions}",
                    $"\U0000200B \U0000200B \U0000200B \U0000200B {embedLinksPermissions}");
                await Context.Channel.SendMessageAsync($"**{title}**\n{body}").ConfigureAwait(false);
                return;
            }
            ICategoryChannel gameCategory = guildDatabase == null || guildDatabase.GameCategoryId == 0 ? null : Context.Guild.GetCategoryChannel(guildDatabase.GameCategoryId);
            if (gameCategory == null)
            {
                await Context.Channel
                    .SendMessageAsync(_localization.GetLocalizedString(guildDatabase, "GUILD_NOT_CONFIGURED_EXCEPTION"))
                    .ConfigureAwait(false);
                if (guildDatabase != null && guildDatabase.GameCategoryId != 0)
                {
                    guildDatabase.GameCategoryId = 0;
                    await _db.Guilds.SaveGuild(guildDatabase).ConfigureAwait(false);
                }
            }
            else if (code == null)
            {
                var usage =
                    $"`{guildDatabase.Prefix ?? _config["default_prefix"]}join-lobby [{_localization.GetLocalizedString(guildDatabase, "COMMAND_JOIN_GAME_LOBBY_ARGS:code")}]`";
                var builder = new EmbedBuilder()
                    .WithCurrentTimestamp()
                    .WithColor(uint.Parse(_config["color"]))
                    .WithAuthor(
                        _localization.GetLocalizedString(guildDatabase,
                            "INVALID_COMMAND_USAGE_MISSING_ARG_EXCEPTION_TITLE"),
                        Context.Client.CurrentUser.FixGetAvatarUrl())
                    .WithDescription(_localization
                        .GetLocalizedString(guildDatabase, "INVALID_COMMAND_USAGE_MISSING_ARG_EXCEPTION_BODY").Replace(
                            "{hint}", usage
                        ));
                var embed = builder.Build();
                await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            }
            else if (_guildLobbyContexts.Any(lobby =>
                lobby.creatorId == Context.User.Id || lobby.memberIds.Contains(Context.User.Id)))
                await Context.Channel
                    .SendMessageAsync(_localization.GetLocalizedString(guildDatabase, "ALREADY_MEMBER_OF_LOBBY_EXCEPTION")
                        .Replace("{action}",
                            _localization.GetLocalizedString(guildDatabase,
                                "ALREADY_MEMBER_OF_LOBBY_EXCEPTION_VAR:action:join"))
                        .Replace("{command_prefix}", guildDatabase.Prefix ?? _config["default_prefix"]))
                    .ConfigureAwait(false);
            else
            {
                GuildLobbyContext lobby;
                EmbedBuilder builder;
                Embed embed;
                if ((lobby = _guildLobbyContexts.FirstOrDefault(l => l.joinCode == code.Value)) == null)
                {
                    builder = new EmbedBuilder()
                        .WithCurrentTimestamp()
                        .WithColor(uint.Parse(_config["color"]))
                        .WithAuthor(
                            _localization.GetLocalizedString(guildDatabase,
                                "GAME_LOBBY_JOIN_LOBBY_EXCEPTION_TITLE"),
                            Context.Client.CurrentUser.FixGetAvatarUrl())
                        .WithDescription(_localization.GetLocalizedString(guildDatabase,
                            "GAME_LOBBY_JOIN_LOBBY_NOT_FOUND_EXCEPTION"));
                    embed = builder.Build();
                    await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                    return;
                }
                lobby.memberIds.Add(Context.User.Id);
                builder = new EmbedBuilder()
                    .WithCurrentTimestamp()
                    .WithColor(uint.Parse(_config["color"]))
                    .WithAuthor(
                        _localization.GetLocalizedString(guildDatabase,
                            "GAME_LOBBY_JOIN_LOBBY_SUCCESS_TITLE"),
                        Context.Client.CurrentUser.FixGetAvatarUrl())
                    .WithDescription(_localization.GetLocalizedString(guildDatabase,
                        "GAME_LOBBY_JOIN_LOBBY_SUCCESS_BODY").Replace("{command_prefix}",
                        guildDatabase.Prefix ?? _config["default_prefix"]))
                    .WithThumbnailUrl(Context.User.FixGetAvatarUrl(size: 256));
                embed = builder.Build();
                await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            }
        }
    }
}