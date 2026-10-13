using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CMW
{
    public partial class ClassBoardWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _refreshTimer;

        private bool _islandWasVisible;
        private bool _floatWasVisible;

        public ClassBoardWindow()
        {
            InitializeComponent();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateTime();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _refreshTimer.Tick += (s, e) => LoadData();

            Loaded += ClassBoardWindow_Loaded;
            Closed += ClassBoardWindow_Closed;
            KeyDown += ClassBoardWindow_KeyDown;
        }

        private void ClassBoardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 记录灵动岛 / 右侧窗口的原可见状态，然后隐藏它们
            try
            {
                _islandWasVisible = App.Island != null && App.Island.IsVisible;
                _floatWasVisible = App.Float != null && App.Float.IsVisible;

                if (App.Island != null) App.Island.Hide();
                if (App.Float != null) App.Float.Hide();
            }
            catch { }

            UpdateTime();
            LoadData();

            _clockTimer.Start();
            _refreshTimer.Start();

            App.NotifyBoardStateChanged();
        }

        private void ClassBoardWindow_Closed(object? sender, EventArgs e)
        {
            _clockTimer.Stop();
            _refreshTimer.Stop();

            // 恢复灵动岛 / 右侧窗口
            try
            {
                if (_islandWasVisible && App.Island != null) App.Island.Show();
                if (_floatWasVisible && App.Float != null) App.Float.Show();
            }
            catch { }

            App.NotifyBoardStateChanged();
        }

        private void ClassBoardWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                BtnExit_Click(sender, e);
            }
        }

        // ═══════ 时间 ═══════

        private void UpdateTime()
        {
            try
            {
                var zh = CultureInfo.GetCultureInfo("zh-CN");
                tbDateTime.Text = DateTime.Now.ToString("yyyy-MM-dd dddd HH:mm:ss", zh);
            }
            catch { }
        }

        // ═══════ 数据 ═══════

        private void LoadData()
        {
            try
            {
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                tbClassName.Text = string.IsNullOrWhiteSpace(config.ClassName)
                    ? "CMW 班级管理"
                    : config.ClassName;

                // 课表
                var scheduleText = SchedulePage.GetTodayScheduleText();
                icSchedule.ItemsSource = scheduleText;
                tbScheduleEmpty.Visibility = scheduleText.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                // 作业
                var hwList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                             ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                var todayHw = hwList
                    .Where(h => h.IsActiveOn(today) && !h.IsCompleted)
                    .OrderBy(h => h.PriorityOrder)
                    .ThenBy(h => h.EndDate)
                    .Select(h => string.IsNullOrWhiteSpace(h.Subject)
                        ? h.HomeworkName
                        : $"{h.HomeworkName}（{h.Subject}）")
                    .ToList();
                icHomework.ItemsSource = todayHw;
                tbHomeworkEmpty.Visibility = todayHw.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                // 值日
                string dutyName = DutyPage.GetTodayDutyGroupName();
                tbDutyName.Text = string.IsNullOrWhiteSpace(dutyName) ? "今日未安排" : dutyName;
                var dutyMembers = DutyPage.GetTodayDutyMembers();
                tbDutyMembers.Text = dutyMembers.Count > 0
                    ? string.Join("、", dutyMembers)
                    : "";

                // 待办
                var tdList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                             ?? new List<TodoItem>();
                var todayTodo = tdList
                    .Where(t => t.Deadline.HasValue
                             && t.Deadline.Value.Date == today
                             && !t.IsCompleted)
                    .Select(t => t.Content)
                    .ToList();
                icTodo.ItemsSource = todayTodo;
                tbTodoEmpty.Visibility = todayTodo.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                // 考试倒计时
                UpdateExamCountdown(config);
            }
            catch { }
        }

        private void UpdateExamCountdown(AppConfig config)
        {
            try
            {
                string name = (config.ExamName ?? "").Trim();
                string dateStr = (config.ExamDate ?? "").Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dateStr))
                {
                    ExamCountdownBar.Visibility = Visibility.Collapsed;
                    return;
                }

                if (!DateTime.TryParse(dateStr, out var examDate))
                {
                    ExamCountdownBar.Visibility = Visibility.Collapsed;
                    return;
                }

                int days = (examDate.Date - DateTime.Today).Days;
                if (days < 0) days = 0;

                tbExamName.Text = name;
                tbExamDays.Text = days.ToString();
                ExamCountdownBar.Visibility = Visibility.Visible;
            }
            catch
            {
                ExamCountdownBar.Visibility = Visibility.Collapsed;
            }
        }

        // ═══════ 退出 ═══════

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "确定关闭电子班牌？",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Close();
        }
    }
}