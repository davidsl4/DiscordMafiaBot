using System.Diagnostics.CodeAnalysis;

namespace MafiaDiscordBot.Converters.Database
{
    [SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
    [SuppressMessage("ReSharper", "UnusedParameter.Global")]
    internal class SqlConverter
    {
        public virtual object Read(object input) => null;
        public virtual (object returnedValue, object state) ReadWithState(object input) => (null, null);
        public virtual object Write(object obj) => null;

        public virtual bool UseReadWithState() => false;
    }
}
