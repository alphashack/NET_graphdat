using System;
using System.Threading;
using Alphashack.Graphdat.Agent;

namespace WebApplicationTest
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            IGraphdat graphdat;
            HttpModule.TryGetGraphdat(out graphdat);

            graphdat.Begin("StepOne");
            Thread.Sleep(200);
            graphdat.End();
            graphdat.Begin("StepTwo");
            graphdat.Begin("A");
            Thread.Sleep(100);
            graphdat.End();
            graphdat.Begin("B");
            Thread.Sleep(250);
        }
    }
}
