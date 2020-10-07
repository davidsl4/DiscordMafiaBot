using System;
using MafiaDiscordBot.Converters.Database;
// ReSharper disable UnusedMember.Global

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property)]
    internal abstract class SqlConverterAttribute : Attribute
    {
        private Type Converter { get; }

        protected SqlConverterAttribute(Type converter)
        {
            Converter = converter.IsSubclassOf(typeof(SqlConverter)) ? converter : throw new TypeInitializationException(converter.Name, null);
        }

        public object Read(object input) => ((SqlConverter)Activator.CreateInstance(Converter))?.Read(input);
        public (object returnedValue, object state) ReadWithState(object input)
        {
            var converter = (SqlConverter)Activator.CreateInstance(Converter);
            if (converter != null && converter.UseReadWithState()) return converter.ReadWithState(input);
            return (converter?.Read(input), null);
        }
        public object Write(object obj) => ((SqlConverter)Activator.CreateInstance(Converter))?.Write(obj);
    }
}
