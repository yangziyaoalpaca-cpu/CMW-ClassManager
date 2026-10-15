using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CMW
{
    public partial class IslandWindow : Window
    {
        // ═══════════════════════════════════════════════════════════
        // ★ 32.0.0：状态机枚举（仅作语义文档，不保留字段）
        // ═══════════════════════════════════════════════════════════
        private enum IslandState
        {
            Capsule,
            Bar,
            Card,
            Notify,
            RollCall
        }

        // ═══════════════════════════════════════════════════════════
        // 常量
        // ═══════════════════════════════════════════════════════════
        private const double CapsuleHeight = 18;
        private const double CapsuleRadius = 9;
        private const double CapsuleMinWidth = 60;
        private const double CapsuleMaxWidth = 160;
        private const double CapsuleTextPadding = 28;
        private const double CircleDiameterDefault = 40;
        private const double CardRadius = 18;

        private const int ClickAutoCollapseMs = 8000;
        private const int NotifyAutoCollapseMs = 3000;
        private const int MouseLeaveDelayMs = 500;
        private const int ExpandDurationMs = 420;
        private const int CollapseDurationMs = 360;
        private const int BarExtendDurationMs = 320;

        private const double BarMinWidth = 50;
        private const double BarSidePadding = 40;

        private const int BarGroupWidthTransitionMs = 180;
        private const int BarGroupFadeOutMs = 80;
        private const int BarGroupFadeInMs = 100;

        private const string StyleColorThenNotify = "ColorThenNotify";
        private const string StyleNotifyOnly = "NotifyOnly";
        private const string StyleColorAndBar = "ColorAndBar";
        private const string StyleNone = "None";

        // ═══════════════════════════════════════════════════════════
        // 字段
        // ═══════════════════════════════════════════════════════════
        private double _cardWidth = 480;
        private double _cardHeight = 320;

        // ★ 32.0.0：令牌机制（唯一动画状态）
        private int _stateToken = 0;

        private bool _isExpanded;
        private bool _isBoundsAnimating;
        private bool _isAnyAnimationRunning;

        private bool _mouseDownPending;
        private bool _isDragMoved;
        private bool _isUserMoved;
        private Point _dragStartScreen;
        private double _dragStartLeft;
        private double _dragStartTop;
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        private bool _isTouchDragging;
        private int _touchId = -1;

        private readonly DispatcherTimer _autoCollapseTimer;
        private readonly DispatcherTimer _mouseLeaveCollapseTimer;
        private readonly DispatcherTimer _scheduleAlarmTimer;
        private readonly DispatcherTimer _capsuleTextTimer;
        private readonly DispatcherTimer _miniRemindTimer;
        private readonly DispatcherTimer _rollCallAutoCloseTimer;
        private readonly DispatcherTimer _barRotateTimer;

        private readonly DispatcherTimer _radiusTimer;
        private DateTime _radiusStartTime;
        private double _radiusFrom;
        private double _radiusTo;
        private double _radiusDurationMs;
        private Action? _radiusOnComplete;

        private bool _isRollCallActive;

        private AppConfig _config;
        private DateTime _lastAlarmCheckTime = DateTime.MinValue;
        private bool _isTransitioning;
        private bool _clickOutsideToClose = false;

        private string _lastHomeworkRemindFired = "";
        private string _lastDutyRemindFired = "";
        private string _lastTodoCustomRemindFired = "";

        private int _capsuleRotationIndex = -1;
        private string _currentCardTab = "Info";

        private const double NotifyBarRadius = 24;
        private const double NotifyBarMinWidth = 500;
        private const double NotifyBarMinHeight = 130;

        private int _notifyTargetNavIndex = -1;
        private string? _miniRemindOverrideText = null;
        private readonly HashSet<Grid> _marqueeStarted = new HashSet<Grid>();
        private ClassTimerWindow? _timerWindow;

        private bool _isBarExtended;
        private int _barGroupIndex = 0;
        private readonly List<int> _barActiveGroups = new List<int>();
        private double _barScheduleFontSize = 12;

        private bool _barWasExtendedBeforeCard;

        // ═══════════════════════════════════════════════════════════
        // 构造函数
        // ═══════════════════════════════════════════════════════════
        public IslandWindow()
        {
            InitializeComponent();

            _autoCollapseTimer = new DispatcherTimer();
            _autoCollapseTimer.Tick += (s, e) => { _autoCollapseTimer.Stop(); CollapseCard(); };

            _mouseLeaveCollapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MouseLeaveDelayMs) };
            _mouseLeaveCollapseTimer.Tick += (s, e) => { _mouseLeaveCollapseTimer.Stop(); CollapseCard(); };

            _scheduleAlarmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _scheduleAlarmTimer.Tick += AlarmTimer_Tick;
            _scheduleAlarmTimer.Start();

            _capsuleTextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _capsuleTextTimer.Tick += (s, e) => UpdateCapsuleTextAndWidth();
            _capsuleTextTimer.Start();

            _miniRemindTimer = new DispatcherTimer();
            _miniRemindTimer.Tick += (s, e) =>
            {
                _miniRemindTimer.Stop();
                _miniRemindOverrideText = null;
                UpdateCapsuleTextAndWidth();
            };

            _rollCallAutoCloseTimer = new DispatcherTimer();
            _rollCallAutoCloseTimer.Tick += (s, e) =>
            {
                _rollCallAutoCloseTimer.Stop();
                EndRollCallAndCollapse();
            };

            _barRotateTimer = new DispatcherTimer();
            _barRotateTimer.Tick += (s, e) => RotateBarGroup();

            _radiusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _radiusTimer.Tick += RadiusTimer_Tick;

            _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            _clickOutsideToClose = _config.NotifyClickOutsideToClose;
            ApplyCardSizeFromConfig();

            App.SettingsChanged += OnSettingsChanged;
            App.RemindConfigChanged += OnSettingsChanged;
            Deactivated += IslandWindow_Deactivated;
        }

        // ═══════════════════════════════════════════════════════════
        // 状态机 · 唯一入口（只递增令牌）
        // ═══════════════════════════════════════════════════════════
        private void SetIslandState(IslandState newState)
        {
            // 枚举仅作语义标记，不做存储
            _ = newState;
            _stateToken++;
        }

        private int NewStateToken()
        {
            return ++_stateToken;
        }

        private bool IsTokenValid(int token)
        {
            return token == _stateToken;
        }

        private static bool IsBoardActive()
        {
            return App.Board != null && App.Board.IsLoaded;
        }

        private void EnsureDpiCache()
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                _dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                _dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
            }
            catch { _dpiScaleX = 1.0; _dpiScaleY = 1.0; }
        }

        private void ApplyCardSizeFromConfig()
        {
            switch (_config.IslandCardSize)
            {
                case "Small": _cardWidth = 400; _cardHeight = 260; break;
                case "Large": _cardWidth = 560; _cardHeight = 360; break;
                case "XLarge": _cardWidth = 640; _cardHeight = 420; break;
                default: _cardWidth = 480; _cardHeight = 320; break;
            }
        }

        private (double Width, double Height) CalcNotifyBarSize()
        {
            var wa = SystemParameters.WorkArea;
            double wp = _config.NotifyBarWidthPercent;
            if (wp < 50) wp = 96;
            if (wp > 100) wp = 100;
            double hp = _config.NotifyBarHeightPercent;
            if (hp < 8) hp = 12;
            if (hp > 30) hp = 30;
            double w = wa.Width * (wp / 100.0);
            double h = wa.Height * (hp / 100.0);
            if (w < NotifyBarMinWidth) w = NotifyBarMinWidth;
            if (h < NotifyBarMinHeight) h = NotifyBarMinHeight;
            return (w, h);
        }

        private int GetAutoCollapseMs()
        {
            int sec = _config.IslandAutoCollapseSeconds;
            if (sec < 2 || sec > 60) return ClickAutoCollapseMs;
            return sec * 1000;
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                _clickOutsideToClose = _config.NotifyClickOutsideToClose;
                ApplyCardSizeFromConfig();

                if (_isAnyAnimationRunning) return;

                if (_isBarExtended)
                {
                    RebuildBarActiveGroups();
                    RefreshBarContent();
                    ApplyBarFonts();
                    RefreshBarWidthAndMarquee();
                }
                else
                {
                    UpdateCapsuleTextAndWidth();
                    if (!_isExpanded) SyncWindowToContent();
                    else ApplyAdaptiveFontSize();
                }
            }), DispatcherPriority.Background);
        }

        private void IslandWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_clickOutsideToClose && _isExpanded && !_isBoundsAnimating && !_isRollCallActive)
                CollapseCard();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MinWidth = 0;
            MinHeight = 0;
            EnsureDpiCache();
            ApplyCapsuleState();
            SyncWindowToContent();
        }

        private void SyncWindowToContent()
        {
            if (_isTransitioning) return;
            if (_isAnyAnimationRunning) return;

            try
            {
                ContentRow.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = Math.Max(1, ContentRow.DesiredSize.Width);
                double h = Math.Max(1, ContentRow.DesiredSize.Height);

                var wa = SystemParameters.WorkArea;
                SetCurrentValue(WidthProperty, w);
                SetCurrentValue(HeightProperty, h);
                if (!_isUserMoved)
                {
                    SetCurrentValue(LeftProperty, wa.Left + (wa.Width - w) / 2);
                    SetCurrentValue(TopProperty, wa.Top + 4);
                }
            }
            catch { }
        }

        private double GetCircleDiameter()
        {
            double d = _config.IslandCircleDiameter;
            if (d <= 0) d = CircleDiameterDefault;
            if (d < 30) d = 30;
            if (d > 150) d = 150;
            return d;
        }

        // ★ 32.0.0 修正版：同时更新 W/H 和 Left/Top（居中）
        private void SetWindowBoundsForFinal(double contentWidth, double contentHeight)
        {
            double w = Math.Max(20, contentWidth + 8);
            double h = Math.Max(20, contentHeight + 8);
            SetCurrentValue(WidthProperty, w);
            SetCurrentValue(HeightProperty, h);

            if (!_isUserMoved)
            {
                var wa = SystemParameters.WorkArea;
                SetCurrentValue(LeftProperty, wa.Left + (wa.Width - w) / 2);
                SetCurrentValue(TopProperty, wa.Top + 4);
            }
        }

        // 动画完成后的兜底居中（保留）
        private void CenterHorizontally()
        {
            if (_isUserMoved) return;
            try
            {
                var wa = SystemParameters.WorkArea;
                double w = ActualWidth > 0 ? ActualWidth : Width;
                if (double.IsNaN(w) || w <= 0) return;
                SetCurrentValue(LeftProperty, wa.Left + (wa.Width - w) / 2);
                SetCurrentValue(TopProperty, wa.Top + 4);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // ★ B2：通知条图标辅助
        // ═══════════════════════════════════════════════════════════
        private void SetCardIcon(DrawingImage? icon)
        {
            try
            {
                if (icon == null)
                {
                    CardIcon.Source = null;
                    CardIcon.Visibility = Visibility.Collapsed;
                    CardTitle.Margin = new Thickness(0);
                }
                else
                {
                    CardIcon.Source = icon;
                    CardIcon.Visibility = Visibility.Visible;
                    CardTitle.Margin = new Thickness(10, 0, 0, 0);
                }
            }
            catch { }
        }

        private void SetNotifyIconForNav(int targetNavIndex)
        {
            try
            {
                DrawingImage? icon = targetNavIndex switch
                {
                    0 => TryFindResource("IconNotifyInfo") as DrawingImage,
                    1 => TryFindResource("IconNotifyBell") as DrawingImage,
                    2 => TryFindResource("IconNotifyTodo") as DrawingImage,
                    3 => TryFindResource("IconNotifyDuty") as DrawingImage,
                    4 => TryFindResource("IconNotifyHomework") as DrawingImage,
                    _ => null
                };
                SetCardIcon(icon);
            }
            catch { }
        }

        private void SetCardIconByName(string name)
        {
            try
            {
                var icon = TryFindResource(name) as DrawingImage;
                SetCardIcon(icon);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 圆角动画
        // ═══════════════════════════════════════════════════════════
        private void StartRadiusAnimation(double from, double to, double durationMs, Action? onCompleted)
        {
            _radiusTimer.Stop();
            _radiusFrom = from;
            _radiusTo = to;
            _radiusDurationMs = Math.Max(1, durationMs);
            _radiusStartTime = DateTime.Now;
            _radiusOnComplete = onCompleted;

            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);
            MainBorder.CornerRadius = new CornerRadius(from);

            _radiusTimer.Start();
        }

        private void RadiusTimer_Tick(object? sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _radiusStartTime).TotalMilliseconds;
            double t = Math.Min(elapsed / _radiusDurationMs, 1.0);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            double k = easing.Ease(t);
            double r = _radiusFrom + (_radiusTo - _radiusFrom) * k;

            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);
            MainBorder.CornerRadius = new CornerRadius(r);

            if (t >= 1.0)
            {
                _radiusTimer.Stop();
                MainBorder.CornerRadius = new CornerRadius(_radiusTo);
                var cb = _radiusOnComplete;
                _radiusOnComplete = null;
                cb?.Invoke();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 横条宽度与滚动
        // ═══════════════════════════════════════════════════════════
        private FrameworkElement? GetCurrentBarGroupElement()
        {
            if (_barGroupIndex < 0 || _barGroupIndex >= _barActiveGroups.Count) return null;
            int groupId = _barActiveGroups[_barGroupIndex];
            switch (groupId)
            {
                case 0: return BarPanelDate;
                case 1: return BarIcSchedule;
                case 2: return BarPanelStatus;
                case 4:
                    if ((BarIcHomework.ItemsSource as System.Collections.IList)?.Count > 0)
                        return BarIcHomework;
                    return BarTxtHomeworkEmpty;
                case 5: return BarTxtDuty;
                case 6:
                    if ((BarIcTodo.ItemsSource as System.Collections.IList)?.Count > 0)
                        return BarIcTodo;
                    return BarTxtTodoEmpty;
                default: return null;
            }
        }

        private double GetMaxBarWidth()
        {
            var wa = SystemParameters.WorkArea;
            double maxWidth = _config.IslandBarWidth;
            if (maxWidth <= 0) maxWidth = wa.Width * 0.6;
            if (maxWidth > wa.Width * 0.9) maxWidth = wa.Width * 0.9;
            if (maxWidth < BarMinWidth) maxWidth = BarMinWidth;
            return maxWidth;
        }

        private double MeasureCurrentBarContent()
        {
            try
            {
                var el = GetCurrentBarGroupElement();
                if (el == null) return 0;
                if (el.Visibility != Visibility.Visible) return 0;

                el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = el.DesiredSize.Width;
                return w > 0 ? w : 0;
            }
            catch { return 0; }
        }

        private double CalcBarWidth()
        {
            double maxWidth = GetMaxBarWidth();
            double contentW = MeasureCurrentBarContent();

            if (contentW <= 0) return BarMinWidth;

            double target = contentW + BarSidePadding;
            if (target < BarMinWidth) target = BarMinWidth;
            if (target > maxWidth) target = maxWidth;
            return target;
        }

        private void ClearMarquee(FrameworkElement? el)
        {
            try
            {
                if (el?.RenderTransform is TranslateTransform tt)
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = 0;
                }
            }
            catch { }
        }

        private void ClearAllBarMarquees()
        {
            ClearMarquee(BarPanelDate);
            ClearMarquee(BarIcSchedule);
            ClearMarquee(BarPanelStatus);
            ClearMarquee(BarIcHomework);
            ClearMarquee(BarTxtDuty);
            ClearMarquee(BarIcTodo);
            ClearMarquee(BarTxtHomeworkEmpty);
            ClearMarquee(BarTxtTodoEmpty);
        }

        private void ApplyBarContentMarquee(double availableContentWidth)
        {
            try
            {
                ClearAllBarMarquees();

                var el = GetCurrentBarGroupElement();
                if (el == null) return;
                if (el.Visibility != Visibility.Visible) return;

                el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double contentW = el.DesiredSize.Width;

                if (contentW <= availableContentWidth + 1) return;

                double distance = contentW - availableContentWidth;
                double speed = 40.0;
                double dur = distance / speed;
                if (dur < 1) dur = 1;
                if (dur > 12) dur = 12;

                var tt = el.RenderTransform as TranslateTransform;
                if (tt == null)
                {
                    tt = new TranslateTransform(0, 0);
                    el.RenderTransform = tt;
                }

                double d = distance;
                double dDur = dur;

                var anim = new DoubleAnimationUsingKeyFrames
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new EasingDoubleKeyFrame(-d, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dDur)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(-d, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dDur + 1))));
                anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dDur * 2 + 1)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dDur * 2 + 2))));

                tt.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            catch { }
        }

        private void RefreshBarWidthAndMarquee()
        {
            if (!_isBarExtended) return;

            double newW = CalcBarWidth();

            BarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            BarBorder.Width = newW;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    SyncWindowToContent();
                    double avail = newW - BarSidePadding;
                    ApplyBarContentMarquee(avail);
                }
                catch { }
            }), DispatcherPriority.Background);
        }

        private void AfterBarWidthTransition(double newW)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    SyncWindowToContent();
                    CenterHorizontally();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            double avail = newW - BarSidePadding;
                            ApplyBarContentMarquee(avail);
                        }
                        catch { }
                    }), DispatcherPriority.Loaded);
                }
                catch { }
            }), DispatcherPriority.Background);
        }

        // ═══════════════════════════════════════════════════════════
        // 横条伸展 / 收起
        // ═══════════════════════════════════════════════════════════
        private void ExtendBar()
        {
            if (!_config.IslandBarEnabled) return;
            if (_isBarExtended) return;
            if (IsBoardActive()) return;
            if (_isAnyAnimationRunning) return;

            if (_isExpanded)
            {
                CollapseCard();
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CollapseDurationMs + 100) };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    ExtendBar();
                };
                t.Start();
                return;
            }

            SetIslandState(IslandState.Bar);
            _isBarExtended = true;
            _barGroupIndex = 0;
            RebuildBarActiveGroups();

            double d = GetCircleDiameter();
            double maxW = GetMaxBarWidth();

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);

            double startW = MainBorder.Width;
            double startH = MainBorder.Height;
            double startR = MainBorder.CornerRadius.TopLeft;
            if (double.IsNaN(startW) || startW <= 0) startW = GetCapsuleWidth();
            if (double.IsNaN(startH) || startH <= 0) startH = CapsuleHeight;

            CapsuleLabel.Text = "CMW";
            CapsuleLabel.FontSize = Math.Max(10, d * 0.28);
            CapsuleLabel.FontWeight = FontWeights.Bold;

            BarBorder.Visibility = Visibility.Visible;
            BarBorder.Height = d;
            BarBorder.Width = maxW;
            BarBorder.Opacity = 0;

            RefreshBarContent();
            ApplyBarFonts();

            try
            {
                BarBorder.Measure(new Size(maxW, d));
                BarBorder.Arrange(new Rect(0, 0, maxW, d));
                BarBorder.UpdateLayout();
            }
            catch { }

            double targetBarWidth = CalcBarWidth();
            if (double.IsNaN(targetBarWidth) || targetBarWidth < BarMinWidth)
                targetBarWidth = BarMinWidth;

            BarBorder.Width = 0;

            double finalContentW = d + BarBorder.Margin.Left + targetBarWidth;
            double finalContentH = Math.Max(d, CapsuleHeight);
            SetWindowBoundsForFinal(finalContentW, finalContentH);

            int token = NewStateToken();
            _isAnyAnimationRunning = true;
            _isBoundsAnimating = true;

            int doneCount = 0;
            const int totalCount = 4;

            Action onOneDone = () =>
            {
                if (!IsTokenValid(token)) return;
                doneCount++;
                if (doneCount >= totalCount)
                {
                    _isBoundsAnimating = false;
                    _isAnyAnimationRunning = false;
                    SyncWindowToContent();
                    CenterHorizontally();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            double avail = BarBorder.Width - BarSidePadding;
                            ApplyBarContentMarquee(avail);
                        }
                        catch { }
                    }), DispatcherPriority.Loaded);
                }
            };

            var wAnim = new DoubleAnimation(startW, d, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = d; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

            var hAnim = new DoubleAnimation(startH, d, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = d; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

            StartRadiusAnimation(startR, d / 2, BarExtendDurationMs, onOneDone);

            var bwAnim = new DoubleAnimation(0, targetBarWidth, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            bwAnim.Completed += (s, e) => { BarBorder.BeginAnimation(FrameworkElement.WidthProperty, null); BarBorder.Width = targetBarWidth; onOneDone(); };
            BarBorder.BeginAnimation(FrameworkElement.WidthProperty, bwAnim);

            var opAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            opAnim.Completed += (s, e) => { BarBorder.BeginAnimation(OpacityProperty, null); BarBorder.Opacity = 1; };
            BarBorder.BeginAnimation(OpacityProperty, opAnim);

            StartBarRotateTimer();
        }

        private void CollapseBar()
        {
            if (!_isBarExtended) return;
            if (_isAnyAnimationRunning) return;

            SetIslandState(IslandState.Capsule);
            _isBarExtended = false;
            _barRotateTimer.Stop();
            ClearAllBarMarquees();

            double d = GetCircleDiameter();
            double targetCapsuleW = GetCapsuleWidth();

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);

            double startW = MainBorder.Width;
            double startH = MainBorder.Height;
            double startR = MainBorder.CornerRadius.TopLeft;

            double curBarW = BarBorder.Width;
            if (double.IsNaN(curBarW) || curBarW <= 0) curBarW = BarMinWidth;
            double finalContentW = d + BarBorder.Margin.Left + curBarW;
            double finalContentH = Math.Max(d, CapsuleHeight);
            SetWindowBoundsForFinal(finalContentW, finalContentH);

            int token = NewStateToken();
            _isAnyAnimationRunning = true;
            _isBoundsAnimating = true;

            int doneCount = 0;
            const int totalCount = 4;

            Action onOneDone = () =>
            {
                if (!IsTokenValid(token)) return;
                doneCount++;
                if (doneCount >= totalCount)
                {
                    _isBoundsAnimating = false;
                    _isAnyAnimationRunning = false;
                    SyncWindowToContent();
                    CenterHorizontally();
                }
            };

            var wAnim = new DoubleAnimation(startW, targetCapsuleW, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = targetCapsuleW; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

            var hAnim = new DoubleAnimation(startH, CapsuleHeight, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = CapsuleHeight; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

            StartRadiusAnimation(startR, CapsuleRadius, BarExtendDurationMs, onOneDone);

            var bwAnim = new DoubleAnimation(curBarW, 0, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            bwAnim.Completed += (s, e) =>
            {
                BarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                BarBorder.Width = 0;
                BarBorder.Visibility = Visibility.Collapsed;
                onOneDone();
            };
            BarBorder.BeginAnimation(FrameworkElement.WidthProperty, bwAnim);

            var opAnim = new DoubleAnimation(BarBorder.Opacity, 0, TimeSpan.FromMilliseconds(BarExtendDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            opAnim.Completed += (s, e) => { BarBorder.BeginAnimation(OpacityProperty, null); BarBorder.Opacity = 0; };
            BarBorder.BeginAnimation(OpacityProperty, opAnim);

            CapsuleLabel.Text = GetCapsuleText();
            CapsuleLabel.FontSize = 9;
            CapsuleLabel.FontWeight = FontWeights.SemiBold;
        }

        private void SwitchBarGroupWithTransition(int nextIndex)
        {
            if (nextIndex < 0 || nextIndex >= _barActiveGroups.Count) return;
            if (nextIndex == _barGroupIndex) return;
            if (!_isBarExtended) return;
            if (_isAnyAnimationRunning) return;

            var oldEl = GetCurrentBarGroupElement();
            if (oldEl == null)
            {
                _barGroupIndex = nextIndex;
                ShowBarGroup(_barGroupIndex);
                RefreshBarWidthAndMarquee();
                return;
            }

            int token = NewStateToken();
            _isAnyAnimationRunning = true;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(BarGroupFadeOutMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (s, e) =>
            {
                if (!IsTokenValid(token)) return;

                oldEl.BeginAnimation(UIElement.OpacityProperty, null);
                oldEl.Opacity = 1;

                _barGroupIndex = nextIndex;
                ShowBarGroup(_barGroupIndex);

                var newEl = GetCurrentBarGroupElement();

                int innerDone = 0;
                const int innerTotal = 2;

                Action onInnerDone = () =>
                {
                    if (!IsTokenValid(token)) return;
                    innerDone++;
                    if (innerDone >= innerTotal)
                    {
                        _isAnyAnimationRunning = false;
                        AfterBarWidthTransition(BarBorder.Width);
                    }
                };

                StartBarWidthTransition(token, onInnerDone);

                if (newEl != null)
                {
                    newEl.BeginAnimation(UIElement.OpacityProperty, null);
                    newEl.Opacity = 0;

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(BarGroupFadeInMs))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.Stop
                    };
                    fadeIn.Completed += (s2, e2) =>
                    {
                        if (!IsTokenValid(token)) return;
                        newEl.BeginAnimation(UIElement.OpacityProperty, null);
                        newEl.Opacity = 1;
                        onInnerDone();
                    };
                    newEl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                else
                {
                    onInnerDone();
                }
            };

            oldEl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void StartBarWidthTransition(int token, Action onCompleted)
        {
            double newW = CalcBarWidth();

            BarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            double oldW = BarBorder.Width;
            if (double.IsNaN(oldW) || oldW <= 0) oldW = BarMinWidth;

            if (Math.Abs(newW - oldW) < 1.0)
            {
                BarBorder.Width = newW;
                onCompleted();
                return;
            }

            var anim = new DoubleAnimation(oldW, newW, TimeSpan.FromMilliseconds(BarGroupWidthTransitionMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            anim.Completed += (s, e) =>
            {
                if (!IsTokenValid(token)) return;
                BarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                BarBorder.Width = newW;
                onCompleted();
            };
            BarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }
        // ═══════════════════════════════════════════════════════════
        // 横条内容 · 组重建
        // ═══════════════════════════════════════════════════════════
        private void RebuildBarActiveGroups()
        {
            _barActiveGroups.Clear();

            Func<int, bool> isGroupEnabled = id => id switch
            {
                0 => _config.IslandBarShowDateGroup,
                1 => _config.IslandBarShowScheduleGroup,
                2 => _config.IslandBarShowStatusGroup,
                4 => _config.IslandBarShowHomeworkGroup,
                5 => _config.IslandBarShowDutyGroup,
                6 => _config.IslandBarShowTodoGroup,
                _ => false
            };

            var order = _config.IslandBarGroupOrder;
            if (order == null || order.Count == 0)
                order = new List<int> { 0, 1, 2, 4, 5, 6 };

            var allIds = new[] { 0, 1, 2, 4, 5, 6 };
            foreach (var id in allIds)
            {
                if (!order.Contains(id)) order.Add(id);
            }

            foreach (var id in order)
            {
                if (id != 0 && id != 1 && id != 2 && id != 4 && id != 5 && id != 6) continue;
                if (!isGroupEnabled(id)) continue;
                _barActiveGroups.Add(id);
            }

            if (_barActiveGroups.Count == 0) _barActiveGroups.Add(0);
        }

        private void StartBarRotateTimer()
        {
            _barRotateTimer.Stop();
            if (!_config.IslandBarAutoRotateEnabled) return;
            int sec = _config.IslandBarRotateSeconds;
            if (sec < 3) sec = 8;
            if (sec > 60) sec = 60;
            _barRotateTimer.Interval = TimeSpan.FromSeconds(sec);
            _barRotateTimer.Start();
        }

        private void RotateBarGroup()
        {
            if (_barActiveGroups.Count <= 1) return;
            int next = (_barGroupIndex + 1) % _barActiveGroups.Count;
            SwitchBarGroupWithTransition(next);
        }

        private void ShowBarGroup(int index)
        {
            if (index < 0 || index >= _barActiveGroups.Count) return;

            ClearAllBarMarquees();

            BarPanelDate.Visibility = Visibility.Collapsed;
            BarIcSchedule.Visibility = Visibility.Collapsed;
            BarPanelStatus.Visibility = Visibility.Collapsed;
            BarIcHomework.Visibility = Visibility.Collapsed;
            BarTxtHomeworkEmpty.Visibility = Visibility.Collapsed;
            BarTxtDuty.Visibility = Visibility.Collapsed;
            BarIcTodo.Visibility = Visibility.Collapsed;
            BarTxtTodoEmpty.Visibility = Visibility.Collapsed;

            int groupId = _barActiveGroups[index];
            switch (groupId)
            {
                case 0: BarPanelDate.Visibility = Visibility.Visible; break;
                case 1: BarIcSchedule.Visibility = Visibility.Visible; break;
                case 2: BarPanelStatus.Visibility = Visibility.Visible; break;
                case 4:
                    if ((BarIcHomework.ItemsSource as System.Collections.IList)?.Count > 0)
                        BarIcHomework.Visibility = Visibility.Visible;
                    else
                        BarTxtHomeworkEmpty.Visibility = Visibility.Visible;
                    break;
                case 5: BarTxtDuty.Visibility = Visibility.Visible; break;
                case 6:
                    if ((BarIcTodo.ItemsSource as System.Collections.IList)?.Count > 0)
                        BarIcTodo.Visibility = Visibility.Visible;
                    else
                        BarTxtTodoEmpty.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void RefreshBarContent()
        {
            RefreshBarDate();
            RefreshBarSchedule();
            RefreshBarStatus();
            RefreshBarHomework();
            RefreshBarDuty();
            RefreshBarTodo();
            ShowBarGroup(_barGroupIndex);
            ApplyBarFonts();
        }

        private void ApplyBarFonts()
        {
            try
            {
                double d = GetCircleDiameter();
                double baseSize = 14;
                if (d < 36) baseSize = 12;
                else if (d < 44) baseSize = 13;

                BarTxtDate.FontSize = baseSize;
                BarTxtWeekday.FontSize = baseSize;
                BarTxtStatus.FontSize = baseSize;
                BarTxtCountdown.FontSize = baseSize;
                BarTxtDot1.FontSize = baseSize;
                BarTxtDot2.FontSize = baseSize;
                BarTxtNext.FontSize = baseSize;
                BarTxtHomeworkEmpty.FontSize = baseSize;
                BarTxtDuty.FontSize = baseSize + 2;
                BarTxtTodoEmpty.FontSize = baseSize;
            }
            catch { }
        }

        private void RefreshBarDate()
        {
            try
            {
                var now = DateTime.Now;
                BarTxtDate.Text = now.ToString("M月d日");
                string[] weekNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
                BarTxtWeekday.Text = weekNames[(int)now.DayOfWeek];
            }
            catch { }
        }

        private void RefreshBarSchedule()
        {
            try
            {
                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
                var slots = _config.TimeSlots;
                if (slots == null || slots.Count == 0) slots = DefaultTimeSlots.Create();

                DateTime today = DateTime.Today;
                int weekDay = (int)today.DayOfWeek;
                string todayStr = today.ToString("yyyy-MM-dd");
                DateTime now = DateTime.Now;

                if (weekDay < 1 || weekDay > 6)
                {
                    BarIcSchedule.ItemsSource = new List<BarScheduleVm>();
                    return;
                }

                int effectiveWeekDay = weekDay;
                if (schedule.DayOverrides != null)
                {
                    var dayOv = schedule.DayOverrides.FirstOrDefault(x => x.Date == todayStr);
                    if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                        effectiveWeekDay = dayOv.SourceWeekDay;
                }

                var items = new List<BarScheduleVm>();
                for (int i = 0; i < slots.Count; i++)
                {
                    string slotName = slots[i].Name ?? "";
                    if (!IsDisplaySlot(slotName)) continue;

                    int period = i + 1;
                    var ov = schedule.Overrides?.FirstOrDefault(x => x.Date == todayStr && x.Period == period && x.WeekDay == weekDay);
                    string lesson = ov?.LessonName ?? "";

                    if (string.IsNullOrWhiteSpace(lesson))
                    {
                        var item = schedule.WeekBase?.FirstOrDefault(x => x.WeekDay == effectiveWeekDay && x.Period == period);
                        lesson = item?.LessonName ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(lesson))
                    {
                        items.Add(new BarScheduleVm
                        {
                            ShortName = "—",
                            Bg = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                            Fg = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                            FontSize = _barScheduleFontSize
                        });
                        continue;
                    }

                    string shortName = GetBarShortName(lesson);
                    bool isCurrent = false, isEnded = false;
                    if (TimeSpan.TryParse(slots[i].StartTime, out var start) && TimeSpan.TryParse(slots[i].EndTime, out var end))
                    {
                        if (now.TimeOfDay >= start && now.TimeOfDay < end) isCurrent = true;
                        else if (now.TimeOfDay >= end) isEnded = true;
                    }

                    Brush bg, fg;
                    if (isCurrent) { bg = new SolidColorBrush(Color.FromRgb(0x1E, 0x5F, 0xD0)); fg = Brushes.White; }
                    else if (isEnded) { bg = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)); fg = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)); }
                    else { bg = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)); fg = new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66)); }

                    items.Add(new BarScheduleVm { ShortName = shortName, Bg = bg, Fg = fg, FontSize = _barScheduleFontSize });
                }

                BarIcSchedule.ItemsSource = items;
            }
            catch { }
        }

        private static bool IsDisplaySlot(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();
            if (name.Contains("晚自习")) return true;
            if (name.StartsWith("第") && name.EndsWith("节"))
            {
                string mid = name.Substring(1, name.Length - 2);
                if (int.TryParse(mid, out int n) && n >= 1 && n <= 8) return true;
            }
            return false;
        }

        private string GetBarShortName(string lesson)
        {
            if (string.IsNullOrWhiteSpace(lesson)) return "—";
            var map = _config.LeftScheduleSubjectMap;
            if (map != null && map.TryGetValue(lesson, out string? shortName) && !string.IsNullOrWhiteSpace(shortName))
                return shortName;
            return lesson.Substring(0, 1);
        }

        private void RefreshBarStatus()
        {
            try
            {
                var slots = _config.TimeSlots;
                if (slots == null || slots.Count == 0) slots = DefaultTimeSlots.Create();

                DateTime now = DateTime.Now;
                int weekDay = (int)now.DayOfWeek;

                if (weekDay < 1 || weekDay > 6)
                {
                    BarTxtStatus.Text = "休息日";
                    BarTxtCountdown.Text = "";
                    BarTxtDot1.Visibility = Visibility.Collapsed;
                    BarTxtDot2.Visibility = Visibility.Collapsed;
                    BarTxtNext.Text = "";
                    return;
                }

                string todayStr = now.ToString("yyyy-MM-dd");
                int currentIdx = -1;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (TimeSpan.TryParse(slots[i].StartTime, out var s) && TimeSpan.TryParse(slots[i].EndTime, out var e))
                    {
                        if (now.TimeOfDay >= s && now.TimeOfDay < e) { currentIdx = i; break; }
                    }
                }

                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
                int effectiveWeekDay = weekDay;
                if (schedule.DayOverrides != null)
                {
                    var dayOv = schedule.DayOverrides.FirstOrDefault(x => x.Date == todayStr);
                    if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                        effectiveWeekDay = dayOv.SourceWeekDay;
                }

                string GetLesson(int idx)
                {
                    if (idx < 0 || idx >= slots.Count) return "";
                    int period = idx + 1;
                    var ov = schedule.Overrides?.FirstOrDefault(x => x.Date == todayStr && x.Period == period && x.WeekDay == weekDay);
                    if (ov != null && !string.IsNullOrWhiteSpace(ov.LessonName)) return ov.LessonName;
                    var item = schedule.WeekBase?.FirstOrDefault(x => x.WeekDay == effectiveWeekDay && x.Period == period);
                    return item?.LessonName ?? "";
                }

                if (currentIdx >= 0)
                {
                    string slotName = slots[currentIdx].Name ?? "";
                    BarTxtStatus.Text = slots[currentIdx].IsLongBreak ? slotName : "上课中";

                    if (TimeSpan.TryParse(slots[currentIdx].EndTime, out var end))
                    {
                        var remain = now.Date.Add(end) - now;
                        BarTxtCountdown.Text = remain.TotalMinutes > 0 ? $"剩 {(int)Math.Ceiling(remain.TotalMinutes)} 分" : "";
                    }

                    string nextLesson = "";
                    for (int i = currentIdx + 1; i < slots.Count; i++)
                    {
                        string lesson = GetLesson(i);
                        if (!string.IsNullOrWhiteSpace(lesson)) { nextLesson = lesson; break; }
                    }

                    BarTxtDot1.Visibility = string.IsNullOrEmpty(BarTxtCountdown.Text) ? Visibility.Collapsed : Visibility.Visible;
                    if (!string.IsNullOrWhiteSpace(nextLesson))
                    {
                        BarTxtDot2.Visibility = Visibility.Visible;
                        BarTxtNext.Text = $"下节 {nextLesson}";
                    }
                    else
                    {
                        BarTxtDot2.Visibility = Visibility.Collapsed;
                        BarTxtNext.Text = "";
                    }
                }
                else
                {
                    int nextIdx = -1;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (TimeSpan.TryParse(slots[i].StartTime, out var s))
                        {
                            if (now.Date.Add(s) > now) { nextIdx = i; break; }
                        }
                    }

                    if (nextIdx >= 0)
                    {
                        var startTime = now.Date.Add(TimeSpan.Parse(slots[nextIdx].StartTime));
                        var remain = startTime - now;
                        BarTxtStatus.Text = "课间";
                        BarTxtCountdown.Text = remain.TotalHours >= 1 ? $"距下节 {(int)Math.Ceiling(remain.TotalHours)} 时" : $"距下节 {(int)Math.Ceiling(remain.TotalMinutes)} 分";

                        string lesson = GetLesson(nextIdx);
                        if (!string.IsNullOrWhiteSpace(lesson))
                        {
                            BarTxtDot1.Visibility = Visibility.Visible;
                            BarTxtDot2.Visibility = Visibility.Visible;
                            BarTxtNext.Text = $"下节 {lesson}";
                        }
                        else
                        {
                            BarTxtDot1.Visibility = Visibility.Visible;
                            BarTxtDot2.Visibility = Visibility.Collapsed;
                            BarTxtNext.Text = "";
                        }
                    }
                    else
                    {
                        BarTxtStatus.Text = "今日课程结束";
                        BarTxtCountdown.Text = "";
                        BarTxtDot1.Visibility = Visibility.Collapsed;
                        BarTxtDot2.Visibility = Visibility.Collapsed;
                        BarTxtNext.Text = "";
                    }
                }
            }
            catch { }
        }

        private void RefreshBarHomework()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                var todayList = list
                    .Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder)
                    .ThenBy(h => h.EndDate)
                    .ToList();

                var vms = new List<BarItemVm>();
                foreach (var h in todayList)
                {
                    string txt = h.HomeworkName;
                    if (!string.IsNullOrWhiteSpace(h.Subject)) txt += $"·{h.Subject}";

                    Brush fg = h.Priority switch
                    {
                        "VeryUrgent" => new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x66)),
                        "Urgent" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x66)),
                        _ => new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE))
                    };

                    vms.Add(new BarItemVm { Text = txt, Fg = fg, FontSize = 13 });
                }

                BarIcHomework.ItemsSource = vms;
            }
            catch { }
        }

        private void RefreshBarDuty()
        {
            try
            {
                string groupName = DutyPage.GetTodayDutyGroupName();
                BarTxtDuty.Text = string.IsNullOrWhiteSpace(groupName)
                    ? "今日无值日"
                    : $"今日 {groupName} 组值日";
            }
            catch
            {
                BarTxtDuty.Text = "今日无值日";
            }
        }

        private void RefreshBarTodo()
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo) ?? new List<TodoItem>();
                var today = DateTime.Today;
                var todayList = list
                    .Where(t => t.Deadline.HasValue
                             && t.Deadline.Value.Date == today
                             && !t.IsCompleted)
                    .ToList();

                var vms = new List<BarItemVm>();
                foreach (var t in todayList)
                {
                    vms.Add(new BarItemVm
                    {
                        Text = string.IsNullOrWhiteSpace(t.Content) ? "（无内容）" : t.Content,
                        Fg = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                        FontSize = 13
                    });
                }

                BarIcTodo.ItemsSource = vms;
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 变蓝
        // ═══════════════════════════════════════════════════════════
        private async void FlashBlue(int seconds)
        {
            try
            {
                var fromColor = ((SolidColorBrush)MainBorder.Background).Color;
                var toColor = Color.FromRgb(0x1E, 0x5F, 0xD0);

                var brush = new SolidColorBrush(fromColor);
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(toColor, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
                MainBorder.Background = brush;

                await Task.Delay(seconds * 1000);

                var back = new SolidColorBrush(toColor);
                back.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(fromColor, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
                MainBorder.Background = back;
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 胶囊文字
        // ═══════════════════════════════════════════════════════════
        private double GetCapsuleWidth()
        {
            string text = CapsuleLabel.Text ?? "CMW";
            double textWidth = MeasureCapsuleTextWidth(text);
            double w = textWidth + CapsuleTextPadding;
            if (w < CapsuleMinWidth) w = CapsuleMinWidth;
            if (w > CapsuleMaxWidth) w = CapsuleMaxWidth;
            return w;
        }

        private double MeasureCapsuleTextWidth(string text)
        {
            try
            {
                FontFamily fontFamily;
                try { fontFamily = (FontFamily)FindResource("GlobalFontFamily"); }
                catch { fontFamily = new FontFamily("Microsoft YaHei UI"); }
                var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                var ft = new FormattedText(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 9, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                return ft.Width;
            }
            catch { return 30; }
        }

        private void UpdateCapsuleTextAndWidth()
        {
            if (_isExpanded || _isAnyAnimationRunning) return;
            if (_isBarExtended) return;

            if (_miniRemindOverrideText != null)
            {
                CapsuleLabel.Text = _miniRemindOverrideText;
                double tw = GetCapsuleWidth();
                MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                MainBorder.Width = tw;
                SyncWindowToContent();
                CenterHorizontally();
                return;
            }

            string newText = GetCapsuleText();
            if (CapsuleLabel.Text == newText) return;

            CapsuleLabel.Text = newText;
            double targetWidth = GetCapsuleWidth();
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.Width = targetWidth;
            SyncWindowToContent();
            CenterHorizontally();
        }

        private void ShowMiniRemindInCapsule(string text, int seconds)
        {
            try
            {
                _miniRemindOverrideText = text;
                CapsuleLabel.Text = text;
                double w = GetCapsuleWidth();
                MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                MainBorder.Width = w;
                SyncWindowToContent();
                CenterHorizontally();

                _miniRemindTimer.Stop();
                int s = Math.Max(2, Math.Min(30, seconds));
                _miniRemindTimer.Interval = TimeSpan.FromSeconds(s);
                _miniRemindTimer.Start();
            }
            catch { }
        }

        private string GetCapsuleText()
        {
            string mode = _config.IslandCapsuleTextMode ?? "CMW";
            switch (mode)
            {
                case "Countdown": return GetCountdownText();
                case "NextClass": return GetNextClassText();
                case "HomeworkCount": return GetHomeworkCountText();
                case "Auto": return GetAutoRotatingText();
                default: return "CMW";
            }
        }

        private List<TimeSlot> GetTimeSlots()
        {
            var slots = _config.TimeSlots;
            if (slots == null || slots.Count == 0) slots = DefaultTimeSlots.Create();
            return slots;
        }

        private string GetCountdownText()
        {
            DateTime now = DateTime.Now;
            var slots = GetTimeSlots();
            foreach (var slot in slots)
            {
                if (TryParseTime(slot.StartTime, out var s) && TryParseTime(slot.EndTime, out var en))
                {
                    var start = now.Date.Add(s);
                    var end = now.Date.Add(en);
                    if (now >= start && now < end)
                        return $"距下课 {(int)Math.Ceiling((end - now).TotalMinutes)}分";
                }
            }
            foreach (var slot in slots)
            {
                if (TryParseTime(slot.StartTime, out var s))
                {
                    var start = now.Date.Add(s);
                    if (start > now)
                    {
                        var remain = start - now;
                        if (remain.TotalMinutes < 60) return $"距上课 {(int)Math.Ceiling(remain.TotalMinutes)}分";
                        else return $"距上课 {(int)Math.Ceiling(remain.TotalHours)}时";
                    }
                }
            }
            return "今日结束";
        }

        private string GetNextClassText()
        {
            DateTime now = DateTime.Now;
            int weekDay = (int)now.DayOfWeek;
            if (weekDay < 1 || weekDay > 6) return "今日结束";
            var slots = GetTimeSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (!TryParseTime(slots[i].StartTime, out var s)) continue;
                var startTime = now.Date.Add(s);
                if (startTime <= now) continue;
                string lesson = GetLessonNameByPeriod(i, weekDay);
                if (!string.IsNullOrWhiteSpace(lesson)) return $"下节 {lesson}";
            }
            return "今日结束";
        }

        private string GetHomeworkCountText()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                int count = list.Count(h => h.IsActiveOn(today) && !h.IsCompleted);
                if (count == 0) return "无作业";
                return $"{count} 项作业";
            }
            catch { return "CMW"; }
        }

        private string GetAutoRotatingText()
        {
            var candidates = new List<string>();
            DateTime now = DateTime.Now;
            var slots = GetTimeSlots();
            int weekDay = (int)now.DayOfWeek;

            if (weekDay >= 1 && weekDay <= 6)
            {
                int currentSlotIdx = -1;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (TryParseTime(slots[i].StartTime, out var s) && TryParseTime(slots[i].EndTime, out var en))
                    {
                        if (now >= now.Date.Add(s) && now < now.Date.Add(en)) { currentSlotIdx = i; break; }
                    }
                }
                if (currentSlotIdx >= 0)
                {
                    if (TryParseTime(slots[currentSlotIdx].EndTime, out var en))
                    {
                        var end = now.Date.Add(en);
                        candidates.Add($"距下课 {(int)Math.Ceiling((end - now).TotalMinutes)}分");
                    }
                    string lesson = GetLessonNameByPeriod(currentSlotIdx, weekDay);
                    if (!string.IsNullOrWhiteSpace(lesson)) candidates.Add($"本节课 {lesson}");
                }
                else
                {
                    int lastIdx = -1;
                    for (int i = slots.Count - 1; i >= 0; i--)
                    {
                        if (TryParseTime(slots[i].EndTime, out var en))
                        {
                            if (now.Date.Add(en) < now) { lastIdx = i; break; }
                        }
                    }
                    if (lastIdx >= 0)
                    {
                        string lastLesson = GetLessonNameByPeriod(lastIdx, weekDay);
                        if (!string.IsNullOrWhiteSpace(lastLesson)) candidates.Add($"上节 {lastLesson}");
                    }
                    int nextIdx = -1;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (TryParseTime(slots[i].StartTime, out var s))
                        {
                            if (now.Date.Add(s) > now) { nextIdx = i; break; }
                        }
                    }
                    if (nextIdx >= 0)
                    {
                        string nextLesson = GetLessonNameByPeriod(nextIdx, weekDay);
                        if (!string.IsNullOrWhiteSpace(nextLesson)) candidates.Add($"下节 {nextLesson}");
                    }
                }
            }

            string todoText = GetTodoCountText();
            if (!string.IsNullOrWhiteSpace(todoText) && todoText != "无待办" && todoText != "CMW") candidates.Add(todoText);
            string hwText = GetHomeworkCountText();
            if (!string.IsNullOrWhiteSpace(hwText) && hwText != "无作业" && hwText != "CMW") candidates.Add(hwText);
            string dutyText = GetDutyText();
            if (!string.IsNullOrWhiteSpace(dutyText)) candidates.Add(dutyText);

            if (candidates.Count == 0) return "CMW";
            if (candidates.Count == 1) return candidates[0];

            _capsuleRotationIndex++;
            if (_capsuleRotationIndex < 0 || _capsuleRotationIndex >= candidates.Count) _capsuleRotationIndex = 0;
            return candidates[_capsuleRotationIndex];
        }

        private string GetLessonNameByPeriod(int periodIndexZeroBased, int weekDay)
        {
            try
            {
                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                int period = periodIndexZeroBased + 1;
                int effectiveWeekDay = weekDay;
                if (schedule.DayOverrides != null)
                {
                    var dayOv = schedule.DayOverrides.FirstOrDefault(x => x.Date == today);
                    if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                        effectiveWeekDay = dayOv.SourceWeekDay;
                }
                var ov = schedule.Overrides?.FirstOrDefault(x => x.Date == today && x.Period == period && x.WeekDay == weekDay);
                if (ov != null && !string.IsNullOrWhiteSpace(ov.LessonName)) return ov.LessonName;
                var item = schedule.WeekBase?.FirstOrDefault(x => x.WeekDay == effectiveWeekDay && x.Period == period);
                return item?.LessonName ?? "";
            }
            catch { return ""; }
        }

        private string GetTodoCountText()
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo) ?? new List<TodoItem>();
                var today = DateTime.Today;
                int count = list.Count(t => t.Deadline.HasValue && t.Deadline.Value.Date == today && !t.IsCompleted);
                if (count == 0) return "无待办";
                return $"{count} 项待办";
            }
            catch { return "CMW"; }
        }

        private string GetDutyText()
        {
            try
            {
                var duty = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
                if (duty == null || string.IsNullOrWhiteSpace(duty.TodayDutyGroupName)) return "";
                return $"值日 {duty.TodayDutyGroupName}";
            }
            catch { return ""; }
        }

        // ═══════════════════════════════════════════════════════════
        // 胶囊状态
        // ═══════════════════════════════════════════════════════════
        private void ApplyCapsuleState()
        {
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);
            double w = GetCapsuleWidth();
            MainBorder.Width = w;
            MainBorder.Height = CapsuleHeight;
            MainBorder.CornerRadius = new CornerRadius(CapsuleRadius);
            MainBorder.VerticalAlignment = VerticalAlignment.Top;
        }

        public void ShowWithExpandAnimation()
        {
            if (IsBoardActive()) { Hide(); return; }

            _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            _clickOutsideToClose = _config.NotifyClickOutsideToClose;
            ApplyCardSizeFromConfig();

            SetIslandState(IslandState.Capsule);
            _isUserMoved = false;
            _isBarExtended = false;
            _isExpanded = false;
            _isRollCallActive = false;
            _barWasExtendedBeforeCard = false;

            BarBorder.Visibility = Visibility.Collapsed;
            BarBorder.Width = 0;
            BarBorder.Opacity = 0;
            ClearAllBarMarquees();

            CapsuleLabel.Text = GetCapsuleText();
            CapsuleLabel.BeginAnimation(OpacityProperty, null);
            CardContent.BeginAnimation(OpacityProperty, null);
            CapsuleLabel.Opacity = 1;
            CardContent.Opacity = 0;
            CardContent.IsHitTestVisible = false;
            CardHintText.Visibility = Visibility.Collapsed;

            ApplyCapsuleState();

            Show();
            SyncWindowToContent();

            RootGrid.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(0.6, 0.6);
            RootGrid.RenderTransform = scale;
            Opacity = 0;

            var duration = TimeSpan.FromMilliseconds(280);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var sx = new DoubleAnimation(0.6, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var sy = new DoubleAnimation(0.6, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var op = new DoubleAnimation(0, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };

            sx.Completed += (s, e) => { scale.ScaleX = 1; };
            sy.Completed += (s, e) => { scale.ScaleY = 1; };
            op.Completed += (s, e) => { Opacity = 1; };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
            BeginAnimation(OpacityProperty, op);
        }

        public void ShowCardFromBar()
        {
            try { if (!_isExpanded) ExpandCard(GetAutoCollapseMs(), showHint: _clickOutsideToClose); }
            catch { }
        }

        private void ExpandCard(int autoCollapseMs, bool showHint)
        {
            if (_isExpanded || _isAnyAnimationRunning) return;

            SetIslandState(IslandState.Card);
            _isExpanded = true;
            _isAnyAnimationRunning = true;
            _isBoundsAnimating = true;

            _barWasExtendedBeforeCard = _isBarExtended;

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);

            double startWidth = MainBorder.Width;
            double startHeight = MainBorder.Height;
            double startRadius = MainBorder.CornerRadius.TopLeft;
            if (double.IsNaN(startWidth) || startWidth <= 0) startWidth = GetCapsuleWidth();
            if (double.IsNaN(startHeight) || startHeight <= 0) startHeight = CapsuleHeight;

            if (_isBarExtended)
            {
                ClearAllBarMarquees();
                BarBorder.Visibility = Visibility.Collapsed;
                BarBorder.Width = 0;
                BarBorder.Opacity = 0;
                _isBarExtended = false;
                _barRotateTimer.Stop();
            }

            ResetPanelsToMenu();
            SetContentState(capsule: false, animated: true, durationMs: 300);
            CardContent.IsHitTestVisible = true;
            CardHintText.Text = showHint ? "单击弹窗外区域收回" : "";
            CardHintText.Visibility = showHint ? Visibility.Visible : Visibility.Collapsed;

            SetWindowBoundsForFinal(_cardWidth, _cardHeight);

            double _startW = startWidth;
            double _startH = startHeight;
            double _startR = startRadius;

            int token = NewStateToken();
            int doneCount = 0;
            const int totalCount = 3;

            Action onOneDone = () =>
            {
                if (!IsTokenValid(token)) return;
                doneCount++;
                if (doneCount >= totalCount)
                {
                    _isBoundsAnimating = false;
                    _isAnyAnimationRunning = false;
                    SyncWindowToContent();
                    CenterHorizontally();
                }
            };

            var wAnim = new DoubleAnimation(_startW, _cardWidth, TimeSpan.FromMilliseconds(ExpandDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = _cardWidth; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

            var hAnim = new DoubleAnimation(_startH, _cardHeight, TimeSpan.FromMilliseconds(ExpandDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = _cardHeight; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

            StartRadiusAnimation(_startR, CardRadius, ExpandDurationMs, onOneDone);

            _autoCollapseTimer.Stop();
            if (!_clickOutsideToClose && autoCollapseMs > 0)
            {
                _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(autoCollapseMs);
                _autoCollapseTimer.Start();
            }
        }

        private void ExpandNotifyCard()
        {
            if (_isExpanded || _isAnyAnimationRunning) return;

            SetIslandState(IslandState.Notify);
            _isExpanded = true;
            _isAnyAnimationRunning = true;
            _isBoundsAnimating = true;

            _barWasExtendedBeforeCard = _isBarExtended;

            if (_isBarExtended)
            {
                ClearAllBarMarquees();
                BarBorder.Visibility = Visibility.Collapsed;
                BarBorder.Width = 0;
                BarBorder.Opacity = 0;
                _isBarExtended = false;
                _barRotateTimer.Stop();
            }

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);

            double startWidth = MainBorder.Width;
            double startHeight = MainBorder.Height;
            double startRadius = MainBorder.CornerRadius.TopLeft;
            if (double.IsNaN(startWidth) || startWidth <= 0) startWidth = GetCapsuleWidth();
            if (double.IsNaN(startHeight) || startHeight <= 0) startHeight = CapsuleHeight;

            var (targetW, targetH) = CalcNotifyBarSize();

            SetWindowBoundsForFinal(targetW, targetH);

            SetContentState(capsule: false, animated: true, durationMs: 320);
            CardContent.IsHitTestVisible = true;
            CardHintText.Visibility = Visibility.Collapsed;

            double _startW = startWidth;
            double _startH = startHeight;
            double _startR = startRadius;

            int token = NewStateToken();
            int doneCount = 0;
            const int totalCount = 3;

            Action onOneDone = () =>
            {
                if (!IsTokenValid(token)) return;
                doneCount++;
                if (doneCount >= totalCount)
                {
                    _isBoundsAnimating = false;
                    _isAnyAnimationRunning = false;
                    SyncWindowToContent();
                    CenterHorizontally();
                }
            };

            var wAnim = new DoubleAnimation(_startW, targetW, TimeSpan.FromMilliseconds(ExpandDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = targetW; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

            var hAnim = new DoubleAnimation(_startH, targetH, TimeSpan.FromMilliseconds(ExpandDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = targetH; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

            StartRadiusAnimation(_startR, NotifyBarRadius, ExpandDurationMs, onOneDone);

            _autoCollapseTimer.Stop();
            if (!_clickOutsideToClose)
            {
                _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(NotifyAutoCollapseMs);
                _autoCollapseTimer.Start();
            }
        }

        private void ResetPanelsToMenu()
        {
            _notifyTargetNavIndex = -1;
            SetCardIconByName("IconHome");
            CardTitle.Text = "CMW 菜单";
            _currentCardTab = "Info";

            if (CardTabInfo != null && !CardTabInfo.IsChecked.GetValueOrDefault())
                CardTabInfo.IsChecked = true;

            PanelInfo.Visibility = Visibility.Visible;
            PanelNotify.Visibility = Visibility.Collapsed;
            PanelHomework.Visibility = Visibility.Collapsed;
            PanelTodo.Visibility = Visibility.Collapsed;
            PanelSchedule.Visibility = Visibility.Collapsed;
            PanelNotes.Visibility = Visibility.Collapsed;
            PanelRollCall.Visibility = Visibility.Collapsed;
            CardTabs.Visibility = Visibility.Visible;
            _isRollCallActive = false;
        }

        private void CollapseCard()
        {
            if (!_isExpanded || _isAnyAnimationRunning) return;

            SetIslandState(IslandState.Capsule);
            _isExpanded = false;
            _autoCollapseTimer.Stop();
            _mouseLeaveCollapseTimer.Stop();

            if (_isRollCallActive) StopRollCallAndRestoreTabs();

            _isAnyAnimationRunning = true;
            _isBoundsAnimating = true;

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);

            double startWidth = MainBorder.Width;
            double startHeight = MainBorder.Height;
            double startRadius = MainBorder.CornerRadius.TopLeft;
            if (double.IsNaN(startWidth) || startWidth <= 0) startWidth = _cardWidth;
            if (double.IsNaN(startHeight) || startHeight <= 0) startHeight = _cardHeight;

            SetContentState(capsule: true, animated: true, durationMs: 260);
            CardContent.IsHitTestVisible = false;
            CardHintText.Visibility = Visibility.Collapsed;

            double targetWidth = GetCapsuleWidth();
            double targetHeight = CapsuleHeight;
            double targetRadius = CapsuleRadius;

            double winW = Math.Max(startWidth, _cardWidth);
            double winH = Math.Max(startHeight, _cardHeight);
            SetWindowBoundsForFinal(winW, winH);

            double _startW = startWidth;
            double _startH = startHeight;
            double _startR = startRadius;
            double _targetW = targetWidth;
            double _targetH = targetHeight;
            double _targetR = targetRadius;

            int token = NewStateToken();
            int doneCount = 0;
            const int totalCount = 3;

            Action onOneDone = () =>
            {
                if (!IsTokenValid(token)) return;
                doneCount++;
                if (doneCount >= totalCount)
                {
                    CapsuleLabel.BeginAnimation(OpacityProperty, null);
                    CardContent.BeginAnimation(OpacityProperty, null);
                    CapsuleLabel.Opacity = 1;
                    CardContent.Opacity = 0;
                    ApplyCapsuleState();
                    _isBoundsAnimating = false;
                    _isAnyAnimationRunning = false;
                    SyncWindowToContent();
                    CenterHorizontally();

                    if (_barWasExtendedBeforeCard)
                    {
                        _barWasExtendedBeforeCard = false;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { ExtendBar(); } catch { }
                        }), DispatcherPriority.Background);
                    }
                }
            };

            var wAnim = new DoubleAnimation(_startW, _targetW, TimeSpan.FromMilliseconds(CollapseDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = _targetW; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

            var hAnim = new DoubleAnimation(_startH, _targetH, TimeSpan.FromMilliseconds(CollapseDurationMs))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = _targetH; onOneDone(); };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

            StartRadiusAnimation(_startR, _targetR, CollapseDurationMs, onOneDone);
        }

        private void SetContentState(bool capsule, bool animated, double durationMs)
        {
            CapsuleLabel.BeginAnimation(OpacityProperty, null);
            CardContent.BeginAnimation(OpacityProperty, null);
            double capsuleTarget = capsule ? 1.0 : 0.0;
            double cardTarget = capsule ? 0.0 : 1.0;

            if (animated)
            {
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
                var duration = TimeSpan.FromMilliseconds(durationMs);
                CapsuleLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(capsuleTarget, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
                CardContent.BeginAnimation(OpacityProperty, new DoubleAnimation(cardTarget, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
            }
            CapsuleLabel.Opacity = capsuleTarget;
            CardContent.Opacity = cardTarget;
        }

        // ═══════════════════════════════════════════════════════════
        // 拖拽
        // ═══════════════════════════════════════════════════════════
        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isTransitioning || _isAnyAnimationRunning) return;
            if (!_config.IslandDraggable || _isExpanded) return;
            EnsureDpiCache();
            _mouseDownPending = true;
            _isDragMoved = false;
            _dragStartScreen = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            _dragStartLeft = Left;
            _dragStartTop = Top;
            try { MainBorder.CaptureMouse(); } catch { }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_mouseDownPending) return;
            var cur = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            double dx = (cur.X - _dragStartScreen.X) / _dpiScaleX;
            double dy = (cur.Y - _dragStartScreen.Y) / _dpiScaleY;
            if (!_isDragMoved && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
            {
                _isDragMoved = true;
                _isUserMoved = true;
            }
            if (_isDragMoved)
            {
                SetCurrentValue(LeftProperty, _dragStartLeft + dx);
                SetCurrentValue(TopProperty, _dragStartTop + dy);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_mouseDownPending)
            {
                _mouseDownPending = false;
                try { MainBorder.ReleaseMouseCapture(); } catch { }
            }
        }

        private void MainBorder_TouchDown(object? sender, TouchEventArgs e)
        {
            if (_isTransitioning || _isAnyAnimationRunning) return;
            if (!_config.IslandDraggable || _isExpanded) return;
            EnsureDpiCache();
            _isTouchDragging = true;
            _touchId = e.TouchDevice.Id;
            _isDragMoved = false;
            _dragStartScreen = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            _dragStartLeft = Left;
            _dragStartTop = Top;
            try { MainBorder.CaptureTouch(e.TouchDevice); } catch { }
            e.Handled = true;
        }

        private void MainBorder_TouchMove(object? sender, TouchEventArgs e)
        {
            if (!_isTouchDragging) return;
            if (e.TouchDevice.Id != _touchId) return;
            var cur = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            double dx = (cur.X - _dragStartScreen.X) / _dpiScaleX;
            double dy = (cur.Y - _dragStartScreen.Y) / _dpiScaleY;
            if (!_isDragMoved && (Math.Abs(dx) > 6 || Math.Abs(dy) > 6))
            {
                _isDragMoved = true;
                _isUserMoved = true;
            }
            if (_isDragMoved)
            {
                SetCurrentValue(LeftProperty, _dragStartLeft + dx);
                SetCurrentValue(TopProperty, _dragStartTop + dy);
            }
            e.Handled = true;
        }

        private void MainBorder_TouchUp(object? sender, TouchEventArgs e)
        {
            if (!_isTouchDragging) return;
            if (e.TouchDevice.Id != _touchId) return;
            _isTouchDragging = false;
            _touchId = -1;
            try { MainBorder.ReleaseTouchCapture(e.TouchDevice); } catch { }
            e.Handled = true;
        }

        private void MainBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isTransitioning || _isAnyAnimationRunning) return;
            bool wasDrag = _isDragMoved;
            _isDragMoved = false;
            if (wasDrag) return;

            if (!_isExpanded)
            {
                ExpandCard(GetAutoCollapseMs(), showHint: _clickOutsideToClose);
            }
            else
            {
                if (e.OriginalSource is DependencyObject src)
                {
                    DependencyObject? cur = src;
                    while (cur != null)
                    {
                        if (cur is Button || cur is CheckBox || cur is RadioButton
                            || cur is ScrollViewer || cur is ItemsControl) return;
                        cur = VisualTreeHelper.GetParent(cur);
                    }
                }
                CollapseCard();
            }
        }

        private void MainBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_config.IslandBarEnabled) return;
            if (!_config.IslandBarManualExtendEnabled) return;
            if (_isExpanded) return;
            if (_isAnyAnimationRunning) return;

            if (_isBarExtended) CollapseBar();
            else ExtendBar();
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e) { _mouseLeaveCollapseTimer.Stop(); }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_clickOutsideToClose) return;
            if (_isRollCallActive) return;
            if (_isExpanded && !_isAnyAnimationRunning)
            {
                _mouseLeaveCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Start();
            }
        }

        private void BarBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try { App.Island?.ShowCardFromBar(); } catch { }
        }

        private void BarBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            try { if (_isBarExtended) CollapseBar(); } catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 菜单事件
        // ═══════════════════════════════════════════════════════════
        private void MenuOpenMainBtn_Click(object sender, RoutedEventArgs e) { _ = TransitionToMainAsync(); }

        private void MenuShowRightPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsBoardActive()) return;
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                cfg.FloatWindowEnabled = true;
                JsonHelper.Save(AppPaths.Config, cfg);
                _config = cfg;
                bool wasNull = App.Float == null;
                if (App.Float == null || !App.Float.IsLoaded)
                {
                    App.Float = new FloatWindow();
                    App.Float.Show();
                }
                else if (!App.Float.IsVisible) App.Float.Show();

                if (wasNull)
                    Dispatcher.BeginInvoke(new Action(() => { try { App.Float?.ShowFromRight(); } catch { } }), DispatcherPriority.Background);
                else
                    App.Float.ShowFromRight();
            }
            catch { }
        }

        private void MenuShowLeftSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (_isBarExtended) CollapseBar();
            else ExtendBar();
        }

        private void MenuExitBtn_Click(object sender, RoutedEventArgs e) { App.ExitApp(); }

        // ═══════════════════════════════════════════════════════════
        // 右侧窗口自动触发
        // ═══════════════════════════════════════════════════════════
        private void TryShowRightPanel()
        {
            try
            {
                if (IsBoardActive()) return;
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                if (!cfg.FloatWindowEnabled) return;
                if (!cfg.RightPanelAutoShowOnClassEnd) return;
                bool wasNull = App.Float == null;
                if (App.Float == null || !App.Float.IsLoaded)
                {
                    App.Float = new FloatWindow();
                    App.Float.Show();
                }
                if (wasNull)
                    Dispatcher.BeginInvoke(new Action(() => { try { App.Float?.ShowFromRight(); } catch { } }), DispatcherPriority.Background);
                else
                    App.Float.ShowFromRight();
            }
            catch { }
        }

        private void TryHideRightPanel()
        {
            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                if (!cfg.FloatWindowEnabled) return;
                if (!cfg.RightPanelAutoHideOnClassStart) return;
                if (App.Float == null) return;
                App.Float.HideToRight();
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 主窗口过渡
        // ═══════════════════════════════════════════════════════════
        private async Task TransitionToMainAsync()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            try
            {
                _autoCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Stop();
                _rollCallAutoCloseTimer.Stop();
                _barRotateTimer.Stop();

                if (_isBarExtended) CollapseBar();
                await PlayShrinkFadeAsync();
                Hide();
                ResetTransformAndOpacity();

                if (IsBoardActive()) { _isTransitioning = false; return; }

                if (Application.Current.MainWindow is MainWindow main)
                    main.ShowWithExpandAnimation();
            }
            finally { _isTransitioning = false; }
        }

        private Task PlayShrinkFadeAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var duration = TimeSpan.FromMilliseconds(220);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
            RootGrid.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = RootGrid.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            RootGrid.RenderTransform = scale;
            var sx = new DoubleAnimation(1, 0.6, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var sy = new DoubleAnimation(1, 0.6, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var op = new DoubleAnimation(1, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            op.Completed += (s, e) => tcs.TrySetResult(true);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
            BeginAnimation(OpacityProperty, op);
            return tcs.Task;
        }

        private void ResetTransformAndOpacity()
        {
            if (RootGrid.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = 1;
                st.ScaleY = 1;
            }
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        }

        // ═══════════════════════════════════════════════════════════
        // 通知
        // ═══════════════════════════════════════════════════════════
        private void ShowNotifyInCard(string title, string content, int targetNavIndex = -1)
        {
            if (IsBoardActive()) return;
            if (_isAnyAnimationRunning) return;

            _notifyTargetNavIndex = targetNavIndex;

            SetNotifyIconForNav(targetNavIndex);

            CardTabs.Visibility = Visibility.Collapsed;
            PanelInfo.Visibility = Visibility.Collapsed;
            PanelHomework.Visibility = Visibility.Collapsed;
            PanelTodo.Visibility = Visibility.Collapsed;
            PanelSchedule.Visibility = Visibility.Collapsed;
            PanelNotes.Visibility = Visibility.Collapsed;
            PanelRollCall.Visibility = Visibility.Collapsed;
            PanelNotify.Visibility = Visibility.Visible;

            CardTitle.Text = title;
            NotifyContentText.Text = content;

            if (targetNavIndex >= 0)
            {
                NotifyTapHint.Text = "单击跳转";
                NotifyTapHint.Visibility = Visibility.Visible;
            }
            else
            {
                NotifyTapHint.Text = "";
                NotifyTapHint.Visibility = Visibility.Collapsed;
            }

            ApplyAdaptiveFontSize();

            if (!_isExpanded)
            {
                ExpandNotifyCard();
            }
            else
            {
                try
                {
                    var (targetW, targetH) = CalcNotifyBarSize();
                    SetWindowBoundsForFinal(targetW, targetH);

                    SetIslandState(IslandState.Notify);
                    _isAnyAnimationRunning = true;
                    MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                    MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
                    MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);
                    double _startW = MainBorder.Width;
                    double _startH = MainBorder.Height;
                    double _startR = MainBorder.CornerRadius.TopLeft;

                    int token = NewStateToken();
                    int doneCount = 0;
                    const int totalCount = 3;
                    Action onOneDone = () =>
                    {
                        if (!IsTokenValid(token)) return;
                        doneCount++;
                        if (doneCount >= totalCount)
                        {
                            _isAnyAnimationRunning = false;
                            SyncWindowToContent();
                            CenterHorizontally();
                        }
                    };

                    var wAnim = new DoubleAnimation(_startW, targetW, TimeSpan.FromMilliseconds(ExpandDurationMs))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
                    wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = targetW; onOneDone(); };
                    MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

                    var hAnim = new DoubleAnimation(_startH, targetH, TimeSpan.FromMilliseconds(ExpandDurationMs))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
                    hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = targetH; onOneDone(); };
                    MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

                    StartRadiusAnimation(_startR, NotifyBarRadius, ExpandDurationMs, onOneDone);
                }
                catch { _isAnyAnimationRunning = false; }

                if (!_clickOutsideToClose)
                {
                    _autoCollapseTimer.Stop();
                    _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(NotifyAutoCollapseMs);
                    _autoCollapseTimer.Start();
                }
            }
        }

        private void PanelNotify_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_notifyTargetNavIndex < 0) return;
            int nav = _notifyTargetNavIndex;
            _notifyTargetNavIndex = -1;
            _ = TransitionToMainAsyncWithNav(nav);
            e.Handled = true;
        }

        private async Task TransitionToMainAsyncWithNav(int navIndex)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            try
            {
                _autoCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Stop();
                _rollCallAutoCloseTimer.Stop();
                _barRotateTimer.Stop();

                if (_isBarExtended) CollapseBar();
                await PlayShrinkFadeAsync();
                Hide();
                ResetTransformAndOpacity();

                if (IsBoardActive()) { _isTransitioning = false; return; }

                if (Application.Current.MainWindow is MainWindow main)
                {
                    main.ShowWithExpandAnimation();
                    await Task.Delay(220);
                    try { main.NavigateByIndex(navIndex); } catch { }
                }
            }
            finally { _isTransitioning = false; }
        }

        // ═══════════════════════════════════════════════════════════
        // 卡片 tab
        // ═══════════════════════════════════════════════════════════
        private void CardTab_Checked(object sender, RoutedEventArgs e)
        {
            if (_config == null) return;
            if (sender is not RadioButton rb) return;

            if (rb.Name == "CardTabTimer")
            {
                OpenTimerWindow();
                if (CardTabInfo != null && !CardTabInfo.IsChecked.GetValueOrDefault())
                    CardTabInfo.IsChecked = true;
                return;
            }

            _currentCardTab = rb.Name switch
            {
                "CardTabHomework" => "Homework",
                "CardTabTodo" => "Todo",
                "CardTabSchedule" => "Schedule",
                "CardTabNotes" => "Notes",
                _ => "Info"
            };

            string iconName = _currentCardTab switch
            {
                "Homework" => "IconHomework",
                "Todo" => "IconTodo",
                "Schedule" => "IconSchedule",
                "Notes" => "IconNotifyInfo",
                _ => "IconHome"
            };
            SetCardIconByName(iconName);

            CardTitle.Text = _currentCardTab switch
            {
                "Homework" => "今日作业",
                "Todo" => "今日待办",
                "Schedule" => "今日课表",
                "Notes" => "笔记",
                _ => "CMW 菜单"
            };

            PanelInfo.Visibility = _currentCardTab == "Info" ? Visibility.Visible : Visibility.Collapsed;
            PanelNotify.Visibility = Visibility.Collapsed;
            PanelHomework.Visibility = _currentCardTab == "Homework" ? Visibility.Visible : Visibility.Collapsed;
            PanelTodo.Visibility = _currentCardTab == "Todo" ? Visibility.Visible : Visibility.Collapsed;
            PanelSchedule.Visibility = _currentCardTab == "Schedule" ? Visibility.Visible : Visibility.Collapsed;
            PanelNotes.Visibility = _currentCardTab == "Notes" ? Visibility.Visible : Visibility.Collapsed;
            PanelRollCall.Visibility = Visibility.Collapsed;

            if (_isExpanded && !_clickOutsideToClose)
            {
                _autoCollapseTimer.Stop();
                _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(GetAutoCollapseMs());
                _autoCollapseTimer.Start();
            }

            switch (_currentCardTab)
            {
                case "Homework": RefreshCardHomework(); break;
                case "Todo": RefreshCardTodo(); break;
                case "Schedule": RefreshCardSchedule(); break;
                case "Notes": RefreshCardNotes(); break;
            }
        }

        private void OpenTimerWindow()
        {
            try
            {
                if (_timerWindow != null && _timerWindow.IsLoaded)
                {
                    if (_timerWindow.WindowState == WindowState.Minimized) _timerWindow.WindowState = WindowState.Normal;
                    _timerWindow.Activate();
                    return;
                }
                _timerWindow = new ClassTimerWindow();
                _timerWindow.Closed += (s, e) => _timerWindow = null;
                _timerWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开课堂倒计时失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshCardHomework()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                var todayList = list.Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder).ThenBy(h => h.EndDate).ToList();
                var vms = todayList.Select(h => new CardHomeworkVm
                {
                    Id = h.Id, IsCompleted = h.IsCompleted,
                    DisplayText = BuildHomeworkDisplayText(h),
                    TextBrush = GetHomeworkBrush(h.Priority)
                }).ToList();
                icCardHomework.ItemsSource = vms;
                tbCardHomeworkEmpty.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private static string BuildHomeworkDisplayText(HomeworkItem h)
        {
            string line = h.HomeworkName;
            if (!string.IsNullOrWhiteSpace(h.Subject)) line += $"（{h.Subject}）";
            line += $" 截止 {h.DateRangeText}";
            return line;
        }

        private static Brush GetHomeworkBrush(string priority)
        {
            return priority switch
            {
                "VeryUrgent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6666")),
                "Urgent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB366")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE"))
            };
        }

        private void CardHomework_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not CardHomeworkVm vm) return;
            ToggleHomeworkCompleted(vm.Id, true);
        }

        private void CardHomework_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not CardHomeworkVm vm) return;
            ToggleHomeworkCompleted(vm.Id, false);
        }

        private void ToggleHomeworkCompleted(string id, bool completed)
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var target = list.FirstOrDefault(x => x.Id == id);
                if (target == null) return;
                if (target.IsCompleted == completed) return;
                target.IsCompleted = completed;
                JsonHelper.Save(AppPaths.Homework, list);
                RefreshCardHomework();
                UpdateCapsuleTextAndWidth();
                App.NotifySettingsChanged();
            }
            catch { }
        }

        private void RefreshCardTodo()
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo) ?? new List<TodoItem>();
                var today = DateTime.Today;
                var todayList = list.Where(t => t.Deadline.HasValue && t.Deadline.Value.Date == today && !t.IsCompleted).ToList();
                var vms = todayList.Select(t => new CardTodoVm
                {
                    Id = t.Id, IsCompleted = t.IsCompleted,
                    DisplayText = string.IsNullOrWhiteSpace(t.Content) ? "（无内容）" : t.Content
                }).ToList();
                icCardTodo.ItemsSource = vms;
                tbCardTodoEmpty.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void CardTodo_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not CardTodoVm vm) return;
            ToggleTodoCompleted(vm.Id, true);
        }

        private void CardTodo_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not CardTodoVm vm) return;
            ToggleTodoCompleted(vm.Id, false);
        }

        private void ToggleTodoCompleted(string id, bool completed)
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo) ?? new List<TodoItem>();
                var target = list.FirstOrDefault(x => x.Id == id);
                if (target == null) return;
                if (target.IsCompleted == completed) return;
                target.IsCompleted = completed;
                JsonHelper.Save(AppPaths.Todo, list);
                RefreshCardTodo();
                UpdateCapsuleTextAndWidth();
                App.NotifySettingsChanged();
            }
            catch { }
        }

        private void RefreshCardSchedule()
        {
            try
            {
                var textList = SchedulePage.GetTodayScheduleText();
                icCardSchedule.ItemsSource = textList;
                tbCardScheduleEmpty.Visibility = textList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void RefreshCardNotes()
        {
            try
            {
                var list = NoteService.GetAll();
                var vms = list.Select(n => new CardNoteVm
                {
                    Id = n.Id, DisplayText = n.DisplayText, CreatedText = n.CreatedText, Note = n
                }).ToList();
                icCardNotes.ItemsSource = vms;
                tbCardNotesEmpty.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                icCardNotes.ItemsSource = new List<CardNoteVm>();
                tbCardNotesEmpty.Visibility = Visibility.Visible;
            }
        }

        private async void BtnAddNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                await Task.Delay(300);
                System.Drawing.Bitmap? fullScreen = ScreenshotService.CaptureScreen();
                if (fullScreen == null)
                {
                    this.Show();
                    MessageBox.Show("屏幕截图失败，请重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                System.Drawing.Bitmap? cropped = null;
                var overlay = new ScreenshotOverlayWindow(fullScreen);
                if (overlay.ShowDialog() == true) cropped = overlay.CroppedBitmap;
                try { fullScreen.Dispose(); } catch { }
                this.Show();
                var addWin = new NoteAddWindow(cropped);
                if (Application.Current.MainWindow is Window owner)
                {
                    try { addWin.Owner = owner; } catch { }
                }
                if (addWin.ShowDialog() == true)
                {
                    RefreshCardNotes();
                    CardTabNotes.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                try { this.Show(); } catch { }
                MessageBox.Show("新增笔记失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not CardNoteVm vm) return;
            if (MessageBox.Show("确定删除这条笔记？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (NoteService.Delete(vm.Id)) RefreshCardNotes();
        }

        private void ApplyAdaptiveFontSize()
        {
            string content = NotifyContentText.Text ?? "";
            int len = content.Length;
            var (w, h) = CalcNotifyBarSize();

            double maxByHeight = Math.Max(14, h * 0.26);
            double usableWidth = Math.Max(200, w - 100);
            double charsPerLine = Math.Max(4.0, len / 2.0);
            double maxByWidth = usableWidth / charsPerLine;

            double maxAllowed = Math.Min(maxByHeight, maxByWidth);
            if (maxAllowed > 20) maxAllowed = 20;
            if (maxAllowed < 12) maxAllowed = 12;

            double baseSize;
            if (len <= 20) baseSize = 20;
            else if (len <= 40) baseSize = 18;
            else if (len <= 60) baseSize = 17;
            else if (len <= 100) baseSize = 15;
            else if (len <= 160) baseSize = 14;
            else if (len <= 240) baseSize = 13;
            else baseSize = 12;

            if (baseSize > maxAllowed) baseSize = maxAllowed;

            double scale = _config.IslandFontScale;
            if (scale < 0.8) scale = 0.8;
            if (scale > 1.5) scale = 1.5;

            NotifyContentText.FontSize = baseSize * scale;

            double titleMax = Math.Max(14, Math.Min(17, h * 0.14));
            double titleSize = 16 * scale;
            if (titleSize > titleMax) titleSize = titleMax;
            if (titleSize < 12) titleSize = 12;
            CardTitle.FontSize = titleSize;

            try { CardContent.Margin = new Thickness(16, 10, 16, 10); } catch { }

            foreach (var child in CardTabs.Children)
            {
                if (child is RadioButton rb) rb.FontSize = 11 * scale;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // A9：上下课表现方式
        // ═══════════════════════════════════════════════════════════
        private async void RunClassChangeVisualAsync(bool isStart, TimeSlot slot, bool doPopup, bool doMini)
        {
            try
            {
                string changeStyle = _config.ClassChangeStyle ?? StyleColorThenNotify;

                if (changeStyle == StyleNone) return;

                bool doColor = (changeStyle == StyleColorThenNotify || changeStyle == StyleColorAndBar);
                bool doBar = (changeStyle == StyleColorAndBar);
                bool doNotify = (changeStyle == StyleColorThenNotify || changeStyle == StyleNotifyOnly);

                if (doColor)
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(() => FlashBlue(_config.IslandBlueFlashSeconds));
                        await Task.Delay(_config.IslandBlueFlashSeconds * 1000);
                    }
                    catch { }
                }

                if (doBar)
                {
                    if (_config.IslandBarEnabled && _config.IslandBarAutoExtendOnClassChange)
                    {
                        try
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (isStart) CollapseBar();
                                else ExtendBar();
                            });
                        }
                        catch { }
                    }
                    return;
                }

                if (doNotify)
                {
                    if (isStart && !_config.ClassStartRemindEnabled) return;
                    if (!isStart && !_config.ClassEndRemindEnabled) return;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (isStart)
                            {
                                if (doPopup) ShowNotifyInCard($"🔔 {slot.Name}", $"{slot.Name} 开始", 1);
                                else if (doMini) ShowMiniRemindInCapsule($"🔔 {slot.Name}", _config.MiniRemindDurationSeconds);
                            }
                            else
                            {
                                string nextClass = GetNextClassNameForBell();
                                string content = string.IsNullOrEmpty(nextClass)
                                    ? $"{slot.Name} 结束 · 今日课程结束"
                                    : $"{slot.Name} 结束 · 下节 {nextClass}";
                                if (doPopup) ShowNotifyInCard($"🔕 {slot.Name}", content, 1);
                                else if (doMini) ShowMiniRemindInCapsule($"🔕 {slot.Name}", _config.MiniRemindDurationSeconds);
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // 定时器
        // ═══════════════════════════════════════════════════════════
        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (_lastAlarmCheckTime.Minute == now.Minute
                && _lastAlarmCheckTime.Hour == now.Hour
                && _lastAlarmCheckTime.Date == now.Date) return;
            _lastAlarmCheckTime = now;

            if (_isBarExtended && !_isAnyAnimationRunning)
            {
                try
                {
                    if (_barGroupIndex >= 0 && _barGroupIndex < _barActiveGroups.Count
                        && _barActiveGroups[_barGroupIndex] == 2)
                    {
                        RefreshBarStatus();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { RefreshBarWidthAndMarquee(); } catch { }
                        }), DispatcherPriority.Background);
                    }
                }
                catch { }
            }

            CheckBoardAutoSchedule(now);

            if (now.Hour == 8 && now.Minute == 0)
                ShowNotifyInCard("今日汇总", GetTodayInfoText(), 0);

            if (TryParseTime(_config.TodoRemindTime1, out var t1) && now.Hour == t1.Hours && now.Minute == t1.Minutes)
                ShowNotifyInCard("待办提醒", GetTodayTodoText(), 2);

            if (TryParseTime(_config.TodoRemindTime2, out var t2) && now.Hour == t2.Hours && now.Minute == t2.Minutes)
                ShowNotifyInCard("待办提醒", GetTodayTodoText(), 2);

            if (_config.TodoCustomRemindEnabled)
            {
                var todoTimes = _config.TodoRemindTimeList;
                if (todoTimes == null || todoTimes.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(_config.TodoCustomRemindTime))
                        todoTimes = new List<string> { _config.TodoCustomRemindTime };
                    else todoTimes = new List<string>();
                }
                foreach (var timeStr in todoTimes)
                {
                    if (TryParseTime(timeStr, out var tcr) && now.Hour == tcr.Hours && now.Minute == tcr.Minutes)
                    {
                        string key = $"{now:yyyy-MM-dd}_todoCustom_{timeStr}";
                        if (_lastTodoCustomRemindFired == key) continue;
                        _lastTodoCustomRemindFired = key;
                        ShowNotifyInCard("待办提醒", GetTodayTodoText(), 2);
                    }
                }
            }

            if (_config.DutyRemindEnabled)
            {
                var dutyTimes = _config.DutyRemindTimeList;
                if (dutyTimes == null || dutyTimes.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(_config.DutyRemindTime))
                        dutyTimes = new List<string> { _config.DutyRemindTime };
                    else dutyTimes = new List<string>();
                }
                foreach (var timeStr in dutyTimes)
                {
                    if (TryParseTime(timeStr, out var dr) && now.Hour == dr.Hours && now.Minute == dr.Minutes)
                    {
                        string key = $"{now:yyyy-MM-dd}_duty_{timeStr}";
                        if (_lastDutyRemindFired == key) continue;
                        _lastDutyRemindFired = key;
                        string dutyName = GetDutyText();
                        string content = string.IsNullOrWhiteSpace(dutyName) ? "今日未安排值日" : dutyName;
                        ShowNotifyInCard("值日提醒", content, 3);
                    }
                }
            }

            if (_config.HomeworkRemindEnabled)
            {
                foreach (var timeStr in _config.HomeworkRemindTimes)
                {
                    if (TryParseTime(timeStr, out var remindTime) && now.Hour == remindTime.Hours && now.Minute == remindTime.Minutes)
                    {
                        string key = $"{now:yyyy-MM-dd}_{timeStr}";
                        if (_lastHomeworkRemindFired == key) continue;
                        _lastHomeworkRemindFired = key;
                        ShowRollCallPanel(timeStr);
                    }
                }
            }

            bool silent = IsInSilentTime(now);
            var slots = _config.TimeSlots;
            if (slots == null || slots.Count == 0) slots = DefaultTimeSlots.Create();

            string style = _config.ClassRemindStyle ?? "Popup";
            bool doPopup = (style == "Popup" || style == "Both");
            bool doMini = (style == "Mini" || style == "Both");

            foreach (var slot in slots)
            {
                if (!slot.EnableNotify) continue;

                bool isStart = TryParseTime(slot.StartTime, out var s) && now.Hour == s.Hours && now.Minute == s.Minutes;
                bool isEnd = TryParseTime(slot.EndTime, out var en) && now.Hour == en.Hours && now.Minute == en.Minutes;

                if (isStart)
                {
                    if (slot.IsLongBreak) TryShowRightPanel();
                    else TryHideRightPanel();
                }
                else if (isEnd && !slot.IsLongBreak)
                {
                    TryShowRightPanel();
                }

                if (silent) continue;
                if (style == "None") continue;

                if (isStart || (isEnd && !slot.IsLongBreak))
                {
                    RunClassChangeVisualAsync(isStart, slot, doPopup, doMini);
                }
            }
        }

        private void CheckBoardAutoSchedule(DateTime now)
        {
            try
            {
                if (!_config.BoardAutoScheduleEnabled) return;
                bool isStartTime = TryParseTime(_config.BoardAutoStartTime, out var st) && now.Hour == st.Hours && now.Minute == st.Minutes;
                bool isEndTime = TryParseTime(_config.BoardAutoEndTime, out var et) && now.Hour == et.Hours && now.Minute == et.Minutes;
                if (isStartTime)
                {
                    if (App.Board == null || !App.Board.IsLoaded)
                    {
                        var board = new ClassBoardWindow();
                        App.Board = board;
                        board.Closed += (s, ev) => { App.Board = null; };
                        board.Show();
                    }
                }
                else if (isEndTime)
                {
                    if (App.Board != null && App.Board.IsLoaded) App.Board.Close();
                }
            }
            catch { }
        }

        private string GetNextClassNameForBell()
        {
            try
            {
                DateTime now = DateTime.Now;
                int weekDay = (int)now.DayOfWeek;
                if (weekDay < 1 || weekDay > 6) return "";
                var slots = GetTimeSlots();
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!TryParseTime(slots[i].StartTime, out var s)) continue;
                    var startTime = now.Date.Add(s);
                    if (startTime <= now) continue;
                    string lesson = GetLessonNameByPeriod(i, weekDay);
                    if (!string.IsNullOrWhiteSpace(lesson)) return lesson;
                }
            }
            catch { }
            return "";
        }

        private bool IsInSilentTime(DateTime now)
        {
            string startStr = _config.SilentStart ?? "";
            string endStr = _config.SilentEnd ?? "";
            if (string.IsNullOrWhiteSpace(startStr) || string.IsNullOrWhiteSpace(endStr)) return false;
            if (!TryParseTime(startStr, out var start)) return false;
            if (!TryParseTime(endStr, out var end)) return false;
            var nowTime = now.TimeOfDay;
            if (start <= end) return nowTime >= start && nowTime < end;
            else return nowTime >= start || nowTime < end;
        }

        private List<string> GetUnfinishedHomeworkNames()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                return list.Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder).Select(h => h.HomeworkName).ToList();
            }
            catch { return new List<string>(); }
        }

        private void SaveHomeworkReminder(string timeStr, List<string> unfinished)
        {
            try
            {
                var records = JsonHelper.Load<List<HomeworkReminderRecord>>(AppPaths.HomeworkReminders) ?? new List<HomeworkReminderRecord>();
                records.Add(new HomeworkReminderRecord { RemindTimeStr = timeStr, RemindAt = DateTime.Now, UnfinishedHomework = unfinished });
                if (records.Count > 50) records = records.Skip(records.Count - 50).ToList();
                JsonHelper.Save(AppPaths.HomeworkReminders, records);
            }
            catch { }
        }

        private static bool TryParseTime(string? text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return TimeSpan.TryParse(text, out result);
        }

        private string GetTodayInfoText()
        {
            var main = Application.Current.MainWindow as MainWindow;
            if (main == null) return "加载失败";
            var data = main.GetTodayDisplayData();
            return $"课表：{data.Schedule}\n值日：{data.Duty}\n作业：{data.Homework}\n待办：{data.Todo}";
        }

        private string GetTodayTodoText()
        {
            var main = Application.Current.MainWindow as MainWindow;
            if (main == null) return "加载失败";
            var data = main.GetTodayDisplayData();
            return $"今日待办：{data.Todo}";
        }

        // ═══════════════════════════════════════════════════════════
        // 作业催收
        // ═══════════════════════════════════════════════════════════
        private void ShowRollCallPanel(string timeStr)
        {
            try
            {
                if (IsBoardActive()) return;
                if (_isAnyAnimationRunning) return;

                var studentData = JsonHelper.Load<StudentListData>(AppPaths.Students) ?? new StudentListData();
                var allStudents = (studentData.Students ?? new List<Student>()).OrderBy(s => s.Order).ToList();

                if (allStudents.Count == 0)
                {
                    var unfinished = GetUnfinishedHomeworkNames();
                    if (unfinished.Count > 0)
                    {
                        ShowNotifyInCard("作业催收", $"有 {unfinished.Count} 项作业未完成，请到作业页点名", 4);
                        SaveHomeworkReminder(timeStr, unfinished);
                    }
                    return;
                }

                var allHw = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                var now = DateTime.Now;

                var todayHw = allHw.Where(h => h.IsActiveOn(today) && !h.IsCompleted && h.IsRollCalled && h.NeedSubmit && now <= h.EndDate)
                    .OrderBy(h => h.PriorityOrder).ThenBy(h => h.HomeworkName).ToList();

                if (todayHw.Count == 0) return;

                var items = new List<(HomeworkItem Hw, List<Student> Unsubmitted)>();
                foreach (var hw in todayHw)
                {
                    var submittedIds = new HashSet<string>(hw.SubmittedStudentIds ?? new List<string>());
                    var unsubmitted = allStudents.Where(s => !submittedIds.Contains(s.Id)).OrderBy(s => s.Order).ToList();
                    items.Add((hw, unsubmitted));
                }

                var unfinishedNames = items.Where(x => x.Unsubmitted.Count > 0)
                    .Select(x => $"{x.Hw.HomeworkName}（未交 {x.Unsubmitted.Count} 人）").ToList();

                if (unfinishedNames.Count > 0) SaveHomeworkReminder(timeStr, unfinishedNames);

                RollCallItemsPanel.Children.Clear();
                foreach (var item in items)
                    RollCallItemsPanel.Children.Add(BuildRollCallItem(item.Hw, item.Unsubmitted));

                SetIslandState(IslandState.RollCall);
                _isRollCallActive = true;
                CardTabs.Visibility = Visibility.Collapsed;
                PanelInfo.Visibility = Visibility.Collapsed;
                PanelNotify.Visibility = Visibility.Collapsed;
                PanelHomework.Visibility = Visibility.Collapsed;
                PanelTodo.Visibility = Visibility.Collapsed;
                PanelSchedule.Visibility = Visibility.Collapsed;
                PanelNotes.Visibility = Visibility.Collapsed;
                PanelRollCall.Visibility = Visibility.Visible;
                SetCardIconByName("IconNotifyHomework");
                CardTitle.Text = $"作业催收（{timeStr}）";

                if (!_isExpanded)
                {
                    _isExpanded = true;
                    _isAnyAnimationRunning = true;
                    _isBoundsAnimating = true;

                    _barWasExtendedBeforeCard = _isBarExtended;
                    if (_isBarExtended)
                    {
                        ClearAllBarMarquees();
                        BarBorder.Visibility = Visibility.Collapsed;
                        BarBorder.Width = 0;
                        BarBorder.Opacity = 0;
                        _isBarExtended = false;
                        _barRotateTimer.Stop();
                    }

                    SetContentState(capsule: false, animated: true, durationMs: 300);
                    CardContent.IsHitTestVisible = true;
                    CardHintText.Visibility = Visibility.Collapsed;

                    MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                    MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
                    MainBorder.BeginAnimation(Border.CornerRadiusProperty, null);
                    double _startW = MainBorder.Width;
                    double _startH = MainBorder.Height;
                    double _startR = MainBorder.CornerRadius.TopLeft;

                    SetWindowBoundsForFinal(_cardWidth, _cardHeight);

                    int token = NewStateToken();
                    int doneCount = 0;
                    const int totalCount = 3;
                    Action onOneDone = () =>
                    {
                        if (!IsTokenValid(token)) return;
                        doneCount++;
                        if (doneCount >= totalCount)
                        {
                            _isBoundsAnimating = false;
                            _isAnyAnimationRunning = false;
                            SyncWindowToContent();
                            CenterHorizontally();
                        }
                    };

                    var wAnim = new DoubleAnimation(_startW, _cardWidth, TimeSpan.FromMilliseconds(ExpandDurationMs))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
                    wAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null); MainBorder.Width = _cardWidth; onOneDone(); };
                    MainBorder.BeginAnimation(FrameworkElement.WidthProperty, wAnim);

                    var hAnim = new DoubleAnimation(_startH, _cardHeight, TimeSpan.FromMilliseconds(ExpandDurationMs))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
                    hAnim.Completed += (s, e) => { MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null); MainBorder.Height = _cardHeight; onOneDone(); };
                    MainBorder.BeginAnimation(FrameworkElement.HeightProperty, hAnim);

                    StartRadiusAnimation(_startR, CardRadius, ExpandDurationMs, onOneDone);
                }

                int dur = _config.HomeworkRemindDurationSeconds;
                if (dur < 30) dur = 30;
                if (dur > 300) dur = 300;

                _autoCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Stop();
                _rollCallAutoCloseTimer.Stop();
                _rollCallAutoCloseTimer.Interval = TimeSpan.FromSeconds(dur);
                _rollCallAutoCloseTimer.Start();
            }
            catch { }
        }

        private Border BuildRollCallItem(HomeworkItem hw, List<Student> unsubmitted)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22FFFFFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };

            nameRow.Children.Add(new TextBlock
            {
                Text = hw.HomeworkName,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                FontFamily = (FontFamily)FindResource("GlobalFontFamily"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (unsubmitted.Count == 0)
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = "  ✅ 已交齐",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BF79C")),
                    FontSize = 13, FontFamily = (FontFamily)FindResource("GlobalFontFamily"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            else
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = $"  未交 {unsubmitted.Count} 人",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9C9C")),
                    FontSize = 13, FontWeight = FontWeights.SemiBold,
                    FontFamily = (FontFamily)FindResource("GlobalFontFamily"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            stack.Children.Add(nameRow);

            if (unsubmitted.Count > 0)
            {
                string names = string.Join("  ", unsubmitted.Select(s => s.Name));
                var marqueeGrid = new Grid { Height = 22, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = true };
                var tb = new TextBlock
                {
                    Text = names + "          " + names,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDEEEEEE")),
                    FontSize = 13, FontFamily = (FontFamily)FindResource("GlobalFontFamily"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    TextWrapping = TextWrapping.NoWrap
                };
                var transform = new TranslateTransform();
                tb.RenderTransform = transform;
                marqueeGrid.Children.Add(tb);
                stack.Children.Add(marqueeGrid);

                marqueeGrid.SizeChanged += (s, e) =>
                {
                    if (e.NewSize.Width > 0 && !_marqueeStarted.Contains(marqueeGrid))
                    {
                        _marqueeStarted.Add(marqueeGrid);
                        StartMarquee(tb, transform, e.NewSize.Width);
                    }
                };
            }

            border.Child = stack;
            return border;
        }

        private void StartMarquee(TextBlock tb, TranslateTransform transform, double containerWidth)
        {
            try
            {
                if (containerWidth <= 0) return;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double totalWidth = tb.DesiredSize.Width;
                if (totalWidth <= containerWidth) return;

                double cycle = totalWidth / 2;
                int speed = _config.HomeworkRemindScrollSpeed;
                if (speed < 20) speed = 20;
                if (speed > 150) speed = 150;
                double durationSec = cycle / speed;
                if (durationSec < 1) durationSec = 1;

                var anim = new DoubleAnimation
                {
                    From = 0, To = -cycle,
                    Duration = TimeSpan.FromSeconds(durationSec),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                transform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            catch { }
        }

        private void StopRollCallAndRestoreTabs()
        {
            _isRollCallActive = false;
            _rollCallAutoCloseTimer.Stop();
            _marqueeStarted.Clear();
            RollCallItemsPanel.Children.Clear();

            CardTabs.Visibility = Visibility.Visible;
            PanelRollCall.Visibility = Visibility.Collapsed;
            CardTabInfo.IsChecked = true;
            _currentCardTab = "Info";
            PanelInfo.Visibility = Visibility.Visible;
            PanelNotify.Visibility = Visibility.Collapsed;
            PanelHomework.Visibility = Visibility.Collapsed;
            PanelTodo.Visibility = Visibility.Collapsed;
            PanelSchedule.Visibility = Visibility.Collapsed;
            PanelNotes.Visibility = Visibility.Collapsed;

            SetCardIconByName("IconHome");
            CardTitle.Text = "CMW 菜单";
        }

        private void EndRollCallAndCollapse()
        {
            if (!_isExpanded) return;
            CollapseCard();
        }
    }

    internal class CardHomeworkVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsCompleted { get; set; }
        public Brush TextBrush { get; set; } = Brushes.White;
    }

    internal class CardTodoVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsCompleted { get; set; }
    }

    internal class CardNoteVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public string CreatedText { get; set; } = "";
        public NoteItem Note { get; set; } = new NoteItem();
    }

    internal class BarScheduleVm
    {
        public string ShortName { get; set; } = "";
        public Brush Bg { get; set; } = Brushes.Transparent;
        public Brush Fg { get; set; } = Brushes.White;
        public double FontSize { get; set; } = 12;
    }

    internal class BarItemVm
    {
        public string Text { get; set; } = "";
        public Brush Fg { get; set; } = Brushes.White;
        public double FontSize { get; set; } = 13;
    }
}