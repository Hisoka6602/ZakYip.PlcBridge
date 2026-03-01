using System.Data;
using System.Windows;
using System.Configuration;

namespace ZakYip.PlcBridge.Client {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication {

        protected override void RegisterTypes(IContainerRegistry containerRegistry) {
        }

        protected override Window CreateShell() {
            var mainWindow = Container.Resolve<MainWindow>();

            return mainWindow;
        }
    }
}
