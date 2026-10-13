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
        private const double CapsuleHeight = 18;
        private const double CapsuleRadius = 9;
        private const double CapsuleMinWidth = 60;
        private const double CapsuleMaxWidth = 160;
        private const double CapsuleTextPadding = 28;
        private const double CardRadius = 18;

        private double _cardWidth = 480;
        private double _cardHeight = 320;

        private const int ClickAutoCollapseMs = 8000;
        private const int NotifyAutoCollapseMs = 3000;
        private const int MouseLeaveDelayMs = 500;
        private const int ExpandDurationMs = 420;
        private const int CollapseDurationMs = 360;

        private bool _isExpanded;
        private bool _isBoundsAnimating;

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
        private bool _isRollCallActive;

        private AppConfig _config;
        private DateTime _lastAlarmCheckTime = DateTime.MinValue;
        private bool _isTransitioning;
        private bool _clickOutsideToClose = false;

        private readonly DispatcherTimer _radiusTimer;
        private DateTime _radiusStartTime;
        private double _radiusFrom;
        private double _radiusTo;
        private double _radiusDurationMs;

        private string _lastHomeworkRemindFired = "";
        private string _lastDutyRemindFired = "";
        private string _lastTodoCustomRemindFired = "";

        private int _capsuleRotationIndex = -1;

        private string _currentCardTab = "Info";

        private bool _isNotifyMode = false;

        // ★ 28.0.0：通知条尺寸改为运行时计算（不再固定常量）
        private const double NotifyBarRadius = 24;
        private const double NotifyBarMinWidth = 400;   // 最小宽度
        private const double NotifyBarMinHeight = 80;   // 最小高度

        private int _notifyTargetNavIndex = -1;

        private string? _miniRemindOverrideText = null;

        private readonly HashSet<Grid> _marqueeStarted = new HashSet<Grid>();

        private ClassTimerWindow? _timerWindow;

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

            _radiusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _radiusTimer.Tick += RadiusTimer_Tick;

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

            _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            _clickOutsideToClose = _config.NotifyClickOutsideToClose;
            ApplyCardSizeFromConfig();

            App.SettingsChanged += OnSettingsChanged;
            App.RemindConfigChanged += OnSettingsChanged;
            Deactivated += IslandWindow_Deactivated;
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
            catch
            {
                _dpiScaleX = 1.0;
                _dpiScaleY = 1.0;
            }
        }

        private void ApplyCardSizeFromConfig()
        {
            switch (_config.IslandCardSize)
            {
                case "Small": _cardWidth = 400; _cardHeight = 260; break;
                case "Large": _cardWidth = 560; _cardHeight = 360; break;
                case "XLarge": _cardWidth = 640; _cardHeight = 420; break;
                case "Medium":
                default: _cardWidth = 480; _cardHeight = 320; break;
            }
        }

        /// <summary>
        /// ★ 28.0.0：根据配置计算通知条尺寸
        /// </summary>
        private (double Width, double Height) CalcNotifyBarSize()
        {
            var wa = SystemParameters.WorkArea;

            double wp = _config.NotifyBarWidthPercent;
            if (wp < 50) wp = 96;
            if (wp > 100) wp = 100;

            double hp = _config.NotifyBarHeightPercent;
            if (hp < 5) hp = 10;
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

                UpdateCapsuleTextAndWidth();

                if (!_isExpanded)
                {
                    UpdateWindowBounds();
                }
                else
                {
                    ApplyAdaptiveFontSize();
                }
            }), DispatcherPriority.Background);
        }

        private void IslandWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_clickOutsideToClose && _isExpanded && !_isBoundsAnimating && !_isRollCallActive)
            {
                CollapseCard();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MinWidth = 0;
            MinHeight = 0;
            EnsureDpiCache();
            UpdateWindowBounds();
            ApplyCapsuleState();
        }

        /// <summary>
        /// ★ 28.0.0：通知模式下用配置计算尺寸，居中在屏幕顶部
        /// </summary>
        private void UpdateWindowBounds()
        {
            var wa = SystemParameters.WorkArea;

            if (_isNotifyMode)
            {
                var (nw, nh) = CalcNotifyBarSize();
                SetCurrentValue(WidthProperty, nw);
                SetCurrentValue(HeightProperty, nh);
                SetCurrentValue(LeftProperty, wa.Left + (wa.Width - nw) / 2);
                SetCurrentValue(TopProperty, wa.Top + 4);
                return;
            }

            SetCurrentValue(WidthProperty, _cardWidth);
            SetCurrentValue(HeightProperty, _cardHeight);

            if (_isUserMoved) return;

            SetCurrentValue(LeftProperty, wa.Left + (wa.Width - _cardWidth) / 2);
            SetCurrentValue(TopProperty, wa.Top + 4);
        }

        // ═══════ 胶囊文字与宽度 ═══════

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
                try { fontFamily = (FontFamily)FindResource("AppleFont"); }
                catch { fontFamily = new FontFamily("Microsoft YaHei UI"); }

                var typeface = new Typeface(fontFamily, FontStyles.Normal,
                    FontWeights.SemiBold, FontStretches.Normal);

                var ft = new FormattedText(text ?? "",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 9, Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                return ft.Width;
            }
            catch { return 30; }
        }

        private void UpdateCapsuleTextAndWidth()
        {
            if (_isExpanded || _isBoundsAnimating) return;

            if (_miniRemindOverrideText != null)
            {
                CapsuleLabel.Text = _miniRemindOverrideText;
                double tw = GetCapsuleWidth();
                MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                MainBorder.Width = tw;
                return;
            }

            string newText = GetCapsuleText();
            if (CapsuleLabel.Text == newText) return;

            CapsuleLabel.Text = newText;

            double targetWidth = GetCapsuleWidth();
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.Width = targetWidth;
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
                case "CMW":
                default: return "CMW";
            }
        }

        private List<TimeSlot> GetTimeSlots()
        {
            var slots = _config.TimeSlots;
            if (slots == null || slots.Count == 0)
                slots = DefaultTimeSlots.Create();
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
                    {
                        int min = (int)Math.Ceiling((end - now).TotalMinutes);
                        return $"距下课 {min}分";
                    }
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
                        if (remain.TotalMinutes < 60)
                            return $"距上课 {(int)Math.Ceiling(remain.TotalMinutes)}分";
                        else
                            return $"距上课 {(int)Math.Ceiling(remain.TotalHours)}时";
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
                if (!string.IsNullOrWhiteSpace(lesson))
                    return $"下节 {lesson}";
            }

            return "今日结束";
        }

        private string GetHomeworkCountText()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();
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
                    if (TryParseTime(slots[i].StartTime, out var s)
                        && TryParseTime(slots[i].EndTime, out var en))
                    {
                        var start = now.Date.Add(s);
                        var end = now.Date.Add(en);
                        if (now >= start && now < end) { currentSlotIdx = i; break; }
                    }
                }

                if (currentSlotIdx >= 0)
                {
                    if (TryParseTime(slots[currentSlotIdx].EndTime, out var en))
                    {
                        var end = now.Date.Add(en);
                        int min = (int)Math.Ceiling((end - now).TotalMinutes);
                        candidates.Add($"距下课 {min}分");
                    }

                    string lesson = GetLessonNameByPeriod(currentSlotIdx, weekDay);
                    if (!string.IsNullOrWhiteSpace(lesson))
                        candidates.Add($"本节课 {lesson}");
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
                        if (!string.IsNullOrWhiteSpace(lastLesson))
                            candidates.Add($"上节 {lastLesson}");
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
                        if (!string.IsNullOrWhiteSpace(nextLesson))
                            candidates.Add($"下节 {nextLesson}");
                    }
                }
            }

            string todoText = GetTodoCountText();
            if (!string.IsNullOrWhiteSpace(todoText) && todoText != "无待办" && todoText != "CMW")
                candidates.Add(todoText);

            string hwText = GetHomeworkCountText();
            if (!string.IsNullOrWhiteSpace(hwText) && hwText != "无作业" && hwText != "CMW")
                candidates.Add(hwText);

            string dutyText = GetDutyText();
            if (!string.IsNullOrWhiteSpace(dutyText))
                candidates.Add(dutyText);

            if (candidates.Count == 0) return "CMW";
            if (candidates.Count == 1) return candidates[0];

            _capsuleRotationIndex++;
            if (_capsuleRotationIndex < 0 || _capsuleRotationIndex >= candidates.Count)
                _capsuleRotationIndex = 0;

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

                var ov = schedule.Overrides?.FirstOrDefault(x =>
                    x.Date == today && x.Period == period && x.WeekDay == weekDay);
                if (ov != null && !string.IsNullOrWhiteSpace(ov.LessonName))
                    return ov.LessonName;

                var item = schedule.WeekBase?.FirstOrDefault(x =>
                    x.WeekDay == effectiveWeekDay && x.Period == period);
                return item?.LessonName ?? "";
            }
            catch { return ""; }
        }

        private string GetTodoCountText()
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();
                var today = DateTime.Today;
                int count = list.Count(t => t.Deadline.HasValue
                    && t.Deadline.Value.Date == today
                    && !t.IsCompleted);
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
                if (duty == null || string.IsNullOrWhiteSpace(duty.TodayDutyGroupName))
                    return "";
                return $"值日 {duty.TodayDutyGroupName}";
            }
            catch { return ""; }
        }

        // ═══════ 胶囊状态 ═══════

        private void ApplyCapsuleState()
        {
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);

            double w = GetCapsuleWidth();
            MainBorder.Width = w;
            MainBorder.Height = CapsuleHeight;
            MainBorder.CornerRadius = new CornerRadius(CapsuleRadius);
        }

        public void ShowWithExpandAnimation()
        {
            if (IsBoardActive())
            {
                Hide();
                return;
            }

            _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            _clickOutsideToClose = _config.NotifyClickOutsideToClose;
            ApplyCardSizeFromConfig();

            _isNotifyMode = false;
            _isUserMoved = false;
            UpdateWindowBounds();

            CapsuleLabel.Text = GetCapsuleText();
            ApplyCapsuleState();

            _isExpanded = false;
            _isRollCallActive = false;
            CapsuleLabel.BeginAnimation(OpacityProperty, null);
            CardContent.BeginAnimation(OpacityProperty, null);
            CapsuleLabel.Opacity = 1;
            CardContent.Opacity = 0;
            CardContent.IsHitTestVisible = false;
            CardHintText.Visibility = Visibility.Collapsed;

            Show();

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

        private void ExpandCard(int autoCollapseMs, bool showHint)
        {
            if (_isExpanded || _isBoundsAnimating) return;

            _isExpanded = true;

            ResetPanelsToMenu();

            SetContentState(capsule: false, animated: true, durationMs: 300);
            CardContent.IsHitTestVisible = true;
            CardHintText.Text = showHint ? "单击弹窗外区域收回" : "";
            CardHintText.Visibility = showHint ? Visibility.Visible : Visibility.Collapsed;

            double startWidth = GetCapsuleWidth();

            AnimateInnerBorder(startWidth, CapsuleHeight, CapsuleRadius,
                _cardWidth, _cardHeight, CardRadius,
                ExpandDurationMs, null);

            _autoCollapseTimer.Stop();
            if (!_clickOutsideToClose && autoCollapseMs > 0)
            {
                _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(autoCollapseMs);
                _autoCollapseTimer.Start();
            }
        }

        private void ExpandNotifyCard()
        {
            if (_isExpanded || _isBoundsAnimating) return;

            _isExpanded = true;

            UpdateWindowBounds();

            SetContentState(capsule: false, animated: true, durationMs: 320);
            CardContent.IsHitTestVisible = true;
            CardHintText.Visibility = Visibility.Collapsed;

            double startWidth = GetCapsuleWidth();
            var (targetW, targetH) = CalcNotifyBarSize();

            AnimateInnerBorder(
                startWidth, CapsuleHeight, CapsuleRadius,
                targetW, targetH, NotifyBarRadius,
                ExpandDurationMs, null);

            _autoCollapseTimer.Stop();
            if (!_clickOutsideToClose)
            {
                _autoCollapseTimer.Interval = TimeSpan.FromMilliseconds(NotifyAutoCollapseMs);
                _autoCollapseTimer.Start();
            }
        }

        private void ResetPanelsToMenu()
        {
            _isNotifyMode = false;
            _notifyTargetNavIndex = -1;
            UpdateWindowBounds();

            CardTitle.Text = "🏠 CMW 菜单";
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
            if (!_isExpanded || _isBoundsAnimating) return;

            _isExpanded = false;
            _autoCollapseTimer.Stop();
            _mouseLeaveCollapseTimer.Stop();

            if (_isRollCallActive)
            {
                StopRollCallAndRestoreTabs();
            }

            SetContentState(capsule: true, animated: true, durationMs: 260);
            CardContent.IsHitTestVisible = false;
            CardHintText.Visibility = Visibility.Collapsed;

            double targetWidth = GetCapsuleWidth();

            AnimateInnerBorder(_cardWidth, _cardHeight, CardRadius,
                targetWidth, CapsuleHeight, CapsuleRadius,
                CollapseDurationMs, () =>
                {
                    CapsuleLabel.BeginAnimation(OpacityProperty, null);
                    CardContent.BeginAnimation(OpacityProperty, null);
                    CapsuleLabel.Opacity = 1;
                    CardContent.Opacity = 0;
                    ApplyCapsuleState();

                    _isNotifyMode = false;
                    UpdateWindowBounds();
                });
        }

        private void CollapseCardImmediate()
        {
            _isExpanded = false;
            _autoCollapseTimer.Stop();
            _mouseLeaveCollapseTimer.Stop();
            _radiusTimer.Stop();

            if (_isRollCallActive)
            {
                StopRollCallAndRestoreTabs();
            }

            CapsuleLabel.BeginAnimation(OpacityProperty, null);
            CardContent.BeginAnimation(OpacityProperty, null);
            CapsuleLabel.Opacity = 1;
            CardContent.Opacity = 0;
            CardContent.IsHitTestVisible = false;
            CardHintText.Visibility = Visibility.Collapsed;

            ApplyCapsuleState();
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

                CapsuleLabel.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(capsuleTarget, duration)
                    {
                        EasingFunction = easing,
                        FillBehavior = FillBehavior.Stop
                    });
                CardContent.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(cardTarget, duration)
                    {
                        EasingFunction = easing,
                        FillBehavior = FillBehavior.Stop
                    });
            }

            CapsuleLabel.Opacity = capsuleTarget;
            CardContent.Opacity = cardTarget;
        }

        private void AnimateInnerBorder(
            double fromWidth, double fromHeight, double fromRadius,
            double toWidth, double toHeight, double toRadius,
            double durationMs, Action? onCompleted)
        {
            _isBoundsAnimating = true;

            var duration = TimeSpan.FromMilliseconds(durationMs);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            int completedCount = 0;
            const int totalCount = 2;

            Action onOneComplete = () =>
            {
                completedCount++;
                if (completedCount >= totalCount)
                {
                    _isBoundsAnimating = false;
                    onCompleted?.Invoke();
                }
            };

            var widthAnim = new DoubleAnimation(fromWidth, toWidth, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            widthAnim.Completed += (s, e) =>
            {
                MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                MainBorder.Width = toWidth;
                onOneComplete();
            };
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(fromHeight, toHeight, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            heightAnim.Completed += (s, e) =>
            {
                MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
                MainBorder.Height = toHeight;
                onOneComplete();
            };
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);

            _radiusFrom = fromRadius;
            _radiusTo = toRadius;
            _radiusStartTime = DateTime.Now;
            _radiusDurationMs = durationMs;
            _radiusTimer.Start();
        }

        private void RadiusTimer_Tick(object? sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _radiusStartTime).TotalMilliseconds;
            double t = Math.Min(elapsed / _radiusDurationMs, 1.0);

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            double k = easing.Ease(t);
            double r = _radiusFrom + (_radiusTo - _radiusFrom) * k;

            MainBorder.CornerRadius = new CornerRadius(r);

            if (t >= 1.0)
            {
                _radiusTimer.Stop();
                MainBorder.CornerRadius = new CornerRadius(_radiusTo);
            }
        }

        // ═══════ 拖拽 ═══════

        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isTransitioning || _isBoundsAnimating) return;
            if (!_config.IslandDraggable || _isExpanded) return;

            EnsureDpiCache();

            _mouseDownPending = true;
            _isDragMoved = false;
            _dragStartScreen = new Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y);
            _dragStartLeft = Left;
            _dragStartTop = Top;

            try { MainBorder.CaptureMouse(); } catch { }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_mouseDownPending) return;

            var cur = new Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y);

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

        private void MainBorder_TouchDown(object sender, TouchEventArgs e)
        {
            if (_isTransitioning || _isBoundsAnimating) return;
            if (!_config.IslandDraggable || _isExpanded) return;

            EnsureDpiCache();

            _isTouchDragging = true;
            _touchId = e.TouchDevice.Id;
            _isDragMoved = false;
            _dragStartScreen = new Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y);
            _dragStartLeft = Left;
            _dragStartTop = Top;

            try { MainBorder.CaptureTouch(e.TouchDevice); } catch { }
            e.Handled = true;
        }

        private void MainBorder_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_isTouchDragging) return;
            if (e.TouchDevice.Id != _touchId) return;

            var cur = new Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y);

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

        private void MainBorder_TouchUp(object sender, TouchEventArgs e)
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
            if (_isTransitioning || _isBoundsAnimating) return;

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
                            || cur is ScrollViewer || cur is ItemsControl)
                            return;
                        cur = VisualTreeHelper.GetParent(cur);
                    }
                }
                CollapseCard();
            }
        }

        private void MainBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            _mouseLeaveCollapseTimer.Stop();
        }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_clickOutsideToClose) return;
            if (_isRollCallActive) return;

            if (_isExpanded && !_isBoundsAnimating)
            {
                _mouseLeaveCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Start();
            }
        }

        // ═══════ 菜单事件 ═══════

        private void MenuOpenMainBtn_Click(object sender, RoutedEventArgs e)
        {
            _ = TransitionToMainAsync();
        }

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
                else if (!App.Float.IsVisible)
                {
                    App.Float.Show();
                }

                if (wasNull)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { App.Float?.ShowFromRight(); }
                        catch { }
                    }), DispatcherPriority.Background);
                }
                else
                {
                    App.Float.ShowFromRight();
                }
            }
            catch { }
        }

        private void MenuShowLeftSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                if (App.LeftSchedule != null && App.LeftSchedule.IsLoaded)
                {
                    App.CloseLeftSchedule();
                    cfg.LeftScheduleEnabled = false;
                    JsonHelper.Save(AppPaths.Config, cfg);
                }
                else
                {
                    cfg.LeftScheduleEnabled = true;
                    JsonHelper.Save(AppPaths.Config, cfg);
                    App.EnsureLeftSchedule();
                }
            }
            catch { }
        }

        private void MenuExitBtn_Click(object sender, RoutedEventArgs e)
        {
            App.ExitApp();
        }

        // ═══════ 右侧窗口自动触发 ═══════

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
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { App.Float?.ShowFromRight(); }
                        catch { }
                    }), DispatcherPriority.Background);
                }
                else
                {
                    App.Float.ShowFromRight();
                }
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

        // ═══════ 主窗口过渡 ═══════

        private async Task TransitionToMainAsync()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            try
            {
                _autoCollapseTimer.Stop();
                _mouseLeaveCollapseTimer.Stop();
                _radiusTimer.Stop();
                _rollCallAutoCloseTimer.Stop();

                await PlayShrinkFadeAsync();

                Hide();
                ResetTransformAndOpacity();

                if (IsBoardActive())
                {
                    _isTransitioning = false;
                    return;
                }

                if (Application.Current.MainWindow is MainWindow main)
                    main.ShowWithExpandAnimation();
            }
            finally
            {
                _isTransitioning = false;
            }
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

        // ═══════ 通知 ═══════

        private void ShowNotifyInCard(string title, string content, int targetNavIndex = -1)
        {
            if (IsBoardActive()) return;

            _isNotifyMode = true;
            _notifyTargetNavIndex = targetNavIndex;

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
                    UpdateWindowBounds();

                    var (targetW, targetH) = CalcNotifyBarSize();

                    AnimateInnerBorder(
                        MainBorder.Width, MainBorder.Height, MainBorder.CornerRadius.TopLeft,
                        targetW, targetH, NotifyBarRadius,
                        ExpandDurationMs, null);
                }
                catch { }

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
            _isNotifyMode = false;

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
                _radiusTimer.Stop();
                _rollCallAutoCloseTimer.Stop();

                await PlayShrinkFadeAsync();

                Hide();
                ResetTransformAndOpacity();

                if (IsBoardActive())
                {
                    _isTransitioning = false;
                    return;
                }

                if (Application.Current.MainWindow is MainWindow main)
                {
                    main.ShowWithExpandAnimation();

                    await Task.Delay(220);
                    try { main.NavigateByIndex(navIndex); }
                    catch { }
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        // ═══════ 卡片 tab ═══════

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

            CardTitle.Text = _currentCardTab switch
            {
                "Homework" => "📝 今日作业",
                "Todo" => "✅ 今日待办",
                "Schedule" => "📚 今日课表",
                "Notes" => "📷 笔记",
                _ => "🏠 CMW 菜单"
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
                default: break;
            }
        }

        private void OpenTimerWindow()
        {
            try
            {
                if (_timerWindow != null && _timerWindow.IsLoaded)
                {
                    if (_timerWindow.WindowState == WindowState.Minimized)
                        _timerWindow.WindowState = WindowState.Normal;
                    _timerWindow.Activate();
                    return;
                }

                _timerWindow = new ClassTimerWindow();
                _timerWindow.Closed += (s, e) => _timerWindow = null;
                _timerWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开课堂倒计时失败：\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ═══════ 作业 tab ═══════

        private void RefreshCardHomework()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();

                var today = DateTime.Today;
                var todayList = list
                    .Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder)
                    .ThenBy(h => h.EndDate)
                    .ToList();

                var vms = todayList.Select(h => new CardHomeworkVm
                {
                    Id = h.Id,
                    IsCompleted = h.IsCompleted,
                    DisplayText = BuildHomeworkDisplayText(h),
                    TextBrush = GetHomeworkBrush(h.Priority)
                }).ToList();

                icCardHomework.ItemsSource = vms;
                tbCardHomeworkEmpty.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private static string BuildHomeworkDisplayText(HomeworkItem h)
        {
            string line = h.HomeworkName;
            if (!string.IsNullOrWhiteSpace(h.Subject))
                line += $"（{h.Subject}）";
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
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();
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

        // ═══════ 待办 tab ═══════

        private void RefreshCardTodo()
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();
                var today = DateTime.Today;

                var todayList = list
                    .Where(t => t.Deadline.HasValue
                             && t.Deadline.Value.Date == today
                             && !t.IsCompleted)
                    .ToList();

                var vms = todayList.Select(t => new CardTodoVm
                {
                    Id = t.Id,
                    IsCompleted = t.IsCompleted,
                    DisplayText = string.IsNullOrWhiteSpace(t.Content) ? "（无内容）" : t.Content
                }).ToList();

                icCardTodo.ItemsSource = vms;
                tbCardTodoEmpty.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
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
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();
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

        // ═══════ 课表 tab ═══════

        private void RefreshCardSchedule()
        {
            try
            {
                var textList = SchedulePage.GetTodayScheduleText();
                icCardSchedule.ItemsSource = textList;

                tbCardScheduleEmpty.Visibility = textList.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        // ═══════ 笔记 tab ═══════

        private void RefreshCardNotes()
        {
            try
            {
                var list = NoteService.GetAll();

                var vms = list.Select(n => new CardNoteVm
                {
                    Id = n.Id,
                    DisplayText = n.DisplayText,
                    CreatedText = n.CreatedText,
                    Note = n
                }).ToList();

                icCardNotes.ItemsSource = vms;

                tbCardNotesEmpty.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
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
                    MessageBox.Show("屏幕截图失败，请重试。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Drawing.Bitmap? cropped = null;
                var overlay = new ScreenshotOverlayWindow(fullScreen);
                bool? overlayResult = overlay.ShowDialog();
                if (overlayResult == true)
                    cropped = overlay.CroppedBitmap;

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
                MessageBox.Show("新增笔记失败：\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not CardNoteVm vm) return;

            if (MessageBox.Show(
                "确定删除这条笔记？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (NoteService.Delete(vm.Id))
            {
                RefreshCardNotes();
            }
        }

        // ═══════ 字体自适应 ═══════

        private void ApplyAdaptiveFontSize()
        {
            string content = NotifyContentText.Text ?? "";
            int len = content.Length;

            // 通知条不高，字号按高度比例缩
            var (_, h) = CalcNotifyBarSize();
            double maxByHeight = Math.Max(12, h * 0.35);   // 行高不超过条高 35%

            double baseSize;
            if (len <= 30) baseSize = 28;
            else if (len <= 60) baseSize = 24;
            else if (len <= 100) baseSize = 20;
            else if (len <= 160) baseSize = 17;
            else if (len <= 240) baseSize = 15;
            else if (len <= 340) baseSize = 13;
            else baseSize = 12;

            if (baseSize > maxByHeight) baseSize = maxByHeight;

            double scale = _config.IslandFontScale;
            if (scale < 0.8) scale = 0.8;
            if (scale > 1.5) scale = 1.5;

            NotifyContentText.FontSize = baseSize * scale;

            double titleMax = Math.Max(14, h * 0.28);
            double titleSize = (len <= 100 ? 22 : 20) * scale;
            if (titleSize > titleMax) titleSize = titleMax;
            CardTitle.FontSize = titleSize;

            foreach (var child in CardTabs.Children)
            {
                if (child is RadioButton rb)
                    rb.FontSize = 11 * scale;
            }
        }

        // ═══════ 定时器 ═══════

        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;

            if (_lastAlarmCheckTime.Minute == now.Minute
                && _lastAlarmCheckTime.Hour == now.Hour
                && _lastAlarmCheckTime.Date == now.Date)
                return;

            _lastAlarmCheckTime = now;

            // ★ 28.0.0：电子班牌定时开关
            CheckBoardAutoSchedule(now);

            if (now.Hour == 8 && now.Minute == 0)
                ShowNotifyInCard("📅 今日汇总", GetTodayInfoText(), 0);

            if (TryParseTime(_config.TodoRemindTime1, out var t1)
                && now.Hour == t1.Hours && now.Minute == t1.Minutes)
                ShowNotifyInCard("📝 待办提醒", GetTodayTodoText(), 2);

            if (TryParseTime(_config.TodoRemindTime2, out var t2)
                && now.Hour == t2.Hours && now.Minute == t2.Minutes)
                ShowNotifyInCard("📝 待办提醒", GetTodayTodoText(), 2);

            if (_config.TodoCustomRemindEnabled
                && TryParseTime(_config.TodoCustomRemindTime, out var tcr))
            {
                if (now.Hour == tcr.Hours && now.Minute == tcr.Minutes)
                {
                    string key = $"{now:yyyy-MM-dd}_todoCustom_{_config.TodoCustomRemindTime}";
                    if (_lastTodoCustomRemindFired != key)
                    {
                        _lastTodoCustomRemindFired = key;
                        ShowNotifyInCard("📝 待办提醒", GetTodayTodoText(), 2);
                    }
                }
            }

            if (_config.DutyRemindEnabled
                && TryParseTime(_config.DutyRemindTime, out var dr))
            {
                if (now.Hour == dr.Hours && now.Minute == dr.Minutes)
                {
                    string key = $"{now:yyyy-MM-dd}_duty_{_config.DutyRemindTime}";
                    if (_lastDutyRemindFired != key)
                    {
                        _lastDutyRemindFired = key;
                        string dutyName = GetDutyText();
                        string content = string.IsNullOrWhiteSpace(dutyName)
                            ? "今日未安排值日"
                            : dutyName;
                        ShowNotifyInCard("👥 值日提醒", content, 3);
                    }
                }
            }

            if (_config.HomeworkRemindEnabled)
            {
                foreach (var timeStr in _config.HomeworkRemindTimes)
                {
                    if (TryParseTime(timeStr, out var remindTime)
                        && now.Hour == remindTime.Hours
                        && now.Minute == remindTime.Minutes)
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
            if (slots == null || slots.Count == 0)
                slots = DefaultTimeSlots.Create();

            string style = _config.ClassRemindStyle ?? "Popup";
            bool doPopup = (style == "Popup" || style == "Both");
            bool doMini = (style == "Mini" || style == "Both");

            foreach (var slot in slots)
            {
                if (!slot.EnableNotify) continue;

                bool isStart = TryParseTime(slot.StartTime, out var s)
                            && now.Hour == s.Hours && now.Minute == s.Minutes;
                bool isEnd = TryParseTime(slot.EndTime, out var en)
                          && now.Hour == en.Hours && now.Minute == en.Minutes;

                if (isStart)
                {
                    if (slot.IsLongBreak)
                        TryShowRightPanel();
                    else
                        TryHideRightPanel();
                }
                else if (isEnd && !slot.IsLongBreak)
                {
                    TryShowRightPanel();
                }

                if (silent) continue;
                if (style == "None") continue;

                if (isStart && _config.ClassStartRemindEnabled)
                {
                    if (doPopup)
                        ShowNotifyInCard($"🔔 {slot.Name}", $"{slot.Name} 开始", 1);
                    else if (doMini)
                        ShowMiniRemindInCapsule($"🔔 {slot.Name}", _config.MiniRemindDurationSeconds);
                }

                if (isEnd && _config.ClassEndRemindEnabled)
                {
                    string nextClass = GetNextClassNameForBell();
                    string content = string.IsNullOrEmpty(nextClass)
                        ? $"{slot.Name} 结束 · 今日课程结束"
                        : $"{slot.Name} 结束 · 下节 {nextClass}";

                    if (doPopup)
                        ShowNotifyInCard($"🔕 {slot.Name}", content, 1);
                    else if (doMini)
                        ShowMiniRemindInCapsule($"🔕 {slot.Name}", _config.MiniRemindDurationSeconds);
                }
            }
        }

        /// <summary>
        /// ★ 28.0.0：电子班牌定时开关
        /// </summary>
        private void CheckBoardAutoSchedule(DateTime now)
        {
            try
            {
                if (!_config.BoardAutoScheduleEnabled) return;

                bool isStartTime = TryParseTime(_config.BoardAutoStartTime, out var st)
                                && now.Hour == st.Hours && now.Minute == st.Minutes;
                bool isEndTime = TryParseTime(_config.BoardAutoEndTime, out var et)
                              && now.Hour == et.Hours && now.Minute == et.Minutes;

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
                    if (App.Board != null && App.Board.IsLoaded)
                    {
                        App.Board.Close();
                    }
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
                    if (!string.IsNullOrWhiteSpace(lesson))
                        return lesson;
                }
            }
            catch { }
            return "";
        }

        private bool IsInSilentTime(DateTime now)
        {
            string startStr = _config.SilentStart ?? "";
            string endStr = _config.SilentEnd ?? "";
            if (string.IsNullOrWhiteSpace(startStr) || string.IsNullOrWhiteSpace(endStr))
                return false;

            if (!TryParseTime(startStr, out var start)) return false;
            if (!TryParseTime(endStr, out var end)) return false;

            var nowTime = now.TimeOfDay;
            if (start <= end)
                return nowTime >= start && nowTime < end;
            else
                return nowTime >= start || nowTime < end;
        }

        private List<string> GetUnfinishedHomeworkNames()
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();
                var today = DateTime.Today;

                return list
                    .Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder)
                    .Select(h => h.HomeworkName)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private void SaveHomeworkReminder(string timeStr, List<string> unfinished)
        {
            try
            {
                var records = JsonHelper.Load<List<HomeworkReminderRecord>>(AppPaths.HomeworkReminders)
                    ?? new List<HomeworkReminderRecord>();

                records.Add(new HomeworkReminderRecord
                {
                    RemindTimeStr = timeStr,
                    RemindAt = DateTime.Now,
                    UnfinishedHomework = unfinished
                });

                if (records.Count > 50)
                    records = records.Skip(records.Count - 50).ToList();

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

        // ═══════════════════════════════════════
        // 作业催收
        // ═══════════════════════════════════════

        private void ShowRollCallPanel(string timeStr)
        {
            try
            {
                if (IsBoardActive()) return;

                var studentData = JsonHelper.Load<StudentListData>(AppPaths.Students)
                                  ?? new StudentListData();
                var allStudents = (studentData.Students ?? new List<Student>())
                    .OrderBy(s => s.Order)
                    .ToList();

                if (allStudents.Count == 0)
                {
                    var unfinished = GetUnfinishedHomeworkNames();
                    if (unfinished.Count > 0)
                    {
                        ShowNotifyInCard("📝 作业催收",
                            $"有 {unfinished.Count} 项作业未完成，请到作业页点名", 4);
                        SaveHomeworkReminder(timeStr, unfinished);
                    }
                    return;
                }

                var allHw = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                            ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                var now = DateTime.Now;

                var todayHw = allHw
                    .Where(h => h.IsActiveOn(today)
                             && !h.IsCompleted
                             && h.IsRollCalled
                             && h.NeedSubmit
                             && now <= h.EndDate)
                    .OrderBy(h => h.PriorityOrder)
                    .ThenBy(h => h.HomeworkName)
                    .ToList();

                if (todayHw.Count == 0) return;

                var items = new List<(HomeworkItem Hw, List<Student> Unsubmitted)>();
                foreach (var hw in todayHw)
                {
                    var submittedIds = new HashSet<string>(
                        hw.SubmittedStudentIds ?? new List<string>());
                    var unsubmitted = allStudents
                        .Where(s => !submittedIds.Contains(s.Id))
                        .OrderBy(s => s.Order)
                        .ToList();
                    items.Add((hw, unsubmitted));
                }

                var unfinishedNames = items
                    .Where(x => x.Unsubmitted.Count > 0)
                    .Select(x => $"{x.Hw.HomeworkName}（未交 {x.Unsubmitted.Count} 人）")
                    .ToList();

                if (unfinishedNames.Count > 0)
                {
                    SaveHomeworkReminder(timeStr, unfinishedNames);
                }

                RollCallItemsPanel.Children.Clear();
                foreach (var item in items)
                {
                    var child = BuildRollCallItem(item.Hw, item.Unsubmitted);
                    RollCallItemsPanel.Children.Add(child);
                }

                _isRollCallActive = true;
                CardTabs.Visibility = Visibility.Collapsed;
                PanelInfo.Visibility = Visibility.Collapsed;
                PanelNotify.Visibility = Visibility.Collapsed;
                PanelHomework.Visibility = Visibility.Collapsed;
                PanelTodo.Visibility = Visibility.Collapsed;
                PanelSchedule.Visibility = Visibility.Collapsed;
                PanelNotes.Visibility = Visibility.Collapsed;
                PanelRollCall.Visibility = Visibility.Visible;
                CardTitle.Text = $"📢 作业催收（{timeStr}）";

                if (!_isExpanded)
                {
                    _isExpanded = true;
                    SetContentState(capsule: false, animated: true, durationMs: 300);
                    CardContent.IsHitTestVisible = true;
                    CardHintText.Visibility = Visibility.Collapsed;

                    double startWidth = GetCapsuleWidth();
                    AnimateInnerBorder(startWidth, CapsuleHeight, CapsuleRadius,
                        _cardWidth, _cardHeight, CardRadius,
                        ExpandDurationMs, null);
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
                FontFamily = (FontFamily)FindResource("AppleFont"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (unsubmitted.Count == 0)
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = "  ✅ 已交齐",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BF79C")),
                    FontSize = 13,
                    FontFamily = (FontFamily)FindResource("AppleFont"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            else
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = $"  未交 {unsubmitted.Count} 人",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9C9C")),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = (FontFamily)FindResource("AppleFont"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            stack.Children.Add(nameRow);

            if (unsubmitted.Count > 0)
            {
                string names = string.Join("  ", unsubmitted.Select(s => s.Name));

                var marqueeGrid = new Grid
                {
                    Height = 22,
                    Margin = new Thickness(0, 6, 0, 0),
                    ClipToBounds = true
                };

                var tb = new TextBlock
                {
                    Text = names + "          " + names,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDEEEEEE")),
                    FontSize = 13,
                    FontFamily = (FontFamily)FindResource("AppleFont"),
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
                    From = 0,
                    To = -cycle,
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

            CardTitle.Text = "🏠 CMW 菜单";
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
}