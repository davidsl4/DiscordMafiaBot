using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MafiaDiscordBot.Attributes.Discord.Commands
{
    public class RequireDeveloper : PreconditionAttribute
    {
        private static IEnumerable<IConfigurationSection> _developerIdsSection;
        public override string ErrorMessage { get; set; }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            _developerIdsSection ??= services.GetRequiredService<IConfigurationRoot>().GetSection("developer_ids")
                .GetChildren();

            return Task.FromResult(
                _developerIdsSection.Any(dev => ulong.TryParse(dev.Value, out var id) && id == context.User.Id)
                    ? PreconditionResult
                        .FromSuccess()
                    : PreconditionResult.FromError(ErrorMessage ??
                                                   "Command can only be run by the developer of the bot."));
        }
    }
}