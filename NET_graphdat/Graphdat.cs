using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using MsgPack;

namespace NET_graphdat
{
    public class Graphdat : IGraphdat
    {
        public const bool VerboseLogging = false;
        private const int GraphdatWorkerLoopSleep = 100;

        private string _source;
        private Socket _socket;
        private EventWaitHandle _termHandle;
        private Thread _thread;
        private ConcurrentQueue<Item> _queue;

        public class Item
        {
            public string type;
            public string source;
            public string route;
            public double responsetime;
            public double timestamp;
            public double cputime;
            public Context[] context;
            internal LoggerDelegate logger;
            internal object logContext;
        };

        public void Init(string config, string source, LoggerDelegate logger, object logContext)
        {
            _socket = new Socket(config, logger, logContext);
            _source = source;
            _queue = new ConcurrentQueue<Item>();
            _thread = new Thread(worker) {IsBackground = true};
            _termHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _thread.Start();

            if (Graphdat.VerboseLogging)
            {
                logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: init '{0}'", config);
            }
        }

        public void Term(LoggerDelegate logger, object logContext)
        {
            if (_termHandle != null) _termHandle.Set();
            if (_thread != null) _thread.Join();
            _socket.Term(logger, logContext);

            if (Graphdat.VerboseLogging)
            {
                logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: term");
            }
        }

        public void Store(Sample sample, LoggerDelegate logger, object logContext)
        {
            var item = new Item
                          {
                              type = "Sample",
                              source = _source,
                              route = string.Format("{0} {1}", sample.method, sample.uri),
                              timestamp = sample.timestamp,
                              responsetime = sample.responsetime,
                              cputime = sample.cputime,
                              context = sample.context,
                              logger = logger,
                              logContext = logContext
                          };
            _queue.Enqueue(item);
        }

        private void Send(Item item)
        {
            var stream = new MemoryStream();
            var packer = Packer.Create(stream);
            var hasContext = (item.context != null && item.context.Length > 0);
            var mapSize = hasContext ? 7 : 6;
            packer.PackMapHeader(mapSize); // timestamp, type, route, responsetime, cputime, source, context?
	        // timestamp
	        packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("timestamp"));
	        packer.Pack(item.timestamp);
	        // type
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("type"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.type));
            // route
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("route"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.route));
            // responsetime
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("responsetime"));
            packer.Pack(item.responsetime);
            // cputime
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("cputime"));
            packer.Pack(item.cputime);
	        // source
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("source"));
            packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(item.source));
            // context
            if (hasContext)
            {
                packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("context"));
                packer.PackArrayHeader(item.context.Length);
                foreach (var context in item.context)
                {
                    packer.PackMapHeader(4); // name, timestamp, responsetime, cputime
                    // name
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("name"));
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes(context.name));
                    // timestamp
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("timestamp"));
                    packer.Pack(context.timestamp);
                    // responsetime
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("responsetime"));
                    packer.Pack(context.responsetime);
                    // cputime
                    packer.PackRaw(System.Text.Encoding.ASCII.GetBytes("cputime"));
                    packer.Pack(context.cputime);
                }
            }

            Console.WriteLine("data: [{0}]", Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length));

            _socket.Send(stream.GetBuffer(), stream.Length, item.logger, item.logContext);
            stream.Close();
        }

        private void worker()
        {
            int sleep;
            do
            {
                Item item;
                if(_queue.TryDequeue(out item))
                {
                    Send(item);
                    sleep = 0;
                }
                else
                {
                    sleep = GraphdatWorkerLoopSleep;
                }
            } while (!_termHandle.WaitOne(sleep));
        }
    }
}
