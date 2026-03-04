using System;
using System.Linq;
using System.Windows;
using System.Reflection;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Client.Presentation {

    /// <summary>
    /// Lottie 播放行为：
    /// - 进入可见状态时：从第一帧开始播放
    /// - 播放一次后停止（RepeatCount=1 时通常自然停止）
    /// - 再次可见时：再次从第一帧开始播放
    /// </summary>
    public static class LottiePlayBehavior {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// 可见时从第一帧开始播放（播一次）。
        /// </summary>
        public static readonly DependencyProperty PlayFromStartWhenVisibleProperty =
            DependencyProperty.RegisterAttached(
                "PlayFromStartWhenVisible",
                typeof(bool),
                typeof(LottiePlayBehavior),
                new PropertyMetadata(false, OnPlayFromStartWhenVisibleChanged));

        /// <summary>
        /// 获取开关状态。
        /// </summary>
        public static bool GetPlayFromStartWhenVisible(DependencyObject obj) {
            return (bool)obj.GetValue(PlayFromStartWhenVisibleProperty);
        }

        /// <summary>
        /// 设置开关状态。
        /// </summary>
        public static void SetPlayFromStartWhenVisible(DependencyObject obj, bool value) {
            obj.SetValue(PlayFromStartWhenVisibleProperty, value);
        }

        private static void OnPlayFromStartWhenVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not FrameworkElement element) {
                return;
            }

            var isEnabled = (bool)e.NewValue;

            // 先解绑再绑定，避免边界情况下重复订阅
            element.IsVisibleChanged -= OnIsVisibleChanged;
            element.Unloaded -= OnUnloaded;

            if (!isEnabled) {
                return;
            }

            element.IsVisibleChanged += OnIsVisibleChanged;
            element.Unloaded += OnUnloaded;

            // 若在启用行为时已经处于可见状态，立即从头播一次
            if (element.IsVisible) {
                SafeRestartAndPlayOnce(element);
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e) {
            if (sender is not FrameworkElement element) {
                return;
            }

            // 卸载时停止，避免资源占用
            SafeStop(element);
        }

        private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (sender is not FrameworkElement element) {
                return;
            }

            if (element.IsVisible) {
                // 进入可见：从头播一次
                SafeRestartAndPlayOnce(element);
            }
            else {
                // 进入不可见：停止，避免后台继续占用
                SafeStop(element);
            }
        }

        private static void SafeRestartAndPlayOnce(object element) {
            try {
                // 停止并回到开头
                InvokeMethodIfExists(element, "Stop");
                ResetToStartIfPossible(element);

                // 播放
                if (!InvokeMethodIfExists(element, "Play")) {
                    // 兼容某些控件用 Start/Begin
                    InvokeMethodIfExists(element, "Start");
                    InvokeMethodIfExists(element, "Begin");
                }
            }
            catch {
                // 行为层异常隔离，避免影响 UI 主流程
            }
        }

        private static void SafeStop(object element) {
            try {
                if (!InvokeMethodIfExists(element, "Stop")) {
                    InvokeMethodIfExists(element, "Pause");
                }
            }
            catch {
                // 行为层异常隔离
            }
        }

        private static void ResetToStartIfPossible(object element) {
            // 常见的几种方式（按优先级尝试）：
            // 1) 属性 Progress=0
            // 2) 方法 SetProgress(0) / Seek(0) / SeekToFrame(0) / GoTo(0)
            // 3) 属性 Frame=0 / CurrentFrame=0
            if (TrySetProperty(element, "Progress", 0d)) {
                return;
            }

            if (InvokeMethodIfExists(element, "SetProgress", 0d)) {
                return;
            }

            if (InvokeMethodIfExists(element, "Seek", 0d) || InvokeMethodIfExists(element, "Seek", 0)) {
                return;
            }

            if (InvokeMethodIfExists(element, "SeekToFrame", 0) || InvokeMethodIfExists(element, "GoTo", 0)) {
                return;
            }

            TrySetProperty(element, "Frame", 0);
            TrySetProperty(element, "CurrentFrame", 0);
        }

        private static bool TrySetProperty(object target, string propertyName, object value) {
            try {
                var type = target.GetType();
                var prop = type.GetProperty(propertyName, PublicInstance);
                if (prop is null || !prop.CanWrite) {
                    return false;
                }

                var converted = ConvertIfNeeded(value, prop.PropertyType);
                if (converted is null && prop.PropertyType.IsValueType) {
                    return false;
                }

                prop.SetValue(target, converted);
                return true;
            }
            catch {
                return false;
            }
        }

        private static bool InvokeMethodIfExists(object target, string methodName, params object[] args) {
            try {
                var type = target.GetType();
                var methods = type.GetMethods(PublicInstance)
                    .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                    .ToArray();

                if (methods.Length == 0) {
                    return false;
                }

                foreach (var method in methods) {
                    var parameters = method.GetParameters();
                    if (parameters.Length != args.Length) {
                        continue;
                    }

                    var convertedArgs = new object[args.Length];
                    var canUse = true;

                    for (var i = 0; i < args.Length; i++) {
                        var targetType = parameters[i].ParameterType;
                        var converted = ConvertIfNeeded(args[i], targetType);

                        if (converted is null && targetType.IsValueType) {
                            canUse = false;
                            break;
                        }

                        convertedArgs[i] = converted!;
                    }

                    if (!canUse) {
                        continue;
                    }

                    method.Invoke(target, convertedArgs);
                    return true;
                }

                return false;
            }
            catch {
                return false;
            }
        }

        private static object? ConvertIfNeeded(object value, Type targetType) {
            try {
                if (value is null) {
                    return null;
                }

                var valueType = value.GetType();
                if (targetType.IsAssignableFrom(valueType)) {
                    return value;
                }

                // 处理 Nullable<T>
                var underlying = Nullable.GetUnderlyingType(targetType);
                if (underlying is not null) {
                    targetType = underlying;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch {
                return null;
            }
        }

        public static readonly DependencyProperty RestartWhenResourcePathChangedProperty =
    DependencyProperty.RegisterAttached(
        "RestartWhenResourcePathChanged",
        typeof(bool),
        typeof(LottiePlayBehavior),
        new PropertyMetadata(false, OnRestartWhenResourcePathChangedChanged));

        public static bool GetRestartWhenResourcePathChanged(DependencyObject obj)
            => (bool)obj.GetValue(RestartWhenResourcePathChangedProperty);

        public static void SetRestartWhenResourcePathChanged(DependencyObject obj, bool value)
            => obj.SetValue(RestartWhenResourcePathChangedProperty, value);

        private static void OnRestartWhenResourcePathChangedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not FrameworkElement element) {
                return;
            }

            element.SetValue(ResourcePathSnapshotProperty, GetResourcePathValue(element));

            element.TargetUpdated -= OnResourcePathTargetUpdated;
            if ((bool)e.NewValue) {
                // 绑定更新触发点：ResourcePath 的 Binding 更新时会走 TargetUpdated
                element.TargetUpdated += OnResourcePathTargetUpdated;
            }
        }

        private static readonly DependencyProperty ResourcePathSnapshotProperty =
            DependencyProperty.RegisterAttached(
                "ResourcePathSnapshot",
                typeof(string),
                typeof(LottiePlayBehavior),
                new PropertyMetadata(string.Empty));

        private static void OnResourcePathTargetUpdated(object? sender, DataTransferEventArgs e) {
            if (sender is not FrameworkElement element) {
                return;
            }

            var current = GetResourcePathValue(element);
            var snapshot = (string)element.GetValue(ResourcePathSnapshotProperty);

            if (string.Equals(current, snapshot, StringComparison.Ordinal)) {
                return;
            }

            element.SetValue(ResourcePathSnapshotProperty, current);

            // 资源切换后，强制从头播放一次
            SafeRestartAndPlayOnce(element);
        }

        private static string GetResourcePathValue(FrameworkElement element) {
            // 反射读取 ResourcePath，避免强依赖控件具体类型
            try {
                var prop = element.GetType().GetProperty("ResourcePath", BindingFlags.Instance | BindingFlags.Public);
                return prop?.GetValue(element)?.ToString() ?? string.Empty;
            }
            catch {
                return string.Empty;
            }
        }
    }
}
