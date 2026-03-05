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

## 本次更新内容（2026-03-06）
1. **彻底修复 `InvokeCommand` 调用失败（参数绑定异常）**
   - 将 `PlcBridgeHub` 面向客户端调用的方法签名改为**仅接收业务参数**（去除额外 `CancellationToken` 形参），避免 SignalR 在参数绑定阶段将调用判定为服务端异常。
   - `InvokeCommand` 内部统一使用 `Context.ConnectionAborted` 作为取消令牌传递给命令处理器，既保留取消能力，也避免客户端参数个数不匹配。
   - `Subscribe` / `Unsubscribe` / `Publish` 同步采用相同策略，消除同类隐患。

2. **危险代码隔离策略保持一致**
   - 高风险调用仍通过 Hub 内部保护性分发与 HostedService 注册处理器执行，异常统一收敛为 `InvokeAckResponse`，不向 UI 裸抛未处理异常。

3. **文档同步更新**
   - 依据本次修复同步维护 README 的更新记录、文件树职责与后续优化建议。

---

## 可继续完善内容
1. **增强可观测性**：
   - 在 `InvokeCommand` 日志中补充参数绑定失败场景的结构化字段（参数个数、方法签名、连接 ID）。
   - 在客户端 UI 中区分“业务失败”与“调用契约失败”两类提示。

2. **契约进一步收敛**：
   - 客户端 `MainWindowViewModel` 内部 `ProductionOrderPushRequest` 可替换为 Core 共享 DTO，减少重复模型。

3. **自动化校验**：
   - 增加 Hub 命令入口集成测试，覆盖参数绑定、取消令牌传递与异常兜底路径。

4. **文档持续化**：
   - 后续每次变更继续维护本 README 的“本次更新内容”和“可继续完善内容”。
