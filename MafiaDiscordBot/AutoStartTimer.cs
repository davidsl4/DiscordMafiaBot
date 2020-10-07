using System;

namespace MafiaDiscordBot
{
    public class AutoStartTimer : System.Timers.Timer
    {
        public delegate void ElapsedEventHandler(object sender, DateTime signalTime); 
        public new event ElapsedEventHandler Elapsed;

        private AutoStartTimer()
        {
            base.Elapsed += (sender, args) => Elapsed?.Invoke(this, args.SignalTime);
            Interval = 100;
        }

        public AutoStartTimer(double interval) : this()
        {
            Interval = interval;
        }

        public new void Start()
        {
            Elapsed?.Invoke(this, DateTime.UtcNow);
            base.Start();
        }
    }
}