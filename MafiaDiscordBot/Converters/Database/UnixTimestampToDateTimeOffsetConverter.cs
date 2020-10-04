using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaDiscordBot.Converters.Database
{
    class UnixTimestampToDateTimeOffsetConverter : SqlConverter
    {
        public override object Read(object input)
        {
            if (input == null) return null;

            long time = 0;

            if (input is ulong) time = (long)(ulong)input;
            else if (input is long) time = (long)input;
            else throw new NotSupportedException();

            return DateTimeOffset.FromUnixTimeSeconds(time);
        }
    }
}
