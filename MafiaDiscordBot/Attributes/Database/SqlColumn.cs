using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    class SqlColumnAttribute : Attribute
    {
        public string Name { get; set; }
        public SqlColumnAttribute(string name)
        {
            Name = name;
        }
    }
}
