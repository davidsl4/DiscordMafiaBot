using System;
using MafiaDiscordBot.Attributes.Database;

namespace MafiaDiscordBot.Models.Database
{
    public class Guild : IDatabaseObject
    {
        [SqlColumn("id")]
        public ulong ID { get; set; }

        [SqlColumn("game_category")]
        public ulong GameCategoryId { get; set; }

        [SqlColumn("prefix")]
        public string Prefix { get; set; }
        
        [SqlColumn("localization")]
        public string Localization { get; set; }

        public DateTime LastAccessed { get; set; }
        public bool Filled { get; set; }
    }
}