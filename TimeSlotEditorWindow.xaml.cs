using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CMW
{
    public partial class TimeSlotEditorWindow : Window
    {
        private List<TimeSlot> _workingSlots = new List<TimeSlot>();
        private readonly bool _isRightPanelMode;

        private static readonly Brush RowBg = Brushes.White;
        private static readonly Brush RowBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E1E1"));
        private static readonly Brush InputBg = Brushes.White;
        private static readonly Brush InputBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8C8D8"));
        private static readonly Brush InputFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));
        private static readonly Brush DelBtnBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
        private static readonly Brush DelBtnBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D4DA"));
        private static readonly Brush DelBtnFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"));
        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        /// <summary>
        /// ★ 28.0.0：支持双模式
        /// isRightPanelMode = false（默认）→ 编辑主 TimeSlots
        /// isRightPanelMode = true → 编辑 RightPanelTimeSlots
        /// </summary>
        public TimeSlotEditorWindow() : this(false)
        {
        }

        public TimeSlotEditorWindow(bool isRightPanelMode)
        {
            _isRightPanelMode = isRightPanelMode;
            InitializeComponent();

            Title = _isRightPanelMode
                ? "编辑右侧窗口独立时段"
                : "编辑时间段";

            LoadSlots();
            BuildRows();
        }

        private void LoadSlots()
        {
            var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

            List<TimeSlot>? slots;
            if (_isRightPanelMode)
            {
                slots = config.RightPanelTimeSlots;
                if (slots == null || slots.Count == 0)
                {
                    // 首次开启 → 用主表作为初始值
                    slots = config.TimeSlots
                        .Select(t => new TimeSlot
                        {
                            Name = t.Name,
                            StartTime = t.StartTime,
                            EndTime = t.EndTime,
                            EnableNotify = t.EnableNotify,
                            IsLongBreak = t.IsLongBreak
                        }).ToList();

                    if (slots.Count == 0)
                        slots = DefaultTimeSlots.Create();
                }
            }
            else
            {
                slots = config.TimeSlots;
                if (slots == null || slots.Count == 0)
                    slots = DefaultTimeSlots.Create();
            }

            _workingSlots = slots
                .Select(s => new TimeSlot
                {
                    Name = s.Name,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    EnableNotify = s.EnableNotify,
                    IsLongBreak = s.IsLongBreak
                })
                .ToList();

            foreach (var s in _workingSlots)
            {
                if (!s.IsLongBreak && IsLongBreakByName(s.Name))
                {
                    s.IsLongBreak = true;
                }
            }
        }

        private static bool IsLongBreakByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            return name.Contains("早读")
                || name.Contains("大课间")
                || name.Contains("午自习")
                || name.Contains("中自习")
                || name.Contains("前自习")
                || name.Contains("晚自习")
                || name.Contains("课间操");
        }

        private void BuildRows()
        {
            slotListPanel.Children.Clear();

            foreach (var slot in _workingSlots)
            {
                slotListPanel.Children.Add(CreateRow(slot));
            }
        }

        private Border CreateRow(TimeSlot slot)
        {
            var border = new Border
            {
                Background = RowBg,
                BorderBrush = RowBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var txtName = CreateInput(slot.Name);
            txtName.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(txtName, 0);
            grid.Children.Add(txtName);

            var txtStart = CreateInput(slot.StartTime);
            txtStart.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(txtStart, 1);
            grid.Children.Add(txtStart);

            var txtEnd = CreateInput(slot.EndTime);
            txtEnd.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(txtEnd, 2);
            grid.Children.Add(txtEnd);

            var chkNotify = new CheckBox
            {
                IsChecked = slot.EnableNotify,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Style = (Style)FindResource("IosToggle")
            };
            Grid.SetColumn(chkNotify, 3);
            grid.Children.Add(chkNotify);

            var chkLong = new CheckBox
            {
                IsChecked = slot.IsLongBreak,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Style = (Style)FindResource("IosToggle")
            };
            Grid.SetColumn(chkLong, 4);
            grid.Children.Add(chkLong);

            var btnDel = new Button
            {
                Content = "删除",
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                FontFamily = UiFont,
                Foreground = DelBtnFg,
                Background = DelBtnBg,
                BorderBrush = DelBtnBorder,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnDel.Click += (s, e) =>
            {
                _workingSlots.Remove(slot);
                BuildRows();
            };
            Grid.SetColumn(btnDel, 5);
            grid.Children.Add(btnDel);

            txtName.TextChanged += (s, e) => slot.Name = txtName.Text;
            txtStart.TextChanged += (s, e) => slot.StartTime = txtStart.Text;
            txtEnd.TextChanged += (s, e) => slot.EndTime = txtEnd.Text;
            chkNotify.Checked += (s, e) => slot.EnableNotify = true;
            chkNotify.Unchecked += (s, e) => slot.EnableNotify = false;
            chkLong.Checked += (s, e) => slot.IsLongBreak = true;
            chkLong.Unchecked += (s, e) => slot.IsLongBreak = false;

            border.Child = grid;
            return border;
        }

        private TextBox CreateInput(string text)
        {
            return new TextBox
            {
                Text = text,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 13,
                FontFamily = UiFont,
                Foreground = InputFg,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = InputBg,
                BorderBrush = InputBorder,
                BorderThickness = new Thickness(1)
            };
        }

        private void BtnAddSlot_Click(object sender, RoutedEventArgs e)
        {
            _workingSlots.Add(new TimeSlot
            {
                Name = "新时段",
                StartTime = "08:00",
                EndTime = "08:45",
                EnableNotify = true,
                IsLongBreak = false
            });
            BuildRows();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            foreach (var slot in _workingSlots)
            {
                if (string.IsNullOrWhiteSpace(slot.Name))
                {
                    tbTip.Text = "❌ 存在空名称，请检查";
                    return;
                }

                if (!TimeSpan.TryParse(slot.StartTime, out var start))
                {
                    tbTip.Text = $"❌ 「{slot.Name}」开始时间格式错误";
                    return;
                }

                if (!TimeSpan.TryParse(slot.EndTime, out var end))
                {
                    tbTip.Text = "❌ 「" + slot.Name + "」结束时间格式错误";
                    return;
                }

                if (end <= start)
                {
                    tbTip.Text = $"❌ 「{slot.Name}」结束时间必须晚于开始时间";
                    return;
                }
            }

            var sorted = _workingSlots
                .OrderBy(s => TimeSpan.Parse(s.StartTime))
                .ToList();

            var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

            if (_isRightPanelMode)
            {
                config.RightPanelTimeSlots = sorted;
            }
            else
            {
                config.TimeSlots = sorted;
            }

            JsonHelper.Save(AppPaths.Config, config);

            // 只有主表变化才通知主窗口重建
            if (!_isRightPanelMode)
            {
                App.NotifyTimeSlotsChanged();
            }
            else
            {
                App.NotifySettingsChanged();
            }

            MessageBox.Show("✅ 时间段已保存", "提示");
            Close();
        }
    }
}