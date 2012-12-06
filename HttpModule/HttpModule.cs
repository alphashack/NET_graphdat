using System;
using System.Diagnostics;
using System.Web;
using System.Linq;
using System.Dynamic;
using System.Collections.Generic;

namespace Alphashack.Graphdat.Agent
{
    public class HttpModule : IHttpModule
    {
        private readonly Connect _agentConnect;
        private const string EventLogSource = "Alphashack.Graphdat.Agent.HttpModule";
        private const string EventLogName = "Graphdat_HttpModule";

        public HttpModule()
        {
            _agentConnect = new Connect();

            SetupEventLog();
        }

        internal static void SetupEventLog()
        {
            if (EventLog.SourceExists(EventLogSource))
            {
                var eventLog = new EventLog { Source = EventLogSource };
                if (eventLog.Log != EventLogName)
                {
                    EventLog.DeleteEventSource(EventLogSource);
                }
            }
            if (!EventLog.SourceExists(EventLogSource))
            {
                EventLog.CreateEventSource(EventLogSource, EventLogName);
                EventLog.WriteEntry(EventLogSource, String.Format("Event Log Created '{0}'/'{1}'", EventLogName, EventLogSource), EventLogEntryType.Information);
            }
        }

        public String ModuleName
        {
            get { return "GraphdatHttpModule"; }
        }

        public static string ContextItemKey
        {
            get { return "Graphdat"; }
        }

        public static void Logger(GraphdatLogType type, object user, string fmt, params object[] args)
        {
            EventLogEntryType logType;
            switch(type)
            {
                case GraphdatLogType.SuccessMessage:
                    logType = EventLogEntryType.SuccessAudit;
                    break;
                case GraphdatLogType.ErrorMessage:
                    logType = EventLogEntryType.Error;
                    break;
                case GraphdatLogType.WarningMessage:
                    logType = EventLogEntryType.Warning;
                    break;
                case GraphdatLogType.InformationMessage:
                    logType = EventLogEntryType.Information;
                    break;
                default:
                    logType = EventLogEntryType.Error;
                    break;
            }
            EventLog.WriteEntry(EventLogSource, string.Format(fmt, args), logType);
        }

        public void Init(HttpApplication application)
        {
            // Setup agent connect
            _agentConnect.Init(Properties.Settings.Default.Port.ToString(), Properties.Settings.Default.Source, Logger);

            // Hook begin and end request for timing
            application.BeginRequest += BeginRequest;
            application.EndRequest += EndRequest;
        }

        public void BeginRequest(Object source, EventArgs e)
        {
            var application = (HttpApplication)source;
            var context = application.Context;

            // Create API (includes context) for request
            var graphdat = new API(Logger);
            // Attach to HttpContext
            context.Items[ContextItemKey] = graphdat;
        }

        public Action<API.Timer, dynamic> build = (timer, data) =>
        {
            data.Timestamp = timer.Timestamp;
            data.ResponseTime = timer.Milliseconds;
            data.CpuTime = (double)0;
        };

        public void EndRequest(Object source, EventArgs e)
        {
            var application = (HttpApplication) source;
            var httpContext = application.Context;

            if (!httpContext.Items.Contains(ContextItemKey))
            {
                Logger(GraphdatLogType.ErrorMessage, null, "Graphdat API not found in HttpContext.");
                return;
            }

            var graphdat = httpContext.Items[ContextItemKey] as API;
            if (graphdat == null)
            {
                Logger(GraphdatLogType.ErrorMessage, null, "Graphdat API not found (incorrect type) in HttpContext.");
                return;
            }

            API.Timer rootTimer;
            if(!graphdat.Context.Validate())
            {
                if (!Properties.Settings.Default.Suppress_ContextPopAutomatic) Logger(GraphdatLogType.WarningMessage, null, "Popping context automatically, you have not ended each context you created, this might be an error (you can suppress this warning: Suppress_ContextPopAutomatic).");
                rootTimer = graphdat.Context.Exit();
            }
            else
            {
                rootTimer = graphdat.Context.Done();
            }

            // Complete the timing
            var context = graphdat.Context.Flatten(build);

            // Send the sample
            var contexts = context.Select((dynamic obj) => new Context {
                Name = obj.Name,
                Timestamp = obj.Timestamp,
                ResponseTime = obj.ResponseTime,
                CpuTime = obj.CpuTime,
                CallCount = GetCallCount(context, obj.Name)
            });

            var sample = new Sample {
                 Method = httpContext.Request.HttpMethod,
                 Uri = httpContext.Request.Url.AbsoluteUri,
                 Host = httpContext.Request.Url.Host,
                 Timestamp = rootTimer.Timestamp,
                 ResponseTime = rootTimer.Milliseconds,
                 CpuTime = 0,
                 Context = contexts.ToArray()
             };

            _agentConnect.Store(sample, Logger);
        }

        public int GetCallCount(List<ExpandoObject> context, string name) 
        {
            return context.Count((dynamic obj) => obj.Name == name);
        }

        public static bool TryGetGraphdat(out IGraphdat graphdat)
        {
            graphdat = null;

            var httpContext = HttpContext.Current;
            if (httpContext == null) return false;
            
            if (!httpContext.Items.Contains(ContextItemKey)) return false;

            var api = httpContext.Items[ContextItemKey] as API;
            if (api == null) return false;

            graphdat = api;
            return true;
        }

        public static IGraphdat SafeGetGraphdat()
        {
            IGraphdat graphdat;
            if(!TryGetGraphdat(out graphdat))
            {
                graphdat = new ErrorGraphdat();
            }
            return graphdat;
        }

        public void Dispose()
        {
            _agentConnect.Term(Logger);
        }

    }
}
