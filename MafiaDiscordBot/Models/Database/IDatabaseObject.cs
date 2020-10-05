using System;

namespace MafiaDiscordBot.Models.Database
{
    public interface IDatabaseObject
    {
        DateTime LastAccessed { get; set; }
        bool Filled { get; set; }
    }
}