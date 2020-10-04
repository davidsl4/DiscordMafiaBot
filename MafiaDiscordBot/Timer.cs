using System;
using System.Timers;

namespace MafiaDiscordBot
{
    public class Timer : System.Timers.Timer
    {
        public bool InvokeOnStart { get; set; } = true;
        public delegate void ElapsedEventHandler(object sender, DateTime signalTime); 
        public new event ElapsedEventHandler Elapsed;

        public Timer()
        {
            base.Elapsed += (sender, args) => Elapsed?.Invoke(this, args.SignalTime);
            Interval = 100;
        }

        public Timer(double interval) : this()
        {
            Interval = interval;
        }

        public new void Start()
        {
            if (InvokeOnStart)
                Elapsed?.Invoke(this, DateTime.UtcNow);
            base.Start();
        }
    }
}