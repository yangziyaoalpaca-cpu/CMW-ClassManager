using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CMW
{
    public partial class SummaryPage : UserControl
    {
        public SummaryPage()
        {
            InitializeComponent();
            Loaded += SummaryPage_Loaded;
            Unloaded += SummaryPage_Unloaded;
        }

        private void SummaryPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDate();
            RefreshAllData();
            UpdateBoardButton();

            App.BoardStateChanged -= OnBoardStateChanged;
            App.BoardStateChanged += OnBoardStateChanged;
        }

        private void SummaryPage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.BoardStateChanged -= OnBoardStateChanged;
        }

        private void OnBoardStateChanged()
        {
            Dispatcher.BeginInvoke(new Action(UpdateBoardButton));
        }

        // ★ 27.3.0：电子班牌开关
        private void BtnClassBoard_Click(object sender, RoutedEventArgs e)
        {
            // 已开启 → 关闭
            if (App.Board != null && App.Board.IsLoaded)
            {
                App.Board.Close();
                return;
            }

            // 未开启 → 打开
            try
            {
                var board = new ClassBoardWindow();
                App.Board = board;

                board.Closed += (s, ev) =>
                {
                    App.Board = null;
                };

                board.Show();
                UpdateBoardButton();
            }
            catch (Exception ex)
            {
                App.Board = null;
                MessageBox.Show("打开电子班牌失败：\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateBoardButton()
        {
            bool active = App.Board != null && App.Board.IsLoaded;
            BtnClassBoard.Content = active
                ? "📺 电子班牌运行中（点击关闭）"
                : "📺 开启电子班牌";
        }

        // ★ A1.3：日期格式优化
        private void RefreshDate()
        {
            var now = DateTime.Now;
            var zh = CultureInfo.GetCultureInfo("zh-CN");
            string dateStr = now.ToString("yyyy-MM-dd dddd", zh);

            string prefix = "";
            var tomorrow = DateTime.Today.AddDays(1);
            if (now.Date == DateTime.Today) prefix = "今天 · ";
            else if (now.Date == tomorrow) prefix = "明天 · ";

            tbDate.Text = prefix + dateStr;
        }

        public void RefreshAllData()
        {
            RefreshTodaySchedule();
            RefreshTodayDuty();
            RefreshTodayHomework();
            RefreshTodayTodo();
            RefreshStatusTags();
        }

        private void RefreshTodaySchedule()
        {
            icTodaySchedule.ItemsSource = SchedulePage.GetTodayScheduleText();
        }

        private void RefreshTodayDuty()
        {
            string groupName = DutyPage.GetTodayDutyGroupName();
            var members = DutyPage.GetTodayDutyMembers();

            tbDutyGroupName.Text = string.IsNullOrWhiteSpace(groupName) ? "今日未安排" : groupName;
            tbDutyMembers.Text = members.Count > 0 ? string.Join("、", members) : "暂无值日成员";
            tbDutyStatus.Text = string.IsNullOrWhiteSpace(groupName)
                ? "今日值日：未安排"
                : "今日值日：已安排";
        }

        private void RefreshTodayHomework()
        {
            icTodayHomework.ItemsSource = HomeworkPage.GetTodayHomeworkForSummary();
        }

        private void RefreshTodayTodo()
        {
            icTodayTodo.ItemsSource = TodoPage.GetTodayTodoForSummary();
        }

        private void RefreshStatusTags()
        {
            tbScheduleCount.Text = $"今日课表：{SchedulePage.GetTodayScheduleText().Count}节";
            tbDutyCount.Text = $"今日值日：{(string.IsNullOrWhiteSpace(DutyPage.GetTodayDutyGroupName()) ? 0 : 1)}组";
            tbHomeworkCount.Text = $"今日作业：{HomeworkPage.GetTodayHomeworkForSummary().Count}项";
            tbTodoCount.Text = $"今日待办：{TodoPage.GetTodayTodoForSummary().Count}条";
            tbCompletedCount.Text = $"作业已完成：{HomeworkPage.GetTodayCompletedCount()}项";
        }
    }

    public class BoolToStrikeConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return TextDecorations.Strikethrough;
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}