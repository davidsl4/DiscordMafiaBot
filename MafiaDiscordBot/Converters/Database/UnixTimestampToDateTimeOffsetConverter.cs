using System;

namespace MafiaDiscordBot.Converters.Database
{
    internal class UnixTimestampToDateTimeOffsetConverter : SqlConverter
    {
        public override object Read(object input)
        {
            if (input == null) return null;

            var time = input switch
            {
                ulong ul => (long)ul,
                long l => l,
                _ => throw new NotSupportedException()
            };

            return DateTimeOffset.FromUnixTimeSeconds(time);
        }
    }
}
