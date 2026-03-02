using Example;
using Prism.Ioc;
using Prism.Mvvm;
using System.Data;
using System.Text;
using Prism.DryIoc;
using System.Windows;
using ToastNotifications;
using System.Configuration;
using System.Windows.Media;
using System.Windows.Interop;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ZakYip.PlcBridge.Client.Views;
using ZakYip.PlcBridge.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using DryIoc.Microsoft.DependencyInjection.Extension;

namespace ZakYip.PlcBridge.Client {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication {

        protected override void RegisterTypes(IContainerRegistry containerRegistry) {
            //注册提示控件
            containerRegistry.RegisterSingleton<Notifier>(provider => {
                return new Notifier(cfg => {
                    cfg.PositionProvider = new WindowPositionProvider(
                        parentWindow: Application.Current.MainWindow,
                        corner: Corner.BottomRight,
                        offsetX: 10,
                        offsetY: 10);

                    cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                        notificationLifetime: TimeSpan.FromSeconds(2),
                        maximumNotificationCount: MaximumNotificationCount.FromCount(7));
                    cfg.Dispatcher = Application.Current.Dispatcher;
                    cfg.DisplayOptions.TopMost = false;
                });
            });
            containerRegistry.GetContainer().RegisterServices(services => {
                //配置内存缓存
                services.AddMemoryCache();
            });
        }

        protected override Window CreateShell() {
            var mainWindow = Container.Resolve<MainWindow>();

            return mainWindow;
        }

        protected override void OnStartup(StartupEventArgs e) {
            RenderOptions.ProcessRenderMode = RenderMode.Default;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ThreadPool.SetMinThreads(100, 200);
            //小数点问题
            FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;
            base.OnStartup(e);
        }

        protected override void ConfigureViewModelLocator() {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
        }
    }
}
