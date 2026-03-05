using System;
using Prism.Mvvm;
using System.Linq;
using System.Text;
using Prism.Regions;
using Prism.Commands;
using System.Windows;
using ToastNotifications;
using System.Windows.Input;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ZakYip.PlcBridge.Client.Enums;
using ZakYip.PlcBridge.Client.Models;
using ZakYip.PlcBridge.Client.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ZakYip.PlcBridge.Client.ViewModels {

    public class MainWindowViewModel : BindableBase {
        private readonly Notifier _notifier;
        private readonly IRegionManager _regionManager;
        private readonly ISignalRMessageClient _signalRMessageClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MainWindowViewModel> _logger;

        private ProductionOrderModel _productionOrder = new() {
            WorkOrderNo = string.Empty,
            ItemCode = string.Empty,
            BatchNo = string.Empty,
            PlannedBoxCount = 0
        };

        private bool _isPushing;
        private ProductionProgressModel _productionProgress = new();
        private ConnectionStatus _signalRConnectionStatus = ConnectionStatus.Disconnected;
        private ConnectionStatus _s7ConnectionStatus = ConnectionStatus.Disconnected;
        private string _callTaskId = string.Empty;
        private ProductionOrderModel? _previousProductionOrder;
        private OperationResultStatus? _operationResultStatus;
        private bool _isLoading = true;

        /// <summary>
        /// 是否加载中。
        /// </summary>
        public bool IsLoading {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public ProductionOrderModel ProductionOrder {
            get => _productionOrder;
            set => SetProperty(ref _productionOrder, value);
        }

        /// <summary>
        /// 上一个生产订单模型。
        /// </summary>
        public ProductionOrderModel? PreviousProductionOrder {
            get => _previousProductionOrder;
            private set => SetProperty(ref _previousProductionOrder, value);
        }

        public OperationResultStatus? OperationResultStatus {
            get => _operationResultStatus;
            set => SetProperty(ref _operationResultStatus, value);
        }

        /// <summary>
        /// 呼叫任务的 Guid。
        /// </summary>
        public string CallTaskId {
            get => _callTaskId;
            private set => SetProperty(ref _callTaskId, value);
        }

        /// <summary>
        /// 是否推送中。
        /// </summary>
        public bool IsPushing {
            get => _isPushing;
            set => SetProperty(ref _isPushing, value);
        }

        /// <summary>
        /// SignalR 连接状态。
        /// </summary>
        public ConnectionStatus SignalRConnectionStatus {
            get => _signalRConnectionStatus;
            private set => SetProperty(ref _signalRConnectionStatus, value);
        }

        /// <summary>
        /// S7 连接状态。
        /// </summary>
        public ConnectionStatus S7ConnectionStatus {
            get => _s7ConnectionStatus;
            private set => SetProperty(ref _s7ConnectionStatus, value);
        }

        public ProductionProgressModel ProductionProgress {
            get => _productionProgress;
            set => SetProperty(ref _productionProgress, value);
        }

        public MainWindowViewModel(Notifier notifier, IRegionManager regionManager,
            ISignalRMessageClient signalRMessageClient,
            IMemoryCache memoryCache,
            ILogger<MainWindowViewModel> logger) {
            _notifier = notifier;
            _regionManager = regionManager;
            _signalRMessageClient = signalRMessageClient;
            _memoryCache = memoryCache;
            _logger = logger;
            _signalRMessageClient.ConnectionStatusChanged += async (sender, args) => {
                await Application.Current.Dispatcher.InvokeAsync(() => {
                    SignalRConnectionStatus = args.CurrentStatus;
                });
            };
            _signalRMessageClient.MessageReceived += (sender, args) => {
            };
        }

        public ICommand LoadedCommand => new DelegateCommand<object>(LoadedDelegate);

        private void LoadedDelegate(object obj) {
            Task.Run(async () => {
                try {
                    await _signalRMessageClient.ConnectAsync();
                    await Task.Delay(1500);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "主窗口加载时连接 SignalR 失败。");
                }
                finally {
                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        IsLoading = false;
                    });
                }
            });
        }

        public ICommand CloseWinCommand => new DelegateCommand<object>(CloseWinDelegate);

        private async void CloseWinDelegate(object obj) {
            //关闭通知

            await Task.Delay(1600);
            System.Windows.Application.Current.Shutdown();//关闭
        }

        /// <summary>
        /// 推送生产信息命令。
        /// </summary>
        public ICommand PushProductionInfoCommand => new DelegateCommand<object>(PushProductionInfoDelegate);

        private void PushProductionInfoDelegate(object payload) {
            if (IsPushing) return;
            IsPushing = true;
            Task.Run(async () => {
                try {
                    var signalRInvokeResponse =
                        await _signalRMessageClient.InvokeAsync("Invoke", new {
                            CommandName = "PushProductionOrder",
                            Request = new PushProductionOrderRequest {
                                WorkOrderNo = ProductionOrder.WorkOrderNo,
                                ItemCode = ProductionOrder.ItemCode,
                                BatchNo = ProductionOrder.BatchNo,
                                PlanQty = ProductionOrder.PlannedBoxCount
                            }
                        });

                    await Application.Current.Dispatcher.InvokeAsync(async () => {
                        IsPushing = false;
                        if (signalRInvokeResponse.IsSuccess) {
                            OperationResultStatus = Enums.OperationResultStatus.Success;
                            await Task.Delay(2000);
                        }
                        else {
                            _logger.LogError("推送生产信息失败：{ErrorMessage}", signalRInvokeResponse.ErrorMessage);
                            OperationResultStatus = Enums.OperationResultStatus.Failure;
                            await Task.Delay(4000);
                        }

                        OperationResultStatus = null;

                        //标记失败、成功状态 2s 后重置为 null（即不显示任何状态）
                    });
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "推送生产信息异常。");
                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        IsPushing = false;
                        OperationResultStatus = Enums.OperationResultStatus.Failure;
                    });
                    await Task.Delay(4000);
                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        OperationResultStatus = null;
                    });
                }
            });
        }

        private sealed record class PushProductionOrderRequest {
            [JsonPropertyName("workOrderNo")]
            public required string WorkOrderNo { get; init; }

            [JsonPropertyName("itemCode")]
            public required string ItemCode { get; init; }

            [JsonPropertyName("batchNo")]
            public string? BatchNo { get; init; }

            [JsonPropertyName("PlanQty")]
            public required int PlanQty { get; init; }
        }
    }
}
