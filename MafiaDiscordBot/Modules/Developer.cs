using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MafiaDiscordBot.Attributes.Discord.Commands;
using MafiaDiscordBot.Extensions;
using MafiaDiscordBot.Properties;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace MafiaDiscordBot.Modules
{
    [Group("dev")]
    [RequireDeveloper]
    public class Developer : ModuleBase<SocketCommandContext>
    {
        private static IConfigurationRoot _config;
        private static SerialKeyValidatorService _serialKeyValidator;
        private static IncomingMessagesHandlerService _incomingMessagesHandler;
        private static Stopwatch _uptime;
        private static SystemMetricsService _systemMetrics;
        private static DatabaseService _database;
        
        public Developer(IServiceProvider service)
        {
            _config ??= service.GetRequiredService<IConfigurationRoot>();
            _serialKeyValidator ??= service.GetRequiredService<SerialKeyValidatorService>();
            _incomingMessagesHandler ??= service.GetRequiredService<IncomingMessagesHandlerService>();
            _uptime ??= service.GetRequiredService<Stopwatch>();
            _systemMetrics ??= service.GetRequiredService<SystemMetricsService>();
            _database ??= service.GetRequiredService<DatabaseService>();
        }

        // ReSharper disable once StringLiteralTypo
        [Command("botinfo")]
        public async Task BotInfo()
        {
            var builder = new EmbedBuilder()
                .WithTitle("Bot Information for Developer")
                .WithDescription("Make sure you know what you do when you maintain the bot with this command.")
                .WithColor(new Color(uint.Parse(_config["color"])))
                .WithCurrentTimestamp()
                .WithAuthor(author =>
                {
                    author
                        .WithName($"Requested by {Context.User}")
                        .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());
                })
                .AddInlineField("Bot Version", AssemblyInfo.BOT_VERSION)
                .AddInlineField("Features (Check documentation)", ((Func<string>) (() =>
                {
                    const string
                        ORIGINAL = "<:owner:603479419595522049>",
                        HOSTED_BY_DEVS = "<:verified:761961109011169280>",
                        PREMIUM_AVAILABLE = "<:coin:762424024537759785> ",
                        OWNED_BY_DEVS = "<:badgeStaff:440460762377486357>";

                    static bool HasPerk(SerialKeyValidatorService.KeyStatus perk) =>
                        (_serialKeyValidator.GetKeyStatus() & perk) == perk;

                    if (!HasPerk(SerialKeyValidatorService.KeyStatus.KeyVerified)) return "\u200B";
                    var sb = new StringBuilder();
                    if (HasPerk(SerialKeyValidatorService.KeyStatus.Original)) sb.Append(ORIGINAL + " ");
                    if (HasPerk(SerialKeyValidatorService.KeyStatus.HostedByDevs)) sb.Append(HOSTED_BY_DEVS + " ");
                    if (HasPerk(SerialKeyValidatorService.KeyStatus.OwnedByDevs)) sb.Append(OWNED_BY_DEVS + " ");
                    if (HasPerk(SerialKeyValidatorService.KeyStatus.Premium)) sb.Append(PREMIUM_AVAILABLE + " ");

                    return sb.ToString();
                }))())
                .AddInlineField("Service Guilds Count",
                    _config.GetSection("emote_guilds_ids").GetChildren().Count().ToString())
                .AddInlineField("Developers", _config.GetSection("developer_ids").GetChildren().Count().ToString())
                .AddInlineField("Localizations", _config.GetSection("localizations").GetChildren().Count().ToString())
                .AddInlineField("Modules", _incomingMessagesHandler.InstalledModulesCount.ToString())
                .AddInlineField("Uptime", ((Func<string>) (() =>
                {
                    var daySuffix = _uptime.Elapsed.Days == 1 ? "" : "s";
                    return
                        $"{_uptime.Elapsed.Days} day{daySuffix}" +
                        " / " +
                        $"{(ulong) _uptime.Elapsed.TotalHours}({_uptime.Elapsed.Hours}):{_uptime.Elapsed.Minutes:D2}:{_uptime.Elapsed.Seconds:D2}";
                }))())
                .AddInlineField("RAM", ((Func<string>) (() =>
                {
                    static string FormatSize(long bytes)
                    {
                        const long
                            Kb = 1024,
                            Mb = Kb * 1024,
                            Gb = Mb * 1024;
                        
                        if (bytes >= Gb) return $"{(float) bytes / Gb:0.##} GB";
                        if (bytes >= Mb) return $"{(float) bytes / Mb:0.##} MB";
                        // ReSharper disable once ConvertIfStatementToReturnStatement
                        if (bytes >= Kb) return $"{(float) bytes / Kb:0.##} KB";
                        return $"{bytes} bytes";
                    }

                    long bot;
                    using (var proc = Process.GetCurrentProcess())
                        bot = proc.PrivateMemorySize64;
                    var used = _systemMetrics.RamUsed;
                    var free = _systemMetrics.RamFree;
                    var total = _systemMetrics.RamTotal;
                    
                    var usedPercentage = (float) used / total * 100;
                    var freePercentage = (float) free / total * 100;

                    return
                        $"Bot: {FormatSize(bot)}\nUsed: {FormatSize(used)} ({usedPercentage:0.##}%)\nFree: {FormatSize(free)} ({freePercentage:0.##}%)\nTotal: {FormatSize(total)}";
                }))())
                //.AddInlineField("CPU", "Bot: 0%\nUsed: 31%\nFree: 69%")
                .AddInlineField("Running machine", ((Func<string>) (() =>
                {
                    const string
                        CHECKMARK = "<:checkmark:603479419964620810>",
                        XMARK = "<:xmark:603479420858269696>";
                    
                    var sb = new StringBuilder();
                    sb.AppendLine($"OS: {_systemMetrics.OSName} {(IntPtr.Size == 8 ? "x64" : "x86")}");
                    sb.AppendLine($"OS Version: {_systemMetrics.OSVersion}");
                    sb.AppendLine($"Is Windows: {(SystemMetricsService.IsWindows ? CHECKMARK : XMARK)}");
                    sb.AppendLine($"Is Linux: {(SystemMetricsService.IsLinux ? CHECKMARK : XMARK)}");

                    sb.Append("Uptime: ");
                    var OSUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                    var daySuffix = OSUptime.Days == 1 ? "" : "s";
                    sb.AppendLine($"{OSUptime.Days} day{daySuffix}" + 
                                  " / " +
                                  $"{OSUptime.Hours}:{OSUptime.Minutes:D2}:{OSUptime.Seconds:D2}");

                    sb.AppendLine($"User: {Environment.UserName}");

                    return sb.ToString();
                }))());
            var embed = builder.Build();
            await Context.Channel.SendMessageAsync(
                    null,
                    embed: embed)
                .ConfigureAwait(false);
        }

        // ReSharper disable once StringLiteralTypo
        [Command("gcclear")]
        public async Task ClearGarbageCollector()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Context.Channel.SendMessageAsync("Done. <:checkmark:603479419964620810>").ConfigureAwait(false);
        }

        // ReSharper disable once StringLiteralTypo
        [Command("dbcacheclear")]
        public async Task ClearDatabaseCache()
        {
            _database.ClearCachedData();
            await Context.Channel.SendMessageAsync("Done. <:checkmark:603479419964620810>").ConfigureAwait(false);
        }
    }
}