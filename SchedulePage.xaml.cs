using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CMW
{
    public partial class SchedulePage : UserControl
    {
        private ScheduleData _data = new ScheduleData();

        private Border[,] _cellBorders = new Border[0, 6];
        private TextBlock[,] _cellTexts = new TextBlock[0, 6];

        private int _rowCount = 0;
        private bool _isGridBuilt;
        private StackPanel? _currentPanel;

        private const double CellRowHeight = 44;

        private static readonly Brush HeaderBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
        private static readonly Brush HeaderFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));
        private static readonly Brush CellBg = Brushes.White;
        private static readonly Brush CellFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"));
        private static readonly Brush CellBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8DCE0"));
        private static readonly Brush OverrideBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE"));
        private static readonly Brush OverrideFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));

        private static readonly (string Bg, string Fg)[] SubjectPalette = new[]
        {
            ("#E3F2FD", "#0D47A1"),
            ("#E8F5E9", "#1B5E20"),
            ("#FFEBEE", "#B71C1C"),
            ("#F3E5F5", "#4A148C"),
            ("#FFF3E0", "#E65100"),
            ("#E0F7FA", "#006064"),
            ("#F1F8E9", "#33691E"),
            ("#FCE4EC", "#880E4F"),
            ("#EDE7F6", "#311B92"),
            ("#E0F2F1", "#004D40"),
        };

        private static readonly Dictionary<string, (Brush Bg, Brush Fg)> _subjectColorCache
            = new Dictionary<string, (Brush, Brush)>();

        public SchedulePage()
        {
            InitializeComponent();
            Loaded += SchedulePage_Loaded;
        }

        private void SchedulePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isGridBuilt) return;

            BuildGrid();
            LoadScheduleData();
            RefreshAllCells();

            _isGridBuilt = true;
            _currentPanel = PanelSchedule;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabSchedule" => PanelSchedule,
                "TabTimeSlots" => PanelTimeSlots,
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

        private static (Brush Bg, Brush Fg) GetSubjectColors(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                return (CellBg, CellFg);

            if (_subjectColorCache.TryGetValue(subject, out var cached))
                return cached;

            int hash = 0;
            foreach (char c in subject)
                hash = hash * 31 + c;
            if (hash < 0) hash = -hash;

            int idx = hash % SubjectPalette.Length;
            var (bgHex, fgHex) = SubjectPalette[idx];

            var bg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));
            var fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex));
            bg.Freeze();
            fg.Freeze();

            var result = (bg, fg);
            _subjectColorCache[subject] = result;
            return result;
        }

        private void BuildGrid()
        {
            var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            var slots = config.TimeSlots;
            if (slots == null || slots.Count == 0)
                slots = DefaultTimeSlots.Create();

            _rowCount = slots.Count;

            gridScheduleTable.Children.Clear();
            gridScheduleTable.RowDefinitions.Clear();

            gridScheduleTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < _rowCount; i++)
            {
                gridScheduleTable.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(CellRowHeight)
                });
            }

            AddHeaderCell("节次", 0, 0);
            AddHeaderCell("周一", 0, 1);
            AddHeaderCell("周二", 0, 2);
            AddHeaderCell("周三", 0, 3);
            AddHeaderCell("周四", 0, 4);
            AddHeaderCell("周五", 0, 5);
            AddHeaderCell("周六", 0, 6);

            _cellBorders = new Border[_rowCount, 6];
            _cellTexts = new TextBlock[_rowCount, 6];

            for (int period = 0; period < _rowCount; period++)
            {
                var rowLabelBorder = new Border
                {
                    Background = HeaderBg,
                    BorderBrush = CellBorderBrush,
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = slots[period].Name,
                        FontSize = 12,
                        FontFamily = new FontFamily("Microsoft YaHei UI"),
                        Foreground = CellFg,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    }
                };
                Grid.SetRow(rowLabelBorder, period + 1);
                Grid.SetColumn(rowLabelBorder, 0);
                gridScheduleTable.Children.Add(rowLabelBorder);

                for (int w = 0; w < 6; w++)
                {
                    var cellText = new TextBlock
                    {
                        Text = "",
                        FontSize = 12,
                        FontFamily = new FontFamily("Microsoft YaHei UI"),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = CellFg
                    };

                    var cellBorder = new Border
                    {
                        Background = CellBg,
                        BorderBrush = CellBorderBrush,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Child = cellText,
                        Tag = (period, w)
                    };

                    cellBorder.MouseLeftButtonUp += Cell_Border_MouseLeftButtonUp;

                    Grid.SetRow(cellBorder, period + 1);
                    Grid.SetColumn(cellBorder, w + 1);
                    gridScheduleTable.Children.Add(cellBorder);

                    _cellBorders[period, w] = cellBorder;
                    _cellTexts[period, w] = cellText;
                }
            }
        }

        private void AddHeaderCell(string text, int row, int col)
        {
            var border = new Border
            {
                Background = HeaderBg,
                BorderBrush = CellBorderBrush,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HeaderFg,
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8)
                }
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            gridScheduleTable.Children.Add(border);
        }

        private void LoadScheduleData()
        {
            _data = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
            if (_data.WeekBase == null) _data.WeekBase = new List<ScheduleItem>();
            if (_data.Overrides == null) _data.Overrides = new List<ScheduleOverride>();
            if (_data.DayOverrides == null) _data.DayOverrides = new List<DayOverride>();
        }

        private void SaveScheduleData()
        {
            try
            {
                // ★ 保证非 null（避免后续保存 null 字段）
                if (_data.WeekBase == null) _data.WeekBase = new List<ScheduleItem>();
                if (_data.Overrides == null) _data.Overrides = new List<ScheduleOverride>();
                if (_data.DayOverrides == null) _data.DayOverrides = new List<DayOverride>();

                JsonHelper.Save(AppPaths.Schedule, _data);
            }
            catch (Exception ex) { MessageBox.Show($"保存失败：{ex.Message}"); }
        }

        /// <summary>
        /// ★ 28.0.0：加天级换课支持
        /// </summary>
        private void RefreshAllCells()
        {
            if (_rowCount == 0) return;

            DateTime today = DateTime.Today;
            string todayStr = today.ToString("yyyy-MM-dd");
            int actualWeekDay = (int)today.DayOfWeek;

            // 天级换课
            int effectiveWeekDay = actualWeekDay;
            var dayOv = _data.DayOverrides?.FirstOrDefault(x => x.Date == todayStr);
            if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                effectiveWeekDay = dayOv.SourceWeekDay;

            for (int period = 0; period < _rowCount; period++)
            {
                for (int w = 0; w < 6; w++)
                {
                    var cellText = _cellTexts[period, w];
                    var cellBorder = _cellBorders[period, w];
                    if (cellText == null || cellBorder == null) continue;

                    int periodNum = period + 1;
                    int weekDayNum = w + 1;

                    // 单节临时换课
                    var ov = _data.Overrides?.FirstOrDefault(
                        x => x.Date == todayStr
                          && x.Period == periodNum
                          && x.WeekDay == weekDayNum);

                    if (ov != null && !string.IsNullOrWhiteSpace(ov.LessonName))
                    {
                        cellText.Text = $"{ov.LessonName}\n{ov.Teacher}";
                        cellText.Foreground = OverrideFg;
                        cellBorder.Background = OverrideBg;
                        continue;
                    }

                    // ★ 天级换课：只影响"今天"这一列
                    int sourceWeekDay = weekDayNum;
                    if (dayOv != null && weekDayNum == actualWeekDay
                        && effectiveWeekDay >= 1 && effectiveWeekDay <= 6)
                    {
                        sourceWeekDay = effectiveWeekDay;
                    }

                    var item = _data.WeekBase?.FirstOrDefault(
                        x => x.WeekDay == sourceWeekDay && x.Period == periodNum);

                    if (item != null && !string.IsNullOrWhiteSpace(item.LessonName))
                    {
                        cellText.Text = $"{item.LessonName}\n{item.Teacher}";
                        var (bg, fg) = GetSubjectColors(item.LessonName);
                        cellBorder.Background = bg;
                        cellText.Foreground = fg;
                    }
                    else
                    {
                        cellText.Text = "";
                        cellText.Foreground = CellFg;
                        cellBorder.Background = CellBg;
                    }
                }
            }
        }

        private void Cell_Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not ValueTuple<int, int> pos) return;

            e.Handled = true;

            int periodIndex = pos.Item1;
            int weekdayIndex = pos.Item2;
            int periodNum = periodIndex + 1;
            int weekDayNum = weekdayIndex + 1;

            OpenCellEditor(border, periodIndex, weekdayIndex, periodNum, weekDayNum);
        }

        private void OpenCellEditor(Border cellBorder, int periodIndex, int weekdayIndex, int periodNum, int weekDayNum)
        {
            // ★ 保证基础列表非 null
            if (_data.WeekBase == null) _data.WeekBase = new List<ScheduleItem>();
            if (_data.Overrides == null) _data.Overrides = new List<ScheduleOverride>();

            string today = DateTime.Now.ToString("yyyy-MM-dd");

            var ovNow = _data.Overrides.FirstOrDefault(
                x => x.Date == today
                  && x.Period == periodNum
                  && x.WeekDay == weekDayNum);
            bool isTempMode = ovNow != null;

            var baseItem = _data.WeekBase.FirstOrDefault(
                x => x.WeekDay == weekDayNum && x.Period == periodNum);

            // ★ 修复：明确用 string.Empty 而非 ""
            string course = ovNow?.LessonName ?? baseItem?.LessonName ?? string.Empty;
            string teacher = ovNow?.Teacher ?? baseItem?.Teacher ?? string.Empty;

            var win = new Window
            {
                Title = "编辑课表",
                Width = 400,
                Height = 340,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EBF0"))
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))
            };
            titleBar.MouseLeftButtonDown += (s, ee) =>
            {
                if (ee.ButtonState == MouseButtonState.Pressed) win.DragMove();
            };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = $"编辑 · 周{weekdayIndex + 1} 第{periodNum}行",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(titleText, 0);
            titleGrid.Children.Add(titleText);

            var closeBtn = new Button
            {
                Content = "✕",
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Cursor = Cursors.Hand,
                Width = 44,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center,
                Template = CreateFlatButtonTemplate("#2E6FDD", "#E81123")
            };
            closeBtn.Click += (s, ee) => win.Close();
            Grid.SetColumn(closeBtn, 1);
            titleGrid.Children.Add(closeBtn);

            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var contentGrid = new Grid { Margin = new Thickness(16) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D4DA")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20)
            };

            var form = new StackPanel();

            form.Children.Add(new TextBlock
            {
                Text = "课程名称",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txtCourse = new TextBox { Text = course, Margin = new Thickness(0, 0, 0, 12) };
            form.Children.Add(txtCourse);

            form.Children.Add(new TextBlock
            {
                Text = "老师",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txtTeacher = new TextBox { Text = teacher, Margin = new Thickness(0, 0, 0, 12) };
            form.Children.Add(txtTeacher);

            var chkTemp = new CheckBox
            {
                Content = "临时换课（仅今天生效）",
                IsChecked = isTempMode,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"))
            };
            form.Children.Add(chkTemp);

            card.Child = form;
            Grid.SetRow(card, 0);
            contentGrid.Children.Add(card);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var btnCancel = new Button { Content = "取消", Width = 80, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
            ApplyStyle(btnCancel, "PclButton");

            var btnOk = new Button { Content = "确定", Width = 80, Height = 32 };
            ApplyStyle(btnOk, "PclButtonPrimary");

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 1);
            contentGrid.Children.Add(btnPanel);

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            win.Content = root;

            btnCancel.Click += (s, ee) => win.Close();

            btnOk.Click += (s, ee) =>
            {
                string c = txtCourse.Text.Trim();
                string t = txtTeacher.Text.Trim();
                bool asTemp = chkTemp.IsChecked == true;

                // ★ 保证列表非 null（保险）
                if (_data.WeekBase == null) _data.WeekBase = new List<ScheduleItem>();
                if (_data.Overrides == null) _data.Overrides = new List<ScheduleOverride>();

                if (asTemp)
                {
                    _data.Overrides.RemoveAll(x =>
                        x.Date == today
                        && x.Period == periodNum
                        && x.WeekDay == weekDayNum);

                    _data.Overrides.Add(new ScheduleOverride
                    {
                        Date = today,
                        WeekDay = weekDayNum,
                        Period = periodNum,
                        LessonName = c,
                        Teacher = t
                    });
                }
                else
                {
                    _data.Overrides.RemoveAll(x =>
                        x.Date == today
                        && x.Period == periodNum
                        && x.WeekDay == weekDayNum);

                    var exist = _data.WeekBase.FirstOrDefault(
                        x => x.WeekDay == weekDayNum && x.Period == periodNum);
                    if (exist == null)
                    {
                        _data.WeekBase.Add(new ScheduleItem
                        {
                            WeekDay = weekDayNum,
                            Period = periodNum,
                            LessonName = c,
                            Teacher = t
                        });
                    }
                    else
                    {
                        exist.LessonName = c;
                        exist.Teacher = t;
                    }
                }

                SaveScheduleData();
                RefreshAllCells();
                win.Close();
            };

            win.ShowDialog();
        }

        private static void ApplyStyle(Button btn, string styleKey)
        {
            try
            {
                var style = Application.Current?.TryFindResource(styleKey) as Style;
                if (style != null) btn.Style = style;
            }
            catch { }
        }

        private static ControlTemplate CreateFlatButtonTemplate(string hoverColor, string pressColor)
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bg";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverColor)), "Bg"));
            template.Triggers.Add(hoverTrigger);

            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(pressColor)), "Bg"));
            template.Triggers.Add(pressTrigger);

            return template;
        }

        // ═══════ 按钮 ═══════

        private void BtnEditTimeSlots_Click(object sender, RoutedEventArgs e)
        {
            var editor = new TimeSlotEditorWindow(false)
            {
                Owner = Window.GetWindow(this)
            };
            editor.ShowDialog();

            if (!IsLoaded) return;

            _isGridBuilt = false;
            BuildGrid();
            LoadScheduleData();
            RefreshAllCells();
            _isGridBuilt = true;

            TabSchedule.IsChecked = true;
        }

        private void BtnCustomChange_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请直接点击课表单元格进行编辑；在弹窗中勾选「临时换课」即可只对今天生效。",
                            "提示");
        }

        // ★ 28.0.0：新增"天级临时换课"按钮
        private void BtnDayChange_Click(object sender, RoutedEventArgs e)
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");

            if (_data.DayOverrides == null) _data.DayOverrides = new List<DayOverride>();

            var existing = _data.DayOverrides.FirstOrDefault(x => x.Date == today);

            var win = new Window
            {
                Title = "天级临时换课",
                Width = 360,
                Height = 220,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EBF0"))
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))
            };
            titleBar.MouseLeftButtonDown += (s, ee) =>
            {
                if (ee.ButtonState == MouseButtonState.Pressed) win.DragMove();
            };
            titleBar.Child = new TextBlock
            {
                Text = "今天按周几的课表上？",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var content = new Grid { Margin = new Thickness(16) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = "选择今天要使用哪一天的课表。取消 = 恢复正常。",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lbl, 0);
            content.Children.Add(lbl);

            var cbDay = new ComboBox { Margin = new Thickness(0, 10, 0, 10) };
            cbDay.Items.Add(new ComboBoxItem { Content = "周一", Tag = 1 });
            cbDay.Items.Add(new ComboBoxItem { Content = "周二", Tag = 2 });
            cbDay.Items.Add(new ComboBoxItem { Content = "周三", Tag = 3 });
            cbDay.Items.Add(new ComboBoxItem { Content = "周四", Tag = 4 });
            cbDay.Items.Add(new ComboBoxItem { Content = "周五", Tag = 5 });
            cbDay.Items.Add(new ComboBoxItem { Content = "周六", Tag = 6 });

            if (existing != null && existing.SourceWeekDay >= 1 && existing.SourceWeekDay <= 6)
                cbDay.SelectedIndex = existing.SourceWeekDay - 1;
            else
                cbDay.SelectedIndex = 0;

            Grid.SetRow(cbDay, 1);
            content.Children.Add(cbDay);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = new Button { Content = "取消换课", Width = 100, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
            if (Application.Current?.TryFindResource("PclButton") is Style s1) btnCancel.Style = s1;

            var btnOk = new Button { Content = "确定", Width = 80, Height = 32 };
            if (Application.Current?.TryFindResource("PclButtonPrimary") is Style s2) btnOk.Style = s2;

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnOk);
            Grid.SetRow(btnRow, 2);
            content.Children.Add(btnRow);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            win.Content = root;

            btnCancel.Click += (s, ee) =>
            {
                _data.DayOverrides.RemoveAll(x => x.Date == today);
                SaveScheduleData();
                RefreshAllCells();
                win.Close();
            };

            btnOk.Click += (s, ee) =>
            {
                if (cbDay.SelectedItem is not ComboBoxItem ci) return;
                int srcDay = Convert.ToInt32(ci.Tag);

                _data.DayOverrides.RemoveAll(x => x.Date == today);
                _data.DayOverrides.Add(new DayOverride
                {
                    Date = today,
                    SourceWeekDay = srcDay
                });

                SaveScheduleData();
                RefreshAllCells();
                win.Close();
            };

            win.ShowDialog();
        }

        private void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV 文件|*.csv|所有文件|*.*",
                Title = "导入课表"
            };
            if (openDlg.ShowDialog() != true) return;

            var (items, error) = CsvImportService.ParseCsv(openDlg.FileName);

            if (items == null)
            {
                MessageBox.Show($"导入失败：{error}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show("CSV 文件中没有有效的课程数据", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                $"将导入 {items.Count} 条课程。\n\n现有课表中相同「星期+节次」的课程会被覆盖，其余保留。\n\n是否继续？",
                "确认导入",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (_data.WeekBase == null) _data.WeekBase = new List<ScheduleItem>();

            foreach (var newItem in items)
            {
                var existing = _data.WeekBase.FirstOrDefault(x =>
                    x.WeekDay == newItem.WeekDay && x.Period == newItem.Period);
                if (existing != null)
                {
                    existing.LessonName = newItem.LessonName;
                    existing.Teacher = newItem.Teacher;
                }
                else
                {
                    _data.WeekBase.Add(newItem);
                }
            }

            SaveScheduleData();
            RefreshAllCells();

            MessageBox.Show($"✅ 已导入 {items.Count} 条课程", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = "课表模板.csv"
            };
            if (saveDlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(saveDlg.FileName,
                    CsvImportService.GenerateTemplate(),
                    new UTF8Encoding(true));

                MessageBox.Show("✅ 模板已保存，可用 Excel 或记事本编辑后导入",
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            SaveScheduleData();
            MessageBox.Show("课表已保存到 schedule.json");
        }

        public static List<string> GetTodayScheduleText()
        {
            var result = new List<string>();
            var data = JsonHelper.Load<ScheduleData>(AppPaths.Schedule);
            if (data == null) return result;

            var now = DateTime.Now;
            int weekDayNum = (int)now.DayOfWeek;
            if (weekDayNum < 1 || weekDayNum > 6) return result;

            string today = now.ToString("yyyy-MM-dd");

            // ★ 天级换课
            int effectiveWeekDay = weekDayNum;
            if (data.DayOverrides != null)
            {
                var dayOv = data.DayOverrides.FirstOrDefault(x => x.Date == today);
                if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                    effectiveWeekDay = dayOv.SourceWeekDay;
            }

            var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            var slots = config.TimeSlots;
            if (slots == null || slots.Count == 0) slots = DefaultTimeSlots.Create();

            for (int period = 1; period <= slots.Count; period++)
            {
                string slotName = slots[period - 1].Name;

                // 单节临时换课
                var ov = data.Overrides?.FirstOrDefault(
                    x => x.Date == today
                      && x.Period == period
                      && x.WeekDay == weekDayNum);
                if (ov != null && !string.IsNullOrWhiteSpace(ov.LessonName))
                {
                    result.Add($"{slotName}：{ov.LessonName}（{ov.Teacher}）");
                    continue;
                }

                // 天级换课
                var item = data.WeekBase?.FirstOrDefault(
                    x => x.WeekDay == effectiveWeekDay && x.Period == period);
                if (item != null && !string.IsNullOrWhiteSpace(item.LessonName))
                {
                    result.Add($"{slotName}：{item.LessonName}（{item.Teacher}）");
                }
            }
            return result;
        }
    }
}