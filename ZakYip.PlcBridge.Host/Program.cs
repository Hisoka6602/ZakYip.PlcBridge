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

// ��������NLog
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try {
    logger.Info("Ӧ�ó�������");

    var builder = WebApplication.CreateBuilder(args);
    var urls = builder.Configuration["Urls"];
    if (!string.IsNullOrWhiteSpace(urls)) {
        builder.WebHost.UseUrls(urls);
    }
    else {
        builder.WebHost.UseUrls("http://0.0.0.0:5000");
    }
    // ��ʽ��ǿ���ü��أ�CreateApplicationBuilder Ĭ�ϻ���أ���������ȷ������Ŀ¼��Ҳ�ɶ�����
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
    // ����NLog
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();

    // ---------------------------
    // Options ע�ᣨǿԼ�������������ýڵ�һ�£�
    // ---------------------------

    // LogCleanupSettings��Program.cs ֮ǰ�Ѱ� LogCleanup �ڵ� :contentReference[oaicite:2]{index=2}
    builder.Services.AddOptions<LogCleanupSettings>()
        .Bind(builder.Configuration.GetSection("LogCleanup"))
        .ValidateOnStart();

    // S7PlcManager ���� IOptionsMonitor<S7ConnectionOptions> :contentReference[oaicite:3]{index=3}
    builder.Services.AddOptions<S7ConnectionOptions>()
        .Bind(builder.Configuration.GetSection("S7Connection"))
        .ValidateOnStart();

    // ElevatorBridgeHostedService ���� IOptionsMonitor<ElevatorHandshakeDbOptions> :contentReference[oaicite:4]{index=4}
    builder.Services.AddOptions<ElevatorHandshakeDbOptions>()
        .Bind(builder.Configuration.GetSection("ElevatorHandshakeDb"))
        .ValidateOnStart();
    builder.Services.AddSingleton<IOptionsMonitor<UsageLimitOptions>>(
        _ => new StaticOptionsMonitor<UsageLimitOptions>(new UsageLimitOptions()));
    //���ע��

    // SafeExecutor��ElevatorBridgeHostedService ����������� :contentReference[oaicite:5]{index=5}
    builder.Services.AddSingleton<SafeExecutor>();

    // IPlcManager -> S7PlcManager�������ӡ������ѭ�������鵥����
    builder.Services.AddSingleton<IPlcManager, S7PlcManager>();
    builder.Services.AddHttpClient<IElevatorApiClient, HttpElevatorApiClient>(c => {
        c.BaseAddress = new Uri("http://172.16.4.108:8800");
        c.Timeout = TimeSpan.FromMilliseconds(2500);
    });
    // ---------------------------
    // SignalR������/����ؼ�����
    // ---------------------------
    builder.Services.AddSignalR(options => {
        // �������ͻ��˷��� ping �ļ�����������ӻ�Ծ��
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);

        // ����������ͻ��ˡ����û�κ���Ϣ/������Ӧ�����ж���ʱ�Ͽ�
        // ��Ҫ���� KeepAliveInterval��ֵԽ��Խ�����ױ��Ͽ��������߸�֪Խ��
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);

        // ���ֳ�ʱ�����ӳ��ڣ�
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);

        // ��Ҫʱ�ٿ���������⿪��
        // options.EnableDetailedErrors = false;
    });

    // �㲥����
    builder.Services.AddSingleton<IPlcBridgeMessageBroadcaster, PlcBridgeMessageBroadcaster>();
    builder.Services.AddSingleton<IStateProtector, DpapiStateProtector>();
    builder.Services.AddSingleton<FileUsageStateStore>();
    builder.Services.AddSingleton<RegistryUsageStateStore>();
    builder.Services.AddSingleton<IUsageStateStore>(sp => new CompositeUsageStateStore(
        sp.GetRequiredService<ILogger<CompositeUsageStateStore>>(),
        sp.GetRequiredService<FileUsageStateStore>(),
        sp.GetRequiredService<RegistryUsageStateStore>()));

    builder.Services.AddSingleton<IUsageLimitGuard, UsageLimitGuard>();
    //����ע��
    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));

    builder.Services.AddHostedService<LogCleanupService>();
    builder.Services.AddHostedService<ElevatorBridgeHostedService>();
    builder.Services.AddHostedService<ElevatorTaskMonitorHostedService>();
    builder.Services.AddHostedService<PlcHeartbeatHostedService>();

    //builder.Services.AddHostedService<UsageLimitHostedService>();
#if !DEBUG
    builder.Host.UseWindowsService();
#endif
    var host = builder.Build();
    // ����ȫ���쳣�������Է�ֹ����
    AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
        var exception = args.ExceptionObject as Exception;
        logger.Fatal(exception, "δ�������쳣������Ӧ�ó��򽫳��Լ�������");
    };

    TaskScheduler.UnobservedTaskException += (sender, args) => {
        logger.Fatal(args.Exception, "δ�۲쵽�������쳣");
        args.SetObserved(); // ��ֹ�������
    };
    host.MapHub<PlcBridgeHub>("/hub/plcbridge");
    host.Run();
}
catch (Exception e) {
    logger.Error(e, "Ӧ�ó������쳣��ֹͣ");
}
finally {
    LogManager.Shutdown();
}
