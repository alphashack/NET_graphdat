using System;

namespace Alphashack.Graphdat.Agent
{
    public class API : IGraphdat
    {
        public class Timer
        {
            private readonly DateTimeOffset _start;
            private DateTimeOffset _stop;

            public TimeSpan Period
            {
                get { return _stop - _start; }
            }

            public double Milliseconds
            {
                get { return Period.TotalMilliseconds; }
            }

            public long Timestamp
            {
                get { return _start.Ticks; }
            }

            public Timer()
            {
                _stop = _start = DateTimeOffset.UtcNow;
            }

            public void Stop()
            {
                _stop = DateTimeOffset.UtcNow;
            }
        }
        
        internal ContextBuilder<Timer> Context;
        private LoggerDelegate _logger;

        public API(LoggerDelegate logger)
        {
            Context = new ContextBuilder<Timer>(() => new Timer(), timer => timer.Stop());
            _logger = logger;
        }

        public void Begin(string name)
        {
            if(Properties.Settings.Default.Debug_BeginContext) _logger(GraphdatLogType.InformationMessage, null, "Begin {0}", name);
            Context.Enter(
                name,
                () => new Timer(),
                timer => timer.Stop());
        }

        public void End(string name = null)
        {
            var timer = Context.Leave(name);
            if (Properties.Settings.Default.Debug_EndContext) _logger(GraphdatLogType.InformationMessage, null, "End {0} ({1}ms)", name, timer.Milliseconds);
        }
    }
}
