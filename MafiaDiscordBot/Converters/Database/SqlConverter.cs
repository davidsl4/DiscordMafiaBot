using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaDiscordBot.Converters.Database
{
    class SqlConverter
    {
        public virtual object Read(object input) => null;
        public virtual (object returnedValue, object state) ReadWithState(object input) => (null, null);
        public virtual object Write(object obj) => null;

        public virtual bool UseReadWithState() => false;
    }
}
