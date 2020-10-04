using MafiaDiscordBot.Attributes.Database;

namespace MafiaDiscordBot.Models.Database
{
    public class Guild
    {
        [SqlColumn("id")]
        public ulong ID { get; set; }

        [SqlColumn("game_category")]
        public ulong GameCategoryId { get; set; }

        [SqlColumn("prefix")]
        public string Prefix { get; set; }
    }
}