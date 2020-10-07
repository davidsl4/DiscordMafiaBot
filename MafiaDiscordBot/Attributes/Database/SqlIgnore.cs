using System;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property)]
    internal abstract class SqlIgnoreAttribute : Attribute
    {
    }
}