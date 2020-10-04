using System;
using System.Threading.Tasks;
using Discord.Commands;
using MafiaDiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MafiaDiscordBot.Modules
{
    [Group("dev")]
    public class Developer : ModuleBase<SocketCommandContext>
    {
        private readonly IServiceProvider _service;
        private readonly DatabaseService _database;
        private readonly IConfigurationRoot _config;
        
        public Developer(IServiceProvider service)
        {
            _service = service;
            _database = service.GetRequiredService<DatabaseService>();
            _config = service.GetRequiredService<IConfigurationRoot>();
        }

        [Command("botinfo")]
        public async Task BotInfo()
        {
            
        }
    }
}