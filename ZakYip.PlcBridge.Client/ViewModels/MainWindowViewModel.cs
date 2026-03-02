using System;
using Prism.Mvvm;
using System.Linq;
using System.Text;
using Prism.Regions;
using Prism.Commands;
using ToastNotifications;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace ZakYip.PlcBridge.Client.ViewModels {

    public class MainWindowViewModel : BindableBase {
        private readonly Notifier _notifier;
        private readonly IRegionManager _regionManager;
        private readonly IMemoryCache _memoryCache;

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
    }
}
