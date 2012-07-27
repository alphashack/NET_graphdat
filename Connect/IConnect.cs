using System.Collections.Generic;

namespace Alphashack.Graphdat.Agent
{
    public enum GraphdatLogType
    {
        SuccessMessage,
        ErrorMessage,
        WarningMessage,
        InformationMessage
    };

    public delegate void LoggerDelegate(GraphdatLogType type, object user, string fmt, params object[] args);

    public class Context
    {
        public string Name;
        public long Timestamp;
        public double ResponseTime;
        public double CpuTime;
    };

    public class Sample
    {
        public string Method;
        public string Uri;
        public double Timestamp;
        public double ResponseTime;
        public double CpuTime;
        public IList<Context> Context;
    };

    public interface IConnect
    {
        void Init(string config, string source, LoggerDelegate logger, object logContext = null);
        void Term(LoggerDelegate logger, object logContext = null);
        void Store(Sample sample, LoggerDelegate logger, object logContext = null);
    }
}
