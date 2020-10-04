using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    class SqlIgnoreAttribute : Attribute
    {
    }
}
