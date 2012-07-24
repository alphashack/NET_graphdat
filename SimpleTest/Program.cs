using System;
using System.Threading;
using NET_graphdat;

namespace SimpleTest
{
    class Program
    {
        public static void logger(GraphdatLogType type, object user, string fmt, params object[] args)
        {
            Console.Write("{0}: ", type);
            Console.WriteLine(fmt, args);
        }

        static void Main(string[] args)
        {
            var gd = new Graphdat();
            var sample = new Sample
                             {
                                 method = "GET",
                                 uri = "/test",
                                 timestamp = DateTime.Now.TimeOfDay.TotalMilliseconds,
                                 responsetime = 100,
                                 cputime = 20,
                                 context = new [] { new Context { name = "/", responsetime = 11 }, new Context { name = "/one", responsetime = 12 }, new Context { name = "/two", responsetime = 13 }}
                             };
            gd.Init("26873", "testing", logger, null);
            for (int i = 0; i < 1000; i++)
            {
                gd.Store(sample, logger, null);
                Thread.Sleep(300);
            }
            gd.Term(logger, null);
            Console.Read();
        }
    }
}
