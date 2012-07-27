using System.Threading;
using System.Web.Mvc;
using Alphashack.Graphdat.Agent;

namespace Mvc3ApplicationTest.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Welcome to ASP.NET MVC!";

            var graphdat = HttpModule.SafeGetGraphdat();

            graphdat.Begin("StepOne");
            Thread.Sleep(200);
            graphdat.End("StepOne");
            graphdat.Begin("StepTwo");
            graphdat.Begin("A");
            Thread.Sleep(100);
            graphdat.End("A");
            graphdat.Begin("B");
            Thread.Sleep(250);
            graphdat.End("B");
            graphdat.End("StepTwo");

            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
}
