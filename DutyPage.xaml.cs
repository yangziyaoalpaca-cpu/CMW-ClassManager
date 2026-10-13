using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CMW
{
    public partial class DutyPage : UserControl
    {
        private const int MaxDutyGroups = 9;

        private List<DutyGroup> _dutyGroups;
        private DutyGroup? _todayDutyGroup;
        private StackPanel? _currentPanel;

        public DutyPage()
        {
            InitializeComponent();
            _dutyGroups = new List<DutyGroup>();
            Loaded += DutyPage_Loaded;
        }

        private void DutyPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDutyData();

            // ★ 28.0.0：改为调用统一的轮换服务（App 启动 / MainWindow 激活也会调）
            DutyRotateService.TryRotateIfNeeded();

            // 轮换可能改了 duty.json，重载一次
            LoadDutyData();

            RefreshDutyGroupList();
            RefreshTodayDutyButtons();
            UpdateCurrentDutyText();
            _currentPanel = PanelToday;
        }

        // ═══════════════════════════════════════
        // 二级选项卡切换
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabToday" => PanelToday,
                "TabGroups" => PanelGroups,
                _ => null
            };

            if (target == null || target == _currentPanel) return;
            SwitchPanel(target);
        }

        private void SwitchPanel(StackPanel target)
        {
            if (_currentPanel != null)
                _currentPanel.Visibility = Visibility.Collapsed;

            target.Visibility = Visibility.Visible;
            target.Opacity = 0;

            var translate = new TranslateTransform(30, 0);
            target.RenderTransform = translate;

            var txAnim = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            var opAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            txAnim.Completed += (s, e) => { translate.X = 0; };
            opAnim.Completed += (s, e) => { target.Opacity = 1; };

            translate.BeginAnimation(TranslateTransform.XProperty, txAnim);
            target.BeginAnimation(UIElement.OpacityProperty, opAnim);

            _currentPanel = target;
        }

        // ═══════════════════════════════════════
        // 新增 / 删除值日组
        // ═══════════════════════════════════════

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_dutyGroups.Count >= MaxDutyGroups)
            {
                MessageBox.Show("最多只能保存 9 个值日组。");
                return;
            }

            string groupName = txtGroupName.Text.Trim();
            string membersText = txtMembers.Text.Trim();

            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show("请输入组名。");
                return;
            }

            _dutyGroups.Add(new DutyGroup
            {
                GroupName = groupName,
                MembersText = membersText
            });

            txtGroupName.Text = string.Empty;
            txtMembers.Text = string.Empty;

            SaveDutyData();
            RefreshDutyGroupList();
            RefreshTodayDutyButtons();
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DutyGroup group)
            {
                RecycleBinService.Add(
                    RecycleBinService.TypeDutyGroup,
                    group.GroupName,
                    group);

                _dutyGroups.Remove(group);
                if (_todayDutyGroup == group) _todayDutyGroup = null;

                SaveDutyData();
                RefreshDutyGroupList();
                RefreshTodayDutyButtons();
                UpdateCurrentDutyText();
            }
        }

        // ═══════════════════════════════════════
        // 刷新
        // ═══════════════════════════════════════

        private void RefreshDutyGroupList()
        {
            icDutyGroupList.ItemsSource = null;
            icDutyGroupList.ItemsSource = _dutyGroups;

            tbGroupEmpty.Visibility = _dutyGroups.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshTodayDutyButtons()
        {
            foreach (var child in ugTodayDutyButtons.Children)
            {
                if (child is not Button button) continue;
                int index = Convert.ToInt32(button.Tag);
                int listIndex = index - 1;

                if (listIndex >= 0 && listIndex < _dutyGroups.Count)
                {
                    var group = _dutyGroups[listIndex];
                    button.Content = group.GroupName;
                    button.IsEnabled = true;
                    button.Style = _todayDutyGroup == group
                        ? (Style)FindResource("SelectedDutySelectBtn")
                        : (Style)FindResource("DutySelectBtn");
                }
                else
                {
                    button.Content = $"第{index}组";
                    button.IsEnabled = false;
                    button.Style = (Style)FindResource("DutySelectBtn");
                }
            }

            UpdateCurrentDutyText();
        }

        private void UpdateCurrentDutyText()
        {
            if (_todayDutyGroup == null)
            {
                tbCurrentDuty.Text = "今日未安排";
                tbCurrentDuty.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080"));
            }
            else
            {
                tbCurrentDuty.Text = _todayDutyGroup.GroupName;
                tbCurrentDuty.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));
            }
        }

        // ═══════════════════════════════════════
        // 选择今日值日
        // ═══════════════════════════════════════

        private void TodayDutyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            int index = Convert.ToInt32(button.Tag);
            int listIndex = index - 1;

            if (listIndex < 0 || listIndex >= _dutyGroups.Count) return;

            _todayDutyGroup = _dutyGroups[listIndex];
            SaveDutyData();
            RefreshTodayDutyButtons();
        }

        // ═══════════════════════════════════════
        // 数据读写
        // ═══════════════════════════════════════

        private void LoadDutyData()
        {
            var data = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
            if (data == null) return;

            _dutyGroups = data.DutyGroups ?? new List<DutyGroup>();

            if (!string.IsNullOrEmpty(data.TodayDutyGroupName))
            {
                _todayDutyGroup = _dutyGroups.FirstOrDefault(
                    g => g.GroupName == data.TodayDutyGroupName);
            }
        }

        private void SaveDutyData()
        {
            try
            {
                JsonHelper.Save(AppPaths.Duty, new DutySaveModel
                {
                    DutyGroups = _dutyGroups,
                    TodayDutyGroupName = _todayDutyGroup?.GroupName
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}");
            }
        }

        public static string GetTodayDutyGroupName()
        {
            var data = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
            return data?.TodayDutyGroupName ?? string.Empty;
        }

        public static List<string> GetTodayDutyMembers()
        {
            string groupName = GetTodayDutyGroupName();
            if (string.IsNullOrEmpty(groupName)) return new List<string>();

            var data = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
            var targetGroup = data?.DutyGroups?.FirstOrDefault(g => g.GroupName == groupName);
            if (targetGroup == null || string.IsNullOrWhiteSpace(targetGroup.MembersText))
                return new List<string>();

            return targetGroup.MembersText
                .Split(new[] { '，', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
    }
}