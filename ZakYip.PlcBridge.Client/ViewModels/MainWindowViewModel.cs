using System;
using Prism.Mvvm;
using System.Linq;
using System.Text;
using Prism.Regions;
using Prism.Commands;
using ToastNotifications;
using System.Windows.Input;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Enums;
using ZakYip.PlcBridge.Client.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ZakYip.PlcBridge.Client.ViewModels {

    public class MainWindowViewModel : BindableBase {
        private readonly Notifier _notifier;
        private readonly IRegionManager _regionManager;
        private readonly IMemoryCache _memoryCache;

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
            IMemoryCache memoryCache) {
            _notifier = notifier;
            _regionManager = regionManager;
            _memoryCache = memoryCache;
        }

        public ICommand LoadedCommand => new DelegateCommand<object>(LoadedDelegate);

        private void LoadedDelegate(object obj) {
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

        private async void PushProductionInfoDelegate(object payload) {
            if (IsPushing) return;
            IsPushing = true;
            Console.WriteLine(ProductionOrder);
            Console.WriteLine($"推送生产信息");
            await Task.Delay(5000);
            IsPushing = false;
        }
    }
}
