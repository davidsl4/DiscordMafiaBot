using System;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class SqlColumnAttribute : Attribute
    {
        public string Name { get; }
        public SqlColumnAttribute(string name)
        {
            Name = name;
        }
    }
}
