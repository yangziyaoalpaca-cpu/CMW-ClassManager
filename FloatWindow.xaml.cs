using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CMW
{
    public partial class FloatWindow : Window
    {
        private AppConfig _config = new AppConfig();
        private readonly DispatcherTimer _autoSwitchTimer;

        private bool _isShown;
        private bool _isAnimating;
        private bool _isLoading;
        private string _currentTab = "Homework";

        private const int AutoSwitchDefaultSeconds = 15;
        private const int AnimDurationMs = 300;

        private const double BaseTitle = 15;
        private const double BaseTab = 13;
        private const double BaseHomeworkName = 14;
        private const double BaseHomeworkDetail = 12;
        private const double BaseTodoContent = 14;
        private const double BaseTodoDetail = 12;
        private const double BaseEmpty = 13;

        public FloatWindow()
        {
            _isLoading = true;
            InitializeComponent();

            Loaded += FloatWindow_Loaded;

            _autoSwitchTimer = new DispatcherTimer();
            _autoSwitchTimer.Tick += (s, e) => DoAutoSwitch();
        }

        private void FloatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                ApplySizeAndPosition();
                ApplyFontScale();
                SetInitialHiddenState();
                ApplyContentMode();
                ApplyAutoSwitchSetting();

                App.SettingsChanged += OnSettingsChanged;
                App.TimeSlotsChanged += OnSettingsChanged;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private double GetScale()
        {
            double s = _config.RightPanelFontScale;
            if (s < 0.8) s = 0.8;
            if (s > 1.5) s = 1.5;
            return s;
        }

        private double GetTabScale(double windowWidth)
        {
            if (windowWidth < 260) return 0.72;
            if (windowWidth < 300) return 0.84;
            if (windowWidth < 340) return 0.94;
            return 1.0;
        }

        private void ApplyTabFontSize(double windowWidth)
        {
            try
            {
                double s = GetScale();
                double tabScale = GetTabScale(windowWidth);
                double tabFs = BaseTab * s * tabScale;

                TabHomework.FontSize = tabFs;
                TabDuty.FontSize = tabFs;
                TabTodo.FontSize = tabFs;
            }
            catch { }
        }

        private void ApplyFontScale()
        {
            try
            {
                double s = GetScale();

                TitleText.FontSize = BaseTitle * s;
                BtnClose.FontSize = 13 * s;

                tbHomeworkEmpty.FontSize = BaseEmpty * s;
                tbTodoEmptyRight.FontSize = BaseEmpty * s;

                ApplyTabFontSize(_config.FloatWindowWidth);
            }
            catch { }
        }

        private void ApplySizeAndPosition()
        {
            var wa = SystemParameters.WorkArea;

            double w = _config.FloatWindowWidth;
            if (w < 200) w = 360;
            if (w > 600) w = 600;

            double percent = _config.RightPanelHeightPercent;
            if (percent < 30) percent = 70;
            if (percent > 90) percent = 90;

            double h = wa.Height * (percent / 100.0);
            if (h < 300) h = 300;
            if (h > wa.Height - 40) h = wa.Height - 40;

            SetCurrentValue(WidthProperty, w);
            SetCurrentValue(HeightProperty, h);
            SetCurrentValue(LeftProperty, wa.Right - w);
            SetCurrentValue(TopProperty, wa.Top + (wa.Height - h) / 2);

            ApplyTabFontSize(w);
        }

        private void SetInitialHiddenState()
        {
            var tt = EnsureTransform();
            tt.BeginAnimation(TranslateTransform.XProperty, null);
            tt.X = ActualWidth > 0 ? ActualWidth : Width;
            _isShown = false;
        }

        private TranslateTransform EnsureTransform()
        {
            if (RootBorder.RenderTransform is TranslateTransform tt)
                return tt;

            tt = new TranslateTransform(0, 0);
            RootBorder.RenderTransform = tt;
            return tt;
        }

        public void ShowFromRight()
        {
            try
            {
                if (!IsVisible) Show();
                else Activate();

                if (_isShown && !_isAnimating) return;

                _isAnimating = true;

                var tt = EnsureTransform();

                tt.BeginAnimation(TranslateTransform.XProperty, null);
                double curWidth = ActualWidth > 0 ? ActualWidth : Width;
                if (double.IsNaN(curWidth) || curWidth <= 0) curWidth = 360;
                tt.X = curWidth;

                double startX = curWidth;
                double targetX = 0;

                var anim = new DoubleAnimation(startX, targetX,
                    TimeSpan.FromMilliseconds(AnimDurationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };

                anim.Completed += (s, e) =>
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = targetX;
                    _isShown = true;
                    _isAnimating = false;

                    RefreshCurrentContent();

                    ApplyAutoSwitchSetting();
                };

                tt.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            catch { }
        }

        public void HideToRight()
        {
            try
            {
                if (!_isShown && !_isAnimating) return;

                _isAnimating = true;
                _autoSwitchTimer.Stop();

                var tt = EnsureTransform();

                tt.BeginAnimation(TranslateTransform.XProperty, null);
                tt.X = 0;

                double curWidth = ActualWidth > 0 ? ActualWidth : Width;
                if (double.IsNaN(curWidth) || curWidth <= 0) curWidth = 360;
                double targetX = curWidth;

                var anim = new DoubleAnimation(0, targetX,
                    TimeSpan.FromMilliseconds(AnimDurationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                    FillBehavior = FillBehavior.Stop
                };

                anim.Completed += (s, e) =>
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = targetX;
                    _isShown = false;
                    _isAnimating = false;
                };

                tt.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            catch { }
        }

        public bool IsShown => _isShown;

        private void ApplyContentMode()
        {
            switch (_config.FloatWindowContentMode)
            {
                case "Homework":
                    TabHomework.IsChecked = true;
                    _currentTab = "Homework";
                    break;
                case "Duty":
                    TabDuty.IsChecked = true;
                    _currentTab = "Duty";
                    break;
                case "Todo":
                    TabTodo.IsChecked = true;
                    _currentTab = "Todo";
                    break;
                case "Auto":
                default:
                    TabHomework.IsChecked = true;
                    _currentTab = "Homework";
                    break;
            }

            UpdatePanelVisibility();
            RefreshCurrentContent();
        }

        private void UpdatePanelVisibility()
        {
            PanelHomework.Visibility = _currentTab == "Homework"
                ? Visibility.Visible : Visibility.Collapsed;
            PanelDuty.Visibility = _currentTab == "Duty"
                ? Visibility.Visible : Visibility.Collapsed;
            PanelTodo.Visibility = _currentTab == "Todo"
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (sender is not RadioButton rb) return;

            _currentTab = rb.Name switch
            {
                "TabDuty" => "Duty",
                "TabTodo" => "Todo",
                _ => "Homework"
            };

            UpdatePanelVisibility();
            RefreshCurrentContent();
        }

        private void RefreshCurrentContent()
        {
            switch (_currentTab)
            {
                case "Duty": RefreshDuty(); break;
                case "Todo": RefreshTodo(); break;
                default: RefreshHomework(); break;
            }
        }

        private void RefreshHomework()
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

                double s = GetScale();
                double nameFs = BaseHomeworkName * s;
                double detailFs = BaseHomeworkDetail * s;

                var vms = todayList.Select(h => new RightHomeworkVm
                {
                    Name = h.HomeworkName,
                    Detail = BuildHomeworkDetail(h),
                    NameBrush = GetHomeworkNameBrush(h.Priority),
                    NameFontSize = nameFs,
                    DetailFontSize = detailFs
                }).ToList();

                icHomework.ItemsSource = vms;
                tbHomeworkEmpty.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private static string BuildHomeworkDetail(HomeworkItem h)
        {
            string d = "";
            if (!string.IsNullOrWhiteSpace(h.Subject))
                d += h.Subject + " · ";
            d += "截止 " + h.DateRangeText;
            if (h.Priority == "Urgent" || h.Priority == "VeryUrgent")
                d += " · " + h.PriorityText;
            return d;
        }

        private static Brush GetHomeworkNameBrush(string priority)
        {
            return priority switch
            {
                "VeryUrgent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6666")),
                "Urgent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB366")),
                _ => Brushes.White
            };
        }

        private void RefreshDuty()
        {
            try
            {
                string groupName = DutyPage.GetTodayDutyGroupName();

                string raw = string.IsNullOrWhiteSpace(groupName)
                    ? "今日无值日"
                    : $"今日{groupName}组值日";

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < raw.Length; i++)
                {
                    sb.Append(raw[i]);
                    if (i < raw.Length - 1) sb.Append('\n');
                }
                tbDutyBig.Text = sb.ToString();
            }
            catch
            {
                tbDutyBig.Text = "今\n日\n无\n值\n日";
            }
        }

        private void RefreshTodo()
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

                double s = GetScale();
                double contentFs = BaseTodoContent * s;
                double detailFs = BaseTodoDetail * s;

                var vms = todayList.Select(t => new RightTodoVm
                {
                    Content = string.IsNullOrWhiteSpace(t.Content) ? "（无内容）" : t.Content,
                    Detail = BuildTodoDetail(t),
                    ContentFontSize = contentFs,
                    DetailFontSize = detailFs
                }).ToList();

                icTodoRight.ItemsSource = vms;
                tbTodoEmptyRight.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private static string BuildTodoDetail(TodoItem t)
        {
            string d = "";
            if (!string.IsNullOrWhiteSpace(t.Remark))
                d += t.Remark + " · ";
            d += "截止 " + t.DeadlineText;
            return d;
        }

        private void ApplyAutoSwitchSetting()
        {
            bool shouldRun = _config.FloatWindowContentMode == "Auto"
                             && _config.FloatWindowAutoSwitch
                             && _isShown;

            if (shouldRun)
            {
                int sec = _config.RightPanelAutoSwitchSeconds;
                if (sec < 5) sec = AutoSwitchDefaultSeconds;
                if (sec > 60) sec = 60;

                _autoSwitchTimer.Interval = TimeSpan.FromSeconds(sec);

                if (!_autoSwitchTimer.IsEnabled)
                    _autoSwitchTimer.Start();
            }
            else
            {
                _autoSwitchTimer.Stop();
            }
        }

        private void DoAutoSwitch()
        {
            if (!_isShown) return;

            string next = _currentTab switch
            {
                "Homework" => "Duty",
                "Duty" => "Todo",
                _ => "Homework"
            };

            switch (next)
            {
                case "Duty": TabDuty.IsChecked = true; break;
                case "Todo": TabTodo.IsChecked = true; break;
                default: TabHomework.IsChecked = true; break;
            }
        }

        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 固定在屏幕右侧，不拖动
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            HideToRight();
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                ApplyFontScale();

                if (!_isShown)
                {
                    ApplySizeAndPosition();

                    var tt = EnsureTransform();
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    double curWidth = ActualWidth > 0 ? ActualWidth : Width;
                    if (double.IsNaN(curWidth) || curWidth <= 0) curWidth = 360;
                    tt.X = curWidth;
                }
                else
                {
                    ApplySizeAndPosition();
                    RefreshCurrentContent();
                }

                ApplyAutoSwitchSetting();
            }), DispatcherPriority.Background);
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoSwitchTimer.Stop();
            App.SettingsChanged -= OnSettingsChanged;
            App.TimeSlotsChanged -= OnSettingsChanged;
            base.OnClosed(e);
        }
    }

    internal class RightHomeworkVm
    {
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";
        public Brush NameBrush { get; set; } = Brushes.White;
        public double NameFontSize { get; set; } = 14;
        public double DetailFontSize { get; set; } = 12;
    }

    internal class RightTodoVm
    {
        public string Content { get; set; } = "";
        public string Detail { get; set; } = "";
        public double ContentFontSize { get; set; } = 14;
        public double DetailFontSize { get; set; } = 12;
    }
}