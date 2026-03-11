using NLog;
using ZakYip.PlcBridge.Core;
using ZakYip.PlcBridge.Host;
using NLog.Extensions.Logging;
using ZakYip.PlcBridge.Drivers;
using ZakYip.PlcBridge.Ingress;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.SignalR;
using ZakYip.PlcBridge.Host.Servers;
using ZakYip.PlcBridge.Core.Utilities;
using ZakYip.PlcBridge.Execution.Store;
using ZakYip.PlcBridge.Ingress.SignalR;
using ZakYip.PlcBridge.Execution.Security;
using ZakYip.PlcBridge.Core.Models.Security;

// 尽早配置NLog
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try {
    logger.Info("应用程序启动");

    var builder = WebApplication.CreateBuilder(args);
    var urls = builder.Configuration["Urls"];
    if (!string.IsNullOrWhiteSpace(urls)) {
        builder.WebHost.UseUrls(urls);
    }
    else {
        builder.WebHost.UseUrls("http://0.0.0.0:5000");
    }
    // 显式补强配置加载（CreateApplicationBuilder 默认会加载，这里用于确保发布目录下也可读到）
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
    // 配置NLog
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();

    // ---------------------------
    // Options 注册（强约束：必须与配置节点一致）
    // ---------------------------

    // LogCleanupSettings：Program.cs 之前已绑定 LogCleanup 节点 :contentReference[oaicite:2]{index=2}
    builder.Services.AddOptions<LogCleanupSettings>()
        .Bind(builder.Configuration.GetSection("LogCleanup"))
        .ValidateOnStart();

    // S7PlcManager 依赖 IOptionsMonitor<S7ConnectionOptions> :contentReference[oaicite:3]{index=3}
    builder.Services.AddOptions<S7ConnectionOptions>()
        .Bind(builder.Configuration.GetSection("S7Connection"))
        .ValidateOnStart();

    // ElevatorBridgeHostedService 依赖 IOptionsMonitor<ElevatorHandshakeDbOptions> :contentReference[oaicite:4]{index=4}
    builder.Services.AddOptions<ElevatorHandshakeDbOptions>()
        .Bind(builder.Configuration.GetSection("ElevatorHandshakeDb"))
        .ValidateOnStart();
    builder.Services.AddSingleton<IOptionsMonitor<UsageLimitOptions>>(
        _ => new StaticOptionsMonitor<UsageLimitOptions>(new UsageLimitOptions()));
    //组件注册

    // SafeExecutor：ElevatorBridgeHostedService 构造参数依赖 :contentReference[oaicite:5]{index=5}
    builder.Services.AddSingleton<SafeExecutor>();

    // IPlcManager -> S7PlcManager（单连接、含监控循环，建议单例）
    builder.Services.AddSingleton<IPlcManager, S7PlcManager>();
    builder.Services.AddHttpClient<IElevatorApiClient, HttpElevatorApiClient>(c => {
        c.BaseAddress = new Uri("http://172.16.4.108:8800");
        c.Timeout = TimeSpan.FromMilliseconds(2500);
    });
    // ---------------------------
    // SignalR：心跳/保活关键配置
    // ---------------------------
    builder.Services.AddSignalR(options => {
        // 服务端向客户端发送 ping 的间隔（保持连接活跃）
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);

        // 服务端允许客户端“多久没任何消息/心跳响应”才判定超时断开
        // 需要大于 KeepAliveInterval；值越大越不容易被断开，但断线感知越慢
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);

        // 握手超时（连接初期）
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);

        // 需要时再开，避免额外开销
        // options.EnableDetailedErrors = false;
    });

    // 广播服务
    builder.Services.AddSingleton<IPlcBridgeMessageBroadcaster, PlcBridgeMessageBroadcaster>();
    builder.Services.AddSingleton<IStateProtector, DpapiStateProtector>();
    builder.Services.AddSingleton<FileUsageStateStore>();
    builder.Services.AddSingleton<RegistryUsageStateStore>();
    builder.Services.AddSingleton<IUsageStateStore>(sp => new CompositeUsageStateStore(
        sp.GetRequiredService<ILogger<CompositeUsageStateStore>>(),
        sp.GetRequiredService<FileUsageStateStore>(),
        sp.GetRequiredService<RegistryUsageStateStore>()));

    builder.Services.AddSingleton<IUsageLimitGuard, UsageLimitGuard>();
    //服务注册
    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));

    builder.Services.AddHostedService<ElevatorBridgeHostedService>();
    builder.Services.AddHostedService<ElevatorTaskMonitorHostedService>();
    builder.Services.AddHostedService<PlcHeartbeatHostedService>();

    //builder.Services.AddHostedService<UsageLimitHostedService>();
#if !DEBUG
    builder.Host.UseWindowsService();
#endif
    var host = builder.Build();
    // 添加全局异常处理器以防止崩溃
    AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
        var exception = args.ExceptionObject as Exception;
        logger.Fatal(exception, "未处理的异常发生，应用程序将尝试继续运行");
    };

    TaskScheduler.UnobservedTaskException += (sender, args) => {
        logger.Fatal(args.Exception, "未观察到的任务异常");
        args.SetObserved(); // 防止程序崩溃
    };
    host.MapHub<PlcBridgeHub>("/hub/plcbridge");
    host.Run();
}
catch (Exception e) {
    logger.Error(e, "应用程序因异常而停止");
}
finally {
    LogManager.Shutdown();
}
