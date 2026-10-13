using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CMW
{
    public partial class HomeworkPage : UserControl
    {
        private List<HomeworkItem> _homeworkList;
        private StackPanel? _currentPanel;

        private List<string> _remindTimes = new List<string>();
        private bool _remindEnabled = false;

        public HomeworkPage()
        {
            InitializeComponent();
            _homeworkList = new List<HomeworkItem>();
            Loaded += HomeworkPage_Loaded;

            sliderScrollSpeed.ValueChanged += (s, e) =>
            {
                tbScrollSpeed.Text = $"当前：{sliderScrollSpeed.Value:F0} px/秒";
            };
            sliderRemindDuration.ValueChanged += (s, e) =>
            {
                tbRemindDuration.Text = $"当前：{sliderRemindDuration.Value:F0} 秒";
            };
        }

        private void HomeworkPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHomeworkFromJson();
            RefreshHomeworkList();
            RefreshTodayHomework();
            LoadRemindConfig();

            if (dpStartDate.SelectedDate == null)
                dpStartDate.SelectedDate = DateTime.Today;
            if (dpEndDate.SelectedDate == null)
                dpEndDate.SelectedDate = DateTime.Today;

            PanelAdd.Visibility = Visibility.Visible;
            PanelList.Visibility = Visibility.Collapsed;
            PanelToday.Visibility = Visibility.Collapsed;
            PanelRemindConfig.Visibility = Visibility.Collapsed;

            _currentPanel = PanelAdd;

            TabAdd.IsChecked = true;
        }

        // ═══════════════════════════════════════
        // Tab 切换
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabAdd" => PanelAdd,
                "TabList" => PanelList,
                "TabToday" => PanelToday,
                "TabRemindConfig" => PanelRemindConfig,
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
        // 时间工具
        // ═══════════════════════════════════════

        private static bool TryBuildDateTime(DatePicker dp, TextBox hourBox, TextBox minBox, out DateTime result)
        {
            result = DateTime.MinValue;
            if (dp.SelectedDate == null) return false;

            if (!int.TryParse(hourBox.Text?.Trim(), out int h)) return false;
            if (!int.TryParse(minBox.Text?.Trim(), out int m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;

            result = dp.SelectedDate.Value.Date.AddHours(h).AddMinutes(m);
            return true;
        }

        private static string FormatDateTime(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm");
        }

        // ═══════════════════════════════════════
        // 新增作业
        // ═══════════════════════════════════════

        private void TxtHomeworkName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddHomework_Click(sender, e);
        }

        private void BtnAddHomework_Click(object sender, RoutedEventArgs e)
        {
            string name = txtHomeworkName.Text.Trim();
            string subject = txtSubject.Text.Trim();
            string priority = (cbPriority.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
            bool needSubmit = toggleNeedSubmit.IsChecked == true;

            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("请输入作业名称。"); return; }

            if (!TryBuildDateTime(dpStartDate, txtStartHour, txtStartMin, out DateTime start))
            {
                MessageBox.Show("开始时间无效（请检查日期和时分）。"); return;
            }

            if (!TryBuildDateTime(dpEndDate, txtEndHour, txtEndMin, out DateTime end))
            {
                MessageBox.Show("截止时间无效（请检查日期和时分）。"); return;
            }

            if (end < start)
            {
                MessageBox.Show("截止时间不能早于开始时间。");
                return;
            }

            _homeworkList.Add(new HomeworkItem
            {
                Id = Guid.NewGuid().ToString(),
                HomeworkName = name,
                Subject = subject,
                StartDate = start,
                EndDate = end,
                Priority = priority,
                NeedSubmit = needSubmit,
                IsCompleted = false,
                SubmittedStudentIds = new List<string>(),
                IsRollCalled = false
            });

            txtHomeworkName.Text = string.Empty;
            txtSubject.Text = string.Empty;

            dpStartDate.SelectedDate = DateTime.Today;
            txtStartHour.Text = "08";
            txtStartMin.Text = "00";

            dpEndDate.SelectedDate = DateTime.Today;
            txtEndHour.Text = "22";
            txtEndMin.Text = "00";

            cbPriority.SelectedIndex = 0;
            toggleNeedSubmit.IsChecked = true;

            SaveHomeworkToJson();
            RefreshHomeworkList();
            RefreshTodayHomework();

            TabList.IsChecked = true;
        }

        // ═══════════════════════════════════════
        // 从模板添加
        // ═══════════════════════════════════════

        private void BtnFromTemplate_Click(object sender, RoutedEventArgs e)
        {
            var win = new HomeworkTemplateWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() != true) return;
            if (win.SelectedTemplate == null) return;

            var tpl = win.SelectedTemplate;

            txtHomeworkName.Text = tpl.Name;
            txtSubject.Text = tpl.Subject ?? "";

            for (int i = 0; i < cbPriority.Items.Count; i++)
            {
                var item = cbPriority.Items[i] as ComboBoxItem;
                if (item != null && (string?)item.Tag == tpl.Priority)
                {
                    cbPriority.SelectedIndex = i;
                    break;
                }
            }

            var today = DateTime.Today;
            dpStartDate.SelectedDate = today;
            txtStartHour.Text = "08";
            txtStartMin.Text = "00";

            dpEndDate.SelectedDate = today.AddDays(Math.Max(1, tpl.DurationDays) - 1);
            txtEndHour.Text = "22";
            txtEndMin.Text = "00";

            txtHomeworkName.Focus();
            txtHomeworkName.CaretIndex = txtHomeworkName.Text.Length;
        }

        // ═══════════════════════════════════════
        // ★ 28.0.0：编辑作业
        // ═══════════════════════════════════════

        private void EditHomework_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not HomeworkItem item) return;

            var win = BuildEditDialog(item);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private Window BuildEditDialog(HomeworkItem item)
        {
            var win = new Window
            {
                Title = "编辑作业",
                Width = 520,
                Height = 520,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EBF0"))
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 顶栏
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
                Text = "编辑作业",
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
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, ee) => win.Close();
            Grid.SetColumn(closeBtn, 1);
            titleGrid.Children.Add(closeBtn);

            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // 内容
            var contentCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D4DA")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(16),
                Padding = new Thickness(20)
            };

            var form = new StackPanel();

            form.Children.Add(new TextBlock
            {
                Text = "作业名称",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txtName = new TextBox { Text = item.HomeworkName, Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(txtName);

            form.Children.Add(new TextBlock
            {
                Text = "科目",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txtSubj = new TextBox { Text = item.Subject, Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(txtSubj);

            form.Children.Add(new TextBlock
            {
                Text = "开始时间（日期 + 时:分）",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var startRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            startRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var dpStart = new DatePicker { SelectedDate = item.StartDate.Date };
            var txtSH = new TextBox { Text = item.StartDate.Hour.ToString("D2"), MaxLength = 2, TextAlignment = TextAlignment.Center };
            var txtSM = new TextBox { Text = item.StartDate.Minute.ToString("D2"), MaxLength = 2, TextAlignment = TextAlignment.Center };
            Grid.SetColumn(dpStart, 0); startRow.Children.Add(dpStart);
            var lbSH = new TextBlock { Text = " 时 ", VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13 };
            Grid.SetColumn(lbSH, 1); startRow.Children.Add(lbSH);
            Grid.SetColumn(txtSH, 2); startRow.Children.Add(txtSH);
            var lbColon1 = new TextBlock { Text = " : ", VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13 };
            Grid.SetColumn(lbColon1, 3); startRow.Children.Add(lbColon1);
            Grid.SetColumn(txtSM, 4); startRow.Children.Add(txtSM);
            form.Children.Add(startRow);

            form.Children.Add(new TextBlock
            {
                Text = "截止时间（日期 + 时:分）",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var endRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            endRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var dpEnd = new DatePicker { SelectedDate = item.EndDate.Date };
            var txtEH = new TextBox { Text = item.EndDate.Hour.ToString("D2"), MaxLength = 2, TextAlignment = TextAlignment.Center };
            var txtEM = new TextBox { Text = item.EndDate.Minute.ToString("D2"), MaxLength = 2, TextAlignment = TextAlignment.Center };
            Grid.SetColumn(dpEnd, 0); endRow.Children.Add(dpEnd);
            var lbEH = new TextBlock { Text = " 时 ", VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13 };
            Grid.SetColumn(lbEH, 1); endRow.Children.Add(lbEH);
            Grid.SetColumn(txtEH, 2); endRow.Children.Add(txtEH);
            var lbColon2 = new TextBlock { Text = " : ", VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13 };
            Grid.SetColumn(lbColon2, 3); endRow.Children.Add(lbColon2);
            Grid.SetColumn(txtEM, 4); endRow.Children.Add(txtEM);
            form.Children.Add(endRow);

            form.Children.Add(new TextBlock
            {
                Text = "优先级",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var cbPrio = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
            cbPrio.Items.Add(new ComboBoxItem { Content = "普通", Tag = "Normal" });
            cbPrio.Items.Add(new ComboBoxItem { Content = "紧急", Tag = "Urgent" });
            cbPrio.Items.Add(new ComboBoxItem { Content = "非常紧急", Tag = "VeryUrgent" });
            for (int i = 0; i < cbPrio.Items.Count; i++)
            {
                var ci = cbPrio.Items[i] as ComboBoxItem;
                if (ci != null && (string?)ci.Tag == item.Priority) { cbPrio.SelectedIndex = i; break; }
            }
            if (cbPrio.SelectedIndex < 0) cbPrio.SelectedIndex = 0;
            form.Children.Add(cbPrio);

            var needRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            needRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            needRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbNeed = new TextBlock
            {
                Text = "需要交（关闭后不参与催收）",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbNeed, 0); needRow.Children.Add(lbNeed);

            var chkNeed = new CheckBox
            {
                IsChecked = item.NeedSubmit,
                Style = Application.Current?.TryFindResource("IosToggle") as Style,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chkNeed, 1); needRow.Children.Add(chkNeed);
            form.Children.Add(needRow);

            contentCard.Child = form;
            Grid.SetRow(contentCard, 1);
            root.Children.Add(contentCard);

            // 底部按钮
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            if (Application.Current?.TryFindResource("PclButton") is Style s1) btnCancel.Style = s1;

            var btnOk = new Button
            {
                Content = "保存",
                Width = 80,
                Height = 32
            };
            if (Application.Current?.TryFindResource("PclButtonPrimary") is Style s2) btnOk.Style = s2;

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);

            var rootGrid = new Grid();
            rootGrid.Children.Add(root);
            var bottomHost = new Grid
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };
            bottomHost.Children.Add(btnPanel);
            rootGrid.Children.Add(bottomHost);

            win.Content = rootGrid;

            btnCancel.Click += (s, ee) => win.Close();

            btnOk.Click += (s, ee) =>
            {
                string n = (txtName.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(n)) { MessageBox.Show("作业名称不能为空。"); return; }

                if (!TryBuildDateTime(dpStart, txtSH, txtSM, out DateTime ns))
                {
                    MessageBox.Show("开始时间无效。"); return;
                }
                if (!TryBuildDateTime(dpEnd, txtEH, txtEM, out DateTime ne))
                {
                    MessageBox.Show("截止时间无效。"); return;
                }
                if (ne < ns)
                {
                    MessageBox.Show("截止时间不能早于开始时间。"); return;
                }

                item.HomeworkName = n;
                item.Subject = (txtSubj.Text ?? "").Trim();
                item.StartDate = ns;
                item.EndDate = ne;
                item.Priority = (cbPrio.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";
                item.NeedSubmit = chkNeed.IsChecked == true;

                SaveHomeworkToJson();
                RefreshHomeworkList();
                RefreshTodayHomework();

                win.Close();
            };

            return win;
        }

        // ═══════════════════════════════════════
        // 点名
        // ═══════════════════════════════════════

        private void RollCall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not HomeworkItem item) return;

            var win = new RollCallWindow(item) { Owner = Window.GetWindow(this) };

            if (win.ShowDialog() == true)
            {
                item.SubmittedStudentIds = win.SubmittedIds ?? new List<string>();
                item.IsRollCalled = true;
                SaveHomeworkToJson();
                RefreshHomeworkList();
            }
        }

        private void DeleteHomework_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is HomeworkItem item)
            {
                RecycleBinService.Add(RecycleBinService.TypeHomework, item.HomeworkName, item);
                _homeworkList.Remove(item);
                SaveHomeworkToJson();
                RefreshHomeworkList();
                RefreshTodayHomework();
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is HomeworkItem item)
            {
                item.IsCompleted = true;
                SaveHomeworkToJson();
                RefreshTodayHomework();
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is HomeworkItem item)
            {
                item.IsCompleted = false;
                SaveHomeworkToJson();
                RefreshTodayHomework();
            }
        }

        private void BtnViewToday_Click(object sender, RoutedEventArgs e)
        {
            var todayList = GetTodayHomework();
            if (todayList.Count == 0) { MessageBox.Show("今天没有作业。"); return; }

            string text = string.Join("\n",
                todayList.Select(x => $"[{x.PriorityText}] {x.HomeworkName}｜{x.Subject}｜{x.DateRangeText}"));
            MessageBox.Show(text, "今日作业");
        }

        private void BtnCopyToday_Click(object sender, RoutedEventArgs e)
        {
            var todayList = GetTodayHomework();

            if (todayList.Count == 0)
            {
                tbCopyTip.Text = "⚠️ 今天没有作业可复制";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"今日作业（{DateTime.Now:MM-dd}）：");

            int idx = 1;
            foreach (var hw in todayList)
            {
                string line = $"{idx}. {hw.HomeworkName}";
                if (!string.IsNullOrWhiteSpace(hw.Subject))
                    line += $"（{hw.Subject}）";
                if (hw.Priority == "Urgent" || hw.Priority == "VeryUrgent")
                    line += $"[{hw.PriorityText}]";
                line += $" 截止 {hw.DateRangeText}";
                sb.AppendLine(line);
                idx++;
            }

            if (TrySetClipboardWithRetry(sb.ToString().TrimEnd()))
                tbCopyTip.Text = $"✅ 已复制 {todayList.Count} 条作业到剪贴板";
            else
                tbCopyTip.Text = "⚠️ 复制失败（剪贴板被占用）";
        }

        private static bool TrySetClipboardWithRetry(string text)
        {
            for (int i = 0; i < 10; i++)
            {
                try { Clipboard.SetDataObject(text, true); return true; }
                catch { System.Threading.Thread.Sleep(100); }
            }
            return false;
        }

        private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            int count = _homeworkList.RemoveAll(x => x.IsCompleted);
            if (count > 0)
            {
                SaveHomeworkToJson();
                RefreshHomeworkList();
                RefreshTodayHomework();
            }
            else MessageBox.Show("没有已完成的作业。");
        }

        private void RefreshHomeworkList()
        {
            icHomeworkList.ItemsSource = null;
            icHomeworkList.ItemsSource = _homeworkList;

            tbAllEmpty.Visibility = _homeworkList.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshTodayHomework()
        {
            var todayList = GetTodayHomework();
            if (todayList.Count == 0)
            {
                tbTodayHomework.Text = "今天暂无作业。";
                return;
            }

            tbTodayHomework.Text = string.Join("；",
                todayList.Select(x => $"{x.HomeworkName}（{x.Subject}）{x.DateRangeText}"));
        }

        private void LoadRemindConfig()
        {
            var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

            _remindEnabled = cfg.HomeworkRemindEnabled;

            var raw = cfg.HomeworkRemindTimes ?? new List<string>();
            _remindTimes = raw
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => TimeSpan.TryParse(t, out _))
                .Distinct()
                .OrderBy(t => TimeSpan.Parse(t))
                .ToList();

            toggleRemindEnabled.IsChecked = _remindEnabled;
            RefreshRemindTimes();

            double speed = cfg.HomeworkRemindScrollSpeed;
            if (speed < 20) speed = 20;
            if (speed > 150) speed = 150;
            sliderScrollSpeed.Value = speed;
            tbScrollSpeed.Text = $"当前：{speed:F0} px/秒";

            int dur = cfg.HomeworkRemindDurationSeconds;
            if (dur < 30) dur = 30;
            if (dur > 300) dur = 300;
            sliderRemindDuration.Value = dur;
            tbRemindDuration.Text = $"当前：{dur} 秒";
        }

        private void RefreshRemindTimes()
        {
            icRemindTimes.ItemsSource = null;
            icRemindTimes.ItemsSource = _remindTimes;
        }

        private void TxtNewRemindTime_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddRemindTime_Click(sender, e);
        }

        private void BtnAddRemindTime_Click(object sender, RoutedEventArgs e)
        {
            string t = (txtNewRemindTime.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(t))
            {
                tbRemindConfigTip.Text = "⚠️ 请输入时间，格式 HH:mm";
                return;
            }

            if (!TimeSpan.TryParse(t, out _))
            {
                tbRemindConfigTip.Text = $"⚠️ 「{t}」不是合法时间，格式 HH:mm";
                return;
            }

            if (_remindTimes.Contains(t))
            {
                tbRemindConfigTip.Text = $"⚠️ {t} 已存在";
                return;
            }

            _remindTimes.Add(t);

            _remindTimes = _remindTimes
                .OrderBy(x => TimeSpan.Parse(x))
                .ToList();

            txtNewRemindTime.Text = "";
            RefreshRemindTimes();
            tbRemindConfigTip.Text = $"✅ 已添加 {t}（记得点「保存设置」）";
        }

        private void BtnDeleteRemindTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string t) return;

            _remindTimes.Remove(t);
            RefreshRemindTimes();
            tbRemindConfigTip.Text = $"✅ 已删除 {t}（记得点「保存设置」）";
        }

        private void BtnClearAllRemindTimes_Click(object sender, RoutedEventArgs e)
        {
            if (_remindTimes.Count == 0)
            {
                tbRemindConfigTip.Text = "已经没有了";
                return;
            }

            if (MessageBox.Show(
                $"确定清空全部 {_remindTimes.Count} 个催收时间点？\n\n清空后记得点「保存设置」才会生效。",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _remindTimes.Clear();
            RefreshRemindTimes();
            tbRemindConfigTip.Text = "✅ 已清空全部时间点（记得点「保存设置」）";
        }

        private void BtnSaveRemindConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                cfg.HomeworkRemindEnabled = toggleRemindEnabled.IsChecked == true;
                cfg.HomeworkRemindTimes = new List<string>(_remindTimes);
                cfg.HomeworkRemindScrollSpeed = (int)sliderScrollSpeed.Value;
                cfg.HomeworkRemindDurationSeconds = (int)sliderRemindDuration.Value;

                JsonHelper.Save(AppPaths.Config, cfg);

                App.NotifyRemindConfigChanged();

                tbRemindConfigTip.Text = $"✅ 已保存（{_remindTimes.Count} 个催收时间点）";
            }
            catch (Exception ex)
            {
                tbRemindConfigTip.Text = $"❌ 保存失败：{ex.Message}";
            }
        }

        public List<HomeworkItem> GetTodayHomework()
        {
            var today = DateTime.Today;
            return _homeworkList
                .Where(x => x.IsActiveOn(today))
                .OrderByDescending(x => x.PriorityOrder)
                .ThenBy(x => x.EndDate)
                .ToList();
        }

        private void LoadHomeworkFromJson()
        {
            _homeworkList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                ?? new List<HomeworkItem>();

            foreach (var h in _homeworkList)
            {
                if (h.SubmittedStudentIds == null)
                    h.SubmittedStudentIds = new List<string>();
            }
        }

        private void SaveHomeworkToJson()
        {
            try { JsonHelper.Save(AppPaths.Homework, _homeworkList); }
            catch { MessageBox.Show("作业保存失败。"); }
        }

        public static List<HomeworkItem> GetTodayHomeworkForSummary()
        {
            var allList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                ?? new List<HomeworkItem>();
            var today = DateTime.Today;
            return allList
                .Where(x => x.IsActiveOn(today))
                .OrderByDescending(x => x.PriorityOrder)
                .ThenBy(x => x.EndDate)
                .ToList();
        }

        public static int GetTodayCompletedCount()
        {
            return GetTodayHomeworkForSummary().Count(x => x.IsCompleted);
        }
    }
}