using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class MainWindow : Window
    {
        public string? SelectedTodayDutyGroupName;

        private bool _isTransitioning;
        private bool _suppressStateChange;
        private bool _isNavigating;
        private int _currentNavIndex = 0;
        private const double SlideDistance = 70;
        private const int MaxNavIndex = 12;

        private bool _isMaximized = false;
        private Rect _restoreBounds = Rect.Empty;

        private Point _touchStartPoint;
        private bool _isHorizontalSwipe;
        private const double HorizontalSwipeThreshold = 100;
        private const double HorizontalSwipeMinDelta = 30;

        // ★ 33.0.0：侧栏按钮数组 + 防递归标志
        private RadioButton[] _navButtons = Array.Empty<RadioButton>();
        private bool _suppressNavChecked = false;

        public MainWindow()
        {
            InitializeComponent();

            // ★ 33.0.0：初始化侧栏按钮数组（顺序与 index 一致）
            _navButtons = new[]
            {
                NavHome, NavSchedule, NavTodo, NavDuty, NavHomework,
                NavSummary, NavStudents, NavExam, NavLogs, NavStatistics,
                NavSeat, NavCalendar, NavSettings
            };

            ResetToDefaultBounds();

            _currentNavIndex = 0;
            MainFrame.Navigate(CreatePageByIndex(0));

            // ★ 33.0.0：初始高亮首页
            UpdateNavHighlight(0);

            App.SettingsChanged += OnSettingsChanged;
            App.TimeSlotsChanged += OnTimeSlotsChanged;

            PreviewTouchDown += MainWindow_PreviewTouchDown;
            PreviewTouchMove += MainWindow_PreviewTouchMove;
            PreviewTouchUp += MainWindow_PreviewTouchUp;

            Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            try { DutyRotateService.TryRotateIfNeeded(); }
            catch { }
        }

        private static object CreatePageByIndex(int index)
        {
            return index switch
            {
                0 => new HomePage(),
                1 => new SchedulePage(),
                2 => new TodoPage(),
                3 => new DutyPage(),
                4 => new HomeworkPage(),
                5 => new SummaryPage(),
                6 => new StudentsPage(),
                7 => new ExamPage(),
                8 => new LogsPage(),
                9 => new StatisticsPage(),
                10 => new SeatPage(),
                11 => new CalendarPage(),
                12 => new SettingsPage(),
                _ => new HomePage()
            };
        }

        private void ResetToDefaultBounds()
        {
            try
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);
                BeginAnimation(OpacityProperty, null);
            }
            catch { }

            var wa = SystemParameters.WorkArea;

            double w = Math.Min(1100, Math.Max(720, wa.Width - 40));
            double h = Math.Min(720, Math.Max(480, wa.Height - 40));

            _suppressStateChange = true;
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            _suppressStateChange = false;

            _isMaximized = false;
            _restoreBounds = Rect.Empty;

            Width = w;
            Height = h;
            Left = wa.Left + (wa.Width - w) / 2;
            Top = wa.Top + (wa.Height - h) / 2;

            try { Opacity = 1; } catch { }
        }

        // ═══════════════════════════════════════════════════════════
        // ★ 33.0.0：侧栏导航高亮同步
        // ═══════════════════════════════════════════════════════════
        private void UpdateNavHighlight(int index)
        {
            if (index < 0 || index >= _navButtons.Length) return;

            _suppressNavChecked = true;
            try
            {
                var btn = _navButtons[index];
                if (btn != null && !btn.IsChecked.GetValueOrDefault())
                    btn.IsChecked = true;
            }
            catch { }
            finally
            {
                _suppressNavChecked = false;
            }
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressNavChecked) return;
            if (sender is not RadioButton rb) return;

            int idx = Array.IndexOf(_navButtons, rb);
            if (idx < 0) return;
            if (idx == _currentNavIndex) return;
            if (_isNavigating) return;

            SafeNavigate(idx);
        }

        // ═══════════════════════════════════════════════════════════
        // 触摸手势
        // ═══════════════════════════════════════════════════════════
        private void MainWindow_PreviewTouchDown(object? sender, TouchEventArgs e)
        {
            _isHorizontalSwipe = false;
            _touchStartPoint = e.GetTouchPoint(this).Position;
        }

        private void MainWindow_PreviewTouchMove(object? sender, TouchEventArgs e)
        {
            if (_isHorizontalSwipe)
            {
                e.Handled = true;
                return;
            }

            var pos = e.GetTouchPoint(this).Position;
            double dx = pos.X - _touchStartPoint.X;
            double dy = pos.Y - _touchStartPoint.Y;

            if (Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dx) > HorizontalSwipeMinDelta)
            {
                _isHorizontalSwipe = true;
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewTouchUp(object? sender, TouchEventArgs e)
        {
            if (!_isHorizontalSwipe)
            {
                _isHorizontalSwipe = false;
                return;
            }

            var pos = e.GetTouchPoint(this).Position;
            double dx = pos.X - _touchStartPoint.X;

            _isHorizontalSwipe = false;
            e.Handled = true;

            if (Math.Abs(dx) < HorizontalSwipeThreshold) return;
            if (_isNavigating) return;

            int newIndex;
            if (dx < 0)
                newIndex = _currentNavIndex + 1;
            else
                newIndex = _currentNavIndex - 1;

            if (newIndex < 0 || newIndex > MaxNavIndex) return;

            SafeNavigate(newIndex);
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(RefreshCurrentPage), DispatcherPriority.Background);
        }

        private void OnTimeSlotsChanged()
        {
            Dispatcher.BeginInvoke(new Action(RefreshCurrentPage), DispatcherPriority.Background);
        }

        private void RefreshCurrentPage()
        {
            if (_isNavigating) return;
            if (_currentNavIndex == 12) return;

            try
            {
                MainFrame.Navigate(CreatePageByIndex(_currentNavIndex));
            }
            catch (Exception ex)
            {
                MessageBox.Show("刷新页面失败：\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            if (_isTransitioning) return;
            _ = TransitionToIslandAsync();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (_suppressStateChange) return;
            if (_isTransitioning) return;

            if (WindowState == WindowState.Minimized)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isTransitioning) return;
                    _ = TransitionToIslandAsync(fromMinimize: true);
                }), DispatcherPriority.Background);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.OriginalSource is DependencyObject src)
            {
                DependencyObject? cur = src;
                while (cur != null)
                {
                    if (cur is Button) return;
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }

            try { DragMove(); } catch { }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximize()
        {
            if (!_isMaximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);

                var wa = SystemParameters.WorkArea;
                Left = wa.Left;
                Top = wa.Top;
                Width = wa.Width;
                Height = wa.Height;

                _isMaximized = true;
            }
            else
            {
                if (_restoreBounds != Rect.Empty)
                {
                    Left = _restoreBounds.Left;
                    Top = _restoreBounds.Top;
                    Width = _restoreBounds.Width;
                    Height = _restoreBounds.Height;
                }
                _isMaximized = false;
            }
        }

        private async Task TransitionToIslandAsync(bool fromMinimize = false)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            try
            {
                if (fromMinimize)
                {
                    _suppressStateChange = true;
                    WindowState = WindowState.Normal;
                    _suppressStateChange = false;
                    await Dispatcher.Yield(DispatcherPriority.Render);
                }

                await PlayShrinkAnimationAsync();

                _suppressStateChange = true;
                Hide();
                _suppressStateChange = false;

                ResetTransformAndOpacity();

                App.Island ??= new IslandWindow();
                App.Island.ShowWithExpandAnimation();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private Task PlayShrinkAnimationAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var duration = TimeSpan.FromMilliseconds(150);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };

            var sx = new DoubleAnimation(1, 0.8, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var sy = new DoubleAnimation(1, 0.8, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var op = new DoubleAnimation(1, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };

            sx.Completed += (s, e) => { RootScale.ScaleX = 0.8; };
            sy.Completed += (s, e) => { RootScale.ScaleY = 0.8; };
            op.Completed += (s, e) => { Opacity = 0; tcs.TrySetResult(true); };

            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
            BeginAnimation(OpacityProperty, op);

            return tcs.Task;
        }

        public void ShowWithExpandAnimation()
        {
            ResetToDefaultBounds();

            _currentNavIndex = 0;
            try { MainFrame.Navigate(CreatePageByIndex(0)); }
            catch { }

            // ★ 33.0.0：同步侧栏高亮
            UpdateNavHighlight(0);

            Show();
            Activate();

            var duration = TimeSpan.FromMilliseconds(200);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            RootScale.ScaleX = 0.8;
            RootScale.ScaleY = 0.8;
            Opacity = 0;

            var sx = new DoubleAnimation(0.8, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var sy = new DoubleAnimation(0.8, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };
            var op = new DoubleAnimation(0, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };

            sx.Completed += (s, e) => { RootScale.ScaleX = 1; };
            sy.Completed += (s, e) => { RootScale.ScaleY = 1; };
            op.Completed += (s, e) => { Opacity = 1; };

            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
            BeginAnimation(OpacityProperty, op);
        }

        private void ResetTransformAndOpacity()
        {
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            BeginAnimation(OpacityProperty, null);
            RootScale.ScaleX = 1;
            RootScale.ScaleY = 1;
            Opacity = 1;
        }

        private void SafeNavigate(int index)
        {
            try
            {
                object page = CreatePageByIndex(index);
                NavigateWithTransition(page, index);
            }
            catch (Exception ex)
            {
                _isNavigating = false;

                // ★ 显示完整堆栈，方便定位 NRE
                MessageBox.Show(
                    "打开页面失败（index=" + index + "）：\n\n" + ex.ToString(),
                    "导航错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void NavigateByIndex(int index)
        {
            if (index < 0 || index > MaxNavIndex) return;
            if (index == _currentNavIndex)
            {
                // ★ 33.0.0：即使同页，也确保高亮
                UpdateNavHighlight(index);
                return;
            }
            SafeNavigate(index);
        }

        private void NavigateWithTransition(object newPage, int newIndex)
        {
            if (newIndex == _currentNavIndex && !_isNavigating)
            {
                // ★ 33.0.0：确保高亮
                UpdateNavHighlight(newIndex);
                return;
            }

            if (_isNavigating)
            {
                _isNavigating = false;
                FrameTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                FrameTranslate.X = 0;
                FrameHost.BeginAnimation(UIElement.OpacityProperty, null);
                FrameHost.Opacity = 1;
            }

            _isNavigating = true;
            int direction = newIndex > _currentNavIndex ? -1 : 1;
            _currentNavIndex = newIndex;

            // ★ 33.0.0：导航瞬间高亮
            UpdateNavHighlight(newIndex);

            StartNavTimeout();

            try
            {
                var slideOutDuration = TimeSpan.FromMilliseconds(120);
                var slideOutEasing = new CubicEase { EasingMode = EasingMode.EaseIn };

                var outTx = new DoubleAnimation(direction * SlideDistance, slideOutDuration)
                {
                    EasingFunction = slideOutEasing,
                    FillBehavior = FillBehavior.Stop
                };
                var outOp = new DoubleAnimation(0, slideOutDuration)
                {
                    EasingFunction = slideOutEasing,
                    FillBehavior = FillBehavior.Stop
                };

                outOp.Completed += (s, e) =>
                {
                    try
                    {
                        MainFrame.Navigate(newPage);

                        FrameTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                        FrameHost.BeginAnimation(UIElement.OpacityProperty, null);
                        FrameTranslate.X = -direction * SlideDistance;
                        FrameHost.Opacity = 0;

                        Dispatcher.BeginInvoke(new Action(PlaySlideIn), DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        _isNavigating = false;
                        MessageBox.Show(
                            "页面切换失败：\n\n" + ex.Message,
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                FrameTranslate.BeginAnimation(TranslateTransform.XProperty, outTx);
                FrameHost.BeginAnimation(UIElement.OpacityProperty, outOp);
            }
            catch (Exception ex)
            {
                _isNavigating = false;
                MessageBox.Show("动画启动失败：\n\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DispatcherTimer? _navTimeoutTimer;

        private void StartNavTimeout()
        {
            _navTimeoutTimer?.Stop();

            _navTimeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _navTimeoutTimer.Tick += (s, e) =>
            {
                _navTimeoutTimer.Stop();

                if (_isNavigating)
                {
                    _isNavigating = false;
                    FrameTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    FrameTranslate.X = 0;
                    FrameHost.BeginAnimation(UIElement.OpacityProperty, null);
                    FrameHost.Opacity = 1;
                }
            };
            _navTimeoutTimer.Start();
        }

        private void PlaySlideIn()
        {
            try
            {
                var slideInDuration = TimeSpan.FromMilliseconds(200);
                var slideInEasing = new CubicEase { EasingMode = EasingMode.EaseOut };

                var inTx = new DoubleAnimation(0, slideInDuration)
                {
                    EasingFunction = slideInEasing,
                    FillBehavior = FillBehavior.Stop
                };
                var inOp = new DoubleAnimation(1, slideInDuration)
                {
                    EasingFunction = slideInEasing,
                    FillBehavior = FillBehavior.Stop
                };

                inTx.Completed += (s, e) =>
                {
                    FrameTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    FrameTranslate.X = 0;
                    _isNavigating = false;
                    _navTimeoutTimer?.Stop();
                };

                inOp.Completed += (s, e) =>
                {
                    FrameHost.BeginAnimation(UIElement.OpacityProperty, null);
                    FrameHost.Opacity = 1;
                };

                FrameTranslate.BeginAnimation(TranslateTransform.XProperty, inTx);
                FrameHost.BeginAnimation(UIElement.OpacityProperty, inOp);
            }
            catch
            {
                _isNavigating = false;
                FrameTranslate.X = 0;
                FrameHost.Opacity = 1;
            }
        }

        public TodayInfo GetTodayDisplayData()
        {
            DateTime now = DateTime.Now;

            var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
            string scheduleText = BuildTodayScheduleText(now, schedule);

            var duty = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
            string dutyText = string.IsNullOrWhiteSpace(duty?.TodayDutyGroupName)
                ? "今日未安排" : duty!.TodayDutyGroupName!;

            var homeworkList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework) ?? new List<HomeworkItem>();
            var todayHw = homeworkList.Where(h => h.IsActiveOn(now)).ToList();
            string homeworkText = todayHw.Count > 0
                ? string.Join("；", todayHw.Select(h => h.HomeworkName)) : "今日无作业";

            var todoList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo) ?? new List<TodoItem>();
            var todayTodo = todoList.Where(t => t.Deadline.HasValue
                && t.Deadline.Value.Date == now.Date && !t.IsCompleted).ToList();
            string todoText = todayTodo.Count > 0
                ? string.Join("；", todayTodo.Select(t => t.Content)) : "暂无待办";

            return new TodayInfo
            {
                Schedule = string.IsNullOrWhiteSpace(scheduleText) ? "今日无课程" : scheduleText,
                Duty = dutyText,
                Homework = homeworkText,
                Todo = todoText
            };
        }

        private string BuildTodayScheduleText(DateTime now, ScheduleData data)
        {
            int weekDay = (int)now.DayOfWeek;
            if (weekDay < 1 || weekDay > 6) return "";

            string today = now.ToString("yyyy-MM-dd");

            var baseMap = new Dictionary<int, ScheduleItem>();
            foreach (var item in data.WeekBase.Where(x => x.WeekDay == weekDay))
                baseMap[item.Period] = item;

            var ovMap = new Dictionary<int, ScheduleOverride>();
            foreach (var ov in data.Overrides.Where(x => x.Date == today))
                ovMap[ov.Period] = ov;

            var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            var slots = config.TimeSlots;
            if (slots == null || slots.Count == 0)
                slots = DefaultTimeSlots.Create();

            var lines = new List<string>();
            for (int period = 1; period <= slots.Count; period++)
            {
                string slotName = (period - 1 < slots.Count) ? slots[period - 1].Name : $"第{period}节";

                if (ovMap.TryGetValue(period, out var ov))
                {
                    if (!string.IsNullOrWhiteSpace(ov.LessonName))
                        lines.Add($"{slotName}：{ov.LessonName}（{ov.Teacher}）");
                }
                else if (baseMap.TryGetValue(period, out var item))
                {
                    if (!string.IsNullOrWhiteSpace(item.LessonName))
                        lines.Add($"{slotName}：{item.LessonName}（{item.Teacher}）");
                }
            }
            return string.Join(" | ", lines);
        }

        public string GetTodayHomeworkText()
        {
            DateTime now = DateTime.Now;
            var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                ?? new List<HomeworkItem>();

            var today = list.Where(h => h.IsActiveOn(now) && !h.IsCompleted)
                .OrderBy(h => h.PriorityOrder)
                .ThenBy(h => h.EndDate)
                .ToList();

            if (today.Count == 0) return "";
            return string.Join("\n", today.Select(h =>
                $"· {h.HomeworkName}（{h.Subject}）截止 {h.DateRangeText}"));
        }

        public string GetTodayTodoText()
        {
            DateTime today = DateTime.Now.Date;
            var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                ?? new List<TodoItem>();

            var todayList = list.Where(t => t.Deadline.HasValue
                && t.Deadline.Value.Date == today
                && !t.IsCompleted).ToList();

            if (todayList.Count == 0) return "";
            return string.Join("\n", todayList.Select(t => $"· {t.Content}"));
        }

        public string GetTodayScheduleFullText()
        {
            DateTime now = DateTime.Now;
            var data = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
            string result = BuildTodayScheduleText(now, data);
            if (string.IsNullOrWhiteSpace(result)) return "";
            return result.Replace(" | ", "\n");
        }
    }
}