using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MsgPack;

namespace Alphashack.Graphdat.Agent
{
    public class Connect : IConnect
    {
        public const bool VerboseLogging = false;
        private const int GraphdatWorkerLoopSleep = 100;

        private string _source;
        private Socket _socket;
        private EventWaitHandle _termHandle;
        private Thread _thread;
        private ConcurrentQueue<Item> _queue;
        private DateTime _lastHeartbeat = DateTime.Now;
        private readonly TimeSpan _heartbeatInterval = new TimeSpan(0, 0, 30);

        private class Item
        {
            public string Type;
            public string Source;
            public string Route;
            public double ResponseTime;
            public double Timestamp;
            public double CpuTime;
            public IList<Context> Context;
            internal LoggerDelegate Logger;
            internal object LogContext;

            public override string ToString()
            {
                return String.Format("type => {0}, Souce => {1}, Route => {2}, ResponseTime => {3}", Type, Source, Route, ResponseTime);
            }
        };

        public void Init(string config, string source, LoggerDelegate logger, object logContext = null)
        {
            _socket = new Socket(config, logger, logContext);
            _source = source;
            _queue = new ConcurrentQueue<Item>();
            _thread = new Thread(Worker) {IsBackground = true};
            _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _thread.Start();

            if (VerboseLogging)
            {
                logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: init '{0}'", config);
            }
        }

        public void Term(LoggerDelegate logger, object logContext = null)
        {
            if (_termHandle != null) _termHandle.Set();
            if (_thread != null) _thread.Join();
            _socket.Term(logger, logContext);

            if (Connect.VerboseLogging)
            {
                logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: term");
            }
        }

        public void Store(Sample sample, LoggerDelegate logger, object logContext = null)
        {
            var item = new Item
                          {
                              Type = "Sample",
                              Source = _source,
                              Route = !string.IsNullOrEmpty(sample.Method) ? string.Format("{0} {1}", sample.Method, sample.Uri) : sample.Uri,
                              Timestamp = sample.Timestamp,
                              ResponseTime = sample.ResponseTime,
                              CpuTime = sample.CpuTime,
                              Context = sample.Context,
                              Logger = logger,
                              LogContext = logContext
                          };
            _queue.Enqueue(item);
        }

        private void Send(Item item)
        {
            var stream = new MemoryStream();
            var packer = Packer.Create(stream);
            var hasContext = (item.Context != null && item.Context.Count > 0);
            var mapSize = hasContext ? 7 : 6;
            packer.PackMapHeader(mapSize); // timestamp, type, route, responsetime, cputime, source, context?
	        // timestamp
	        packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("timestamp"));
	        packer.Pack(item.Timestamp);
	        // type
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("type"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.Type));
            // route
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("route"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.Route));
            // responsetime
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("responsetime"));
            packer.Pack(item.ResponseTime);
            // cputime
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("cputime"));
            packer.Pack(item.CpuTime);
	        // source
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("source"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.Source));
            // context
            if (hasContext)
            {
                packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("context"));
                packer.PackArrayHeader(item.Context.Count);
                foreach (var context in item.Context)
                {
                    packer.PackMapHeader(4); // name, timestamp, responsetime, cputime
                    // name
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("name"));
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(context.Name));
                    // timestamp
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("timestamp"));
                    packer.Pack(context.Timestamp);
                    // responsetime
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("responsetime"));
                    packer.Pack(context.ResponseTime);
                    // cputime
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("cputime"));
                    packer.Pack(context.CpuTime);
                }
            }

            _socket.Send(stream.GetBuffer(), stream.Length, item.Logger, item.LogContext);
            stream.Close();
        }

        private void Worker()
        {
            int sleep;
            var hasSentData = false;
            do
            {
                Item item;
                if(_queue.TryDequeue(out item))
                {
                    Send(item);
                    hasSentData = true;
                    sleep = 0;
                }
                else
                {
                    Heartbeat(hasSentData);
                    sleep = GraphdatWorkerLoopSleep;
                    hasSentData = false;
                }
            } while (!_termHandle.WaitOne(sleep));
        }

        private void Heartbeat(bool hasSentData)
        {
            var now = DateTime.Now;

            if(!hasSentData && now - _lastHeartbeat > _heartbeatInterval)
            {
                SendHeartbeat();
                hasSentData = true;
            }

            if (hasSentData)
                _lastHeartbeat = now;
        }

        private void SendHeartbeat()
        {
            _socket.SendHeartbeat();
        }
    }
}
