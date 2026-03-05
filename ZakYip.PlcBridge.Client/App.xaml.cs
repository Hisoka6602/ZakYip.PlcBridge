using Example;
using Prism.Ioc;
using System.IO;
using Prism.Mvvm;
using System.Data;
using System.Text;
using Prism.DryIoc;
using System.Windows;
using ToastNotifications;
using System.Configuration;
using System.Windows.Media;
using System.Windows.Interop;
using NLog;
using NLog.Extensions.Logging;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Client.Views;
using ZakYip.PlcBridge.Client.Options;
using ZakYip.PlcBridge.Client.Services;
using Microsoft.Extensions.Configuration;
using ZakYip.PlcBridge.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DryIoc.Microsoft.DependencyInjection.Extension;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ZakYip.PlcBridge.Client {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication {
        private static readonly Logger StartupLogger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
        private LogCleanupService? _logCleanupService;

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
                // 构建 IConfiguration：不使用 SetBasePath，直接拼绝对路径（避免 FileExtensions 包）
                var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(appsettingsPath, optional: false, reloadOnChange: true)
                    .Build();

                services.AddSingleton<IConfiguration>(configuration);

                // 日志：只使用 NLog Provider
                services.AddLogging(logging => {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                    logging.AddNLog(configuration);
                });

                // 从 IConfiguration 手工读取 SignalRConnectionOptions（不使用 Binder）
                var signalROptions = SignalRConnectionOptionsFactory.Create(configuration);
                services.AddSingleton(signalROptions);
                var logCleanupOptions = LogCleanupOptionsFactory.Create(configuration);
                services.AddSingleton(logCleanupOptions);

                // SignalR Client：单例复用连接
                services.AddSingleton<ISignalRMessageClient, SignalRMessageClient>();
                services.AddSingleton<LogCleanupService>();
            });
        }

        protected override Window CreateShell() {
            var mainWindow = Container.Resolve<MainWindow>();

            return mainWindow;
        }

        protected override void OnStartup(StartupEventArgs e) {
            DispatcherUnhandledException += (_, args) => {
                StartupLogger.Error(args.Exception, "UI 线程未处理异常。");
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) => {
                StartupLogger.Fatal(args.ExceptionObject as Exception, "非 UI 线程未处理异常。");
            };
            TaskScheduler.UnobservedTaskException += (_, args) => {
                StartupLogger.Fatal(args.Exception, "未观察到的任务异常。");
                args.SetObserved();
            };

            RenderOptions.ProcessRenderMode = RenderMode.Default;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ThreadPool.SetMinThreads(100, 200);
            //小数点问题
            FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;
            base.OnStartup(e);

            _logCleanupService = Container.Resolve<LogCleanupService>();
            _logCleanupService.Start();
        }

        protected override void ConfigureViewModelLocator() {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
        }

        protected override async void OnExit(ExitEventArgs e) {
            if (_logCleanupService is not null) {
                await _logCleanupService.StopAsync();
            }

            LogManager.Shutdown();
            base.OnExit(e);
        }
    }
}
