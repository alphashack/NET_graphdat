using System.Diagnostics;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    public static class DebugHelper
    {
        public static void LogEntry(EventLog log, string message, EventLogEntryType type = EventLogEntryType.Information)
        {
            if(Properties.Settings.Default.Debug)
                log.WriteEntry(message, type);
        }
    }
}
