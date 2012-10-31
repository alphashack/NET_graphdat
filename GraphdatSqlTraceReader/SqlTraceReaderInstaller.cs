using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Alphashack.Graphdat.Agent.SqlTrace
{
    [RunInstaller(true)]
    public class SqlTraceReaderInstaller : Installer
    {
        private readonly ServiceProcessInstaller _serviceProcessInstaller;
        private readonly ServiceInstaller _serviceInstaller;

        public SqlTraceReaderInstaller()
        {
            _serviceProcessInstaller = new ServiceProcessInstaller();
            _serviceInstaller = new ServiceInstaller();

            _serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            _serviceInstaller.StartType = ServiceStartMode.Automatic;
            _serviceInstaller.ServiceName = Constants.ReaderServiceName;

            Installers.Add(_serviceInstaller);
            Installers.Add(_serviceProcessInstaller);
        }
    }
}
