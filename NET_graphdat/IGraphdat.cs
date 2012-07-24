namespace NET_graphdat
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
        public string name;
        public double timestamp;
        public double responsetime;
        public double cputime;
    };

    public class Sample
    {
        public string method;
        public string uri;
        public double timestamp;
        public double responsetime;
        public double cputime;
        public Context[] context;
    };

    public interface IGraphdat
    {
        void Init(string config, string source, LoggerDelegate logger, object logContext);
        void Term(LoggerDelegate logger, object logContext);
        void Store(Sample sample, LoggerDelegate logger, object logContext);
    }
}
