# ZakYip.PlcBridge

## 项目说明
本仓库用于 PLC 与电梯系统之间的数据桥接，包含 Host（服务端）、Client（WPF 客户端）、Ingress（SignalR/HTTP 接入层）、Drivers（PLC 驱动）、Core（共享契约）与 Execution（安全与状态存储）等模块。

---

## 文件树与职责说明
> 说明：仓库源码文件较多，以下按模块列出目录树，并对每个关键文件职责进行说明，便于维护和二次扩展。

```text
ZakYip.PlcBridge.sln                         # 解决方案入口
README.md                                    # 项目自述与维护说明

ZakYip.PlcBridge.Core/                       # 共享契约层（模型/接口/常量）
  SignalR/HubMethodNames.cs                  # Hub 方法名常量定义
  Models/SignalR/InvokeEnvelope.cs           # 统一 Invoke 请求信封
  Models/SignalR/InvokeAckResponse.cs        # Invoke 响应模型
  ...                                        # 其他 PLC/电梯模型、枚举、接口、工具

ZakYip.PlcBridge.Ingress/                    # 接入层（SignalR Hub / HTTP API 客户端）
  SignalR/PlcBridgeHub.cs                    # SignalR Hub，负责订阅/发布/命令调用分发
  HttpElevatorApiClient.cs                   # 电梯 HTTP API 调用封装

ZakYip.PlcBridge.Host/                       # 服务端启动层
  Program.cs                                 # DI、SignalR、HostedService 注册与宿主启动
  Servers/ElevatorTaskMonitorHostedService.cs# 电梯任务轮询与命令处理注册
  Servers/ElevatorBridgeHostedService.cs     # PLC 与电梯桥接业务流程

ZakYip.PlcBridge.Client/                     # WPF 客户端
  ViewModels/MainWindowViewModel.cs          # UI 主流程、SignalR 调用、状态展示
  Services/SignalRMessageClient.cs           # SignalR 连接与 Invoke/接收封装
  Views/MainWindow.xaml                      # UI 界面

ZakYip.PlcBridge.Drivers/                    # PLC 驱动实现
  S7PlcManager.cs                            # S7 PLC 连接、读写、监控

ZakYip.PlcBridge.Execution/                  # 执行安全与状态持久化
  Security/UsageLimitGuard.cs                # 使用限制策略守卫
  Store/*.cs                                 # 文件/注册表复合状态存储

ZakYip.PlcBridge.Resources/                  # 客户端资源层
  Styles/*.xaml                              # WPF 样式
  Converters/*.cs                            # UI 转换器
  Lotties/*.json                             # 动效资源
```

---

## 本次更新内容（2026-03-05）
1. **修复客户端未命中 `Invoke` 断点问题**
   - 新增 Hub 统一调用入口常量 `InvokeCommand`，避免继续依赖字符串硬编码。
   - 服务端新增 `InvokeCommand` 方法作为统一命令入口，`Invoke` 保留为兼容转发入口。
   - 客户端调用从 `"Invoke"` 调整为 `"InvokeCommand"`，避免因方法名歧义/绑定问题导致调用失败。

2. **统一 Invoke 请求契约**
   - 在 Core 新增 `InvokeEnvelope`，将命令请求信封下沉为共享模型，避免客户端与服务端定义漂移。

3. **危险代码隔离思路保持一致**
   - 现有高风险逻辑（轮询与业务执行）继续通过 HostedService + 既有安全执行器模式承载；本次改动未引入额外裸奔高危调用。

---

## 可继续完善内容
1. **增强诊断能力**：
   - 增加 Invoke 链路 TraceId，并在客户端/服务端日志中透传。
   - 在 Host 中引入可配置 `EnableDetailedErrors`（仅开发环境开启）。

2. **契约进一步收敛**：
   - 将 `PushProductionOrder` 的 Request DTO 统一放入 Core，客户端直接复用，减少重复定义。

3. **自动化校验**：
   - 补充针对 SignalR 命令入口的集成测试（Hub 调用 + Handler 分发 + 异常路径）。

4. **文档持续化**：
   - 后续每次变更继续维护本 README 的“本次更新内容”和“可继续完善内容”。
