namespace Alphashack.Graphdat.Agent
{
    public class ErrorGraphdat : IGraphdat
    {
        private bool _usageErrorLogged;

        public ErrorGraphdat()
        {
            HttpModule.SetupEventLog();
        }

        private void LogUsageError()
        {
            if (!_usageErrorLogged)
            {
                HttpModule.Logger(GraphdatLogType.ErrorMessage, null, "Graphdat API not found in HttpContext. HttpModule does not seem to be loaded.");
                _usageErrorLogged = true;
            }
        }

        public void Begin(string name)
        {
            LogUsageError();
        }

        public void End(string name)
        {
            LogUsageError();
        }
    }
}
