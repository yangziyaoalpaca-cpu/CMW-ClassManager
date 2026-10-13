using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CMW
{
    /// <summary>
    /// iOS 26 液态玻璃效果自定义控件
    /// 26.1.0 版本：移除光晕/内阴影/镜面点光，改用 BitmapCache 缓存，触摸输入不触发按压
    /// </summary>
    public class LiquidGlassBorder : ContentControl
    {
        static LiquidGlassBorder()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(LiquidGlassBorder),
                new FrameworkPropertyMetadata(typeof(LiquidGlassBorder)));
        }

        public LiquidGlassBorder()
        {
            Padding = new Thickness(12);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            SnapsToDevicePixels = true;

            // ★ 使用 Preview 事件 + 触摸过滤，避免拦截 ScrollViewer 触摸滚动
            PreviewMouseLeftButtonDown += LiquidGlassBorder_PreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += LiquidGlassBorder_PreviewMouseLeftButtonUp;
            MouseLeave += LiquidGlassBorder_MouseLeave;
        }

        private void LiquidGlassBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // ★ 触摸输入不触发按压动画，让 ScrollViewer 优先接收滑动
            if (IsTouchInput(e)) return;

            if (PressAnimationEnabled && IsEnabled)
            {
                SetCurrentValue(IsPressedProperty, true);
            }
        }

        private void LiquidGlassBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsTouchInput(e)) return;
            SetCurrentValue(IsPressedProperty, false);
        }

        private void LiquidGlassBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            SetCurrentValue(IsPressedProperty, false);
        }

        private static bool IsTouchInput(MouseEventArgs e)
        {
            var device = e.StylusDevice;
            if (device == null) return false;

            var tablet = device.TabletDevice;
            if (tablet == null) return false;

            return tablet.Type == TabletDeviceType.Touch;
        }

        // ═══════ 依赖属性 ═══════

        /// <summary>圆角</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius),
                typeof(LiquidGlassBorder),
                new PropertyMetadata(new CornerRadius(16)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>玻璃整体不透明度（0~1）</summary>
        public static readonly DependencyProperty GlassOpacityProperty =
            DependencyProperty.Register(
                nameof(GlassOpacity), typeof(double),
                typeof(LiquidGlassBorder),
                new PropertyMetadata(1.0));

        public double GlassOpacity
        {
            get => (double)GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        /// <summary>是否启用悬停光晕（26.1.0 保留属性，模板简化后不再使用）</summary>
        public static readonly DependencyProperty HoverGlowEnabledProperty =
            DependencyProperty.Register(
                nameof(HoverGlowEnabled), typeof(bool),
                typeof(LiquidGlassBorder),
                new PropertyMetadata(true));

        public bool HoverGlowEnabled
        {
            get => (bool)GetValue(HoverGlowEnabledProperty);
            set => SetValue(HoverGlowEnabledProperty, value);
        }

        /// <summary>是否启用按压回弹动画</summary>
        public static readonly DependencyProperty PressAnimationEnabledProperty =
            DependencyProperty.Register(
                nameof(PressAnimationEnabled), typeof(bool),
                typeof(LiquidGlassBorder),
                new PropertyMetadata(false));

        public bool PressAnimationEnabled
        {
            get => (bool)GetValue(PressAnimationEnabledProperty);
            set => SetValue(PressAnimationEnabledProperty, value);
        }

        /// <summary>按压状态（模板触发器监听此属性）</summary>
        public static readonly DependencyProperty IsPressedProperty =
            DependencyProperty.Register(
                nameof(IsPressed), typeof(bool),
                typeof(LiquidGlassBorder),
                new PropertyMetadata(false));

        public bool IsPressed
        {
            get => (bool)GetValue(IsPressedProperty);
            set => SetValue(IsPressedProperty, value);
        }
    }
}