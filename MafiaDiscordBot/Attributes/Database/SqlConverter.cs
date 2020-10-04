using System;
using MafiaDiscordBot.Converters.Database;

namespace MafiaDiscordBot.Attributes.Database
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    class SqlConverterAttribute : Attribute
    {
        Type _converter;
        Type Converter { get => _converter; }

        public SqlConverterAttribute(Type converter)
        {
            _converter = converter.IsSubclassOf(typeof(SqlConverter)) ? converter : throw new TypeInitializationException(converter.Name, null);
        }

        public object Read(object input) => ((SqlConverter)Activator.CreateInstance(Converter)).Read(input);
        public (object returnedValue, object state) ReadWithState(object input)
        {
            var converter = ((SqlConverter)Activator.CreateInstance(Converter));
            if (converter.UseReadWithState()) return converter.ReadWithState(input);
            return (converter.Read(input), null);
        }
        public object Write(object obj) => ((SqlConverter)Activator.CreateInstance(Converter)).Write(obj);
    }
}
