using System;
using System.Threading;
using Alphashack.Graphdat.Agent;

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

        }

        void Test01()
        {
            var gd = new Connect();
            var sample = new Sample
                             {
                                 Method = "GET",
                                 Uri = "/test",
                                 Timestamp = DateTime.Now.TimeOfDay.TotalMilliseconds,
                                 ResponseTime = 100,
                                 CpuTime = 20,
                                 Context =
                                     new[]
                                         {
                                             new Context {Name = "/", ResponseTime = 11},
                                             new Context {Name = "/one", ResponseTime = 12},
                                             new Context {Name = "/two", ResponseTime = 13}
                                         }
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

        void Test02()
        {
            // Arrange
            var rootpayload = "rootpayload";
            var subject = new ContextBuilder<string>(() => rootpayload);
            var child1payload = "child1payload";
            subject.Enter("child1", () => child1payload);
            var child2payload = "child2payload";
            subject.Enter("child2", () => child2payload);
            subject.Leave();
            var child3payload = "child3payload";
            subject.Enter("child3", () => child3payload);
            subject.Leave();
            subject.Leave();

            Action<string, dynamic> build = (payload, obj) => { obj.property = payload; };

            // Act
            var result = subject.Flatten(build);

            // Assert
            //assert.equivalent([{name:'/',property:rootpayload},{name:'/child1',property:child1payload},{name:'/child1/child2',property:child2payload},{name:'/child1/child3',property:child3payload}], obj);


        }
    }
}
