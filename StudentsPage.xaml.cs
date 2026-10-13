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
using System.Windows.Threading;
using Microsoft.Win32;

namespace CMW
{
    public partial class StudentsPage : UserControl
    {
        private List<Student> _students = new List<Student>();
        private StackPanel? _currentPanel;

        // ── 随机点名 ──
        private readonly Random _rng = new Random();
        private readonly DispatcherTimer _pickRollTimer;
        private readonly DispatcherTimer _pickStopTimer;
        private List<Student> _pickPool = new List<Student>();
        private readonly List<Student> _pickHistory = new List<Student>();
        private bool _isPicking;
        private Student? _pickResult;

        // ── 班级投票 ──
        private PollListData _pollData = new PollListData();
        private PollItem? _currentPoll;

        public StudentsPage()
        {
            InitializeComponent();

            _pickRollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _pickRollTimer.Tick += PickRollTimer_Tick;

            _pickStopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _pickStopTimer.Tick += PickStopTimer_Tick;

            Loaded += StudentsPage_Loaded;
            Unloaded += StudentsPage_Unloaded;
        }

        private void StudentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStudents();
            LoadPolls();
            RefreshList();
            RefreshPickerCounter();
            RefreshPollCombo();

            _currentPanel = PanelRoster;
        }

        private void StudentsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _pickRollTimer.Stop();
            _pickStopTimer.Stop();
        }

        // ═══════════════════════════════════════
        // 二级 tab
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabRoster" => PanelRoster,
                "TabPicker" => PanelPicker,
                "TabPoll" => PanelPoll,
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
        // 名单管理
        // ═══════════════════════════════════════

        private void LoadStudents()
        {
            var data = JsonHelper.Load<StudentListData>(AppPaths.Students)
                       ?? new StudentListData();
            _students = (data.Students ?? new List<Student>())
                .OrderBy(s => s.Order)
                .ToList();

            for (int i = 0; i < _students.Count; i++)
                _students[i].Order = i;
        }

        private void SaveStudents()
        {
            try
            {
                for (int i = 0; i < _students.Count; i++)
                    _students[i].Order = i;

                JsonHelper.Save(AppPaths.Students, new StudentListData
                {
                    Students = _students
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误");
            }
        }

        private void RefreshList()
        {
            icStudents.ItemsSource = null;
            icStudents.ItemsSource = _students;

            tbCount.Text = $"共 {_students.Count} 名学生";
            tbEmpty.Visibility = _students.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var win = BuildAddDialog();
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private Window BuildAddDialog()
        {
            var win = new Window
            {
                Title = "添加学生",
                Width = 480,
                Height = 460,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
                Text = "添加学生",
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
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tipText = new TextBlock
            {
                Text = "每行一个学生，格式：学号 姓名（学号可省略）\n例：1 张三\n或：张三",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(tipText, 0);
            contentGrid.Children.Add(tipText);

            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D4DA")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };

            var txtInput = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Height = double.NaN,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            txtInput.Style = Application.Current?.TryFindResource("MultilineTextBox") as Style;

            card.Child = txtInput;
            Grid.SetRow(card, 1);
            contentGrid.Children.Add(card);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            ApplyStyle(btnCancel, "PclButton");

            var btnOk = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 32
            };
            ApplyStyle(btnOk, "PclButtonPrimary");

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 2);
            contentGrid.Children.Add(btnPanel);

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            win.Content = root;

            btnCancel.Click += (s, ee) => win.Close();

            btnOk.Click += (s, ee) =>
            {
                string text = txtInput.Text ?? "";
                int added = ParseAndAddStudents(text);

                if (added == 0)
                {
                    MessageBox.Show("没有解析出有效的学生", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveStudents();
                RefreshList();
                RefreshPickerCounter();
                win.Close();
            };

            return win;
        }

        private int ParseAndAddStudents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            int added = 0;
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string studentId = "";
                string name = "";

                int splitIdx = -1;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ' ' || c == '\t' || c == ',' || c == '，')
                    {
                        splitIdx = i;
                        break;
                    }
                }

                if (splitIdx > 0 && splitIdx < line.Length - 1)
                {
                    studentId = line.Substring(0, splitIdx).Trim();
                    name = line.Substring(splitIdx + 1).Trim();
                }
                else
                {
                    name = line;
                }

                if (string.IsNullOrWhiteSpace(name)) continue;

                bool exists = _students.Any(s =>
                    s.Name == name && s.StudentId == studentId);
                if (exists) continue;

                _students.Add(new Student
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    StudentId = studentId,
                    Order = _students.Count
                });
                added++;
            }

            return added;
        }

        private void DeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Student s) return;

            _students.Remove(s);
            SaveStudents();
            RefreshList();
            RefreshPickerCounter();
        }

        private void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV 文件|*.csv|所有文件|*.*",
                Title = "导入名单"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                var sb = new StringBuilder();

                foreach (var raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("学号") && line.Contains("姓名")) continue;
                    if (line.ToLower().Contains("studentid")) continue;

                    sb.AppendLine(line);
                }

                int added = ParseAndAddStudents(sb.ToString());

                if (added == 0)
                {
                    MessageBox.Show("CSV 中没有有效数据", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveStudents();
                RefreshList();
                RefreshPickerCounter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_students.Count == 0)
            {
                MessageBox.Show("暂无可导出的学生", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = "班级名单.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("学号,姓名");

                foreach (var s in _students)
                {
                    string id = EscapeCsv(s.StudentId);
                    string name = EscapeCsv(s.Name);
                    sb.AppendLine($"{id},{name}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_students.Count == 0)
            {
                MessageBox.Show("名单已经是空的", "提示");
                return;
            }

            if (MessageBox.Show(
                $"确定清空全部 {_students.Count} 名学生？该操作不可恢复。\n\n建议先「导出 CSV」备份。",
                "警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _students.Clear();
            SaveStudents();
            RefreshList();
            RefreshPickerCounter();
        }

        // ═══════════════════════════════════════
        // 随机点名
        // ═══════════════════════════════════════

        private void RefreshPickerCounter()
        {
            if (_students.Count == 0)
            {
                tbPickerCounter.Text = "名单为空，请先到「名单管理」添加学生";
                tbPickerName.Text = "—";
                tbPickerSubtitle.Text = "";
                tbPickerStatus.Text = "—";
                BtnPickerStart.IsEnabled = false;
                return;
            }

            BtnPickerStart.IsEnabled = true;

            if (ChkNoRepeat.IsChecked == true)
            {
                tbPickerCounter.Text = $"剩余可抽：{_pickPool.Count} / {_students.Count} 人";
            }
            else
            {
                tbPickerCounter.Text = $"名单：{_students.Count} 人（可重复抽）";
            }
        }

        private void ResetPickPool()
        {
            _pickPool = new List<Student>(_students);
            _pickHistory.Clear();
            _pickResult = null;

            tbPickerName.Text = "—";
            tbPickerSubtitle.Text = "";
            tbPickerStatus.Text = "点击下方「开始」按钮";
            tbPickerHistory.Text = "（暂无）";
            BtnPickerStart.Content = "开始";

            RefreshPickerCounter();
        }

        private void BtnPickerStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isPicking)
            {
                StopPicking();
                return;
            }

            if (_students.Count == 0)
            {
                MessageBox.Show("请先到「名单管理」添加学生。", "提示");
                return;
            }

            if (ChkNoRepeat.IsChecked == true && _pickPool.Count == 0)
            {
                tbPickerStatus.Text = "本轮已抽完，点「重置本轮」再来";
                tbPickerName.Text = "🎉";
                tbPickerSubtitle.Text = "";
                BtnPickerStart.Content = "开始";
                return;
            }

            StartPicking();
        }

        private void StartPicking()
        {
            _isPicking = true;

            BtnPickerStart.Content = "停止";
            tbPickerStatus.Text = "抽选中...";
            tbPickerSubtitle.Text = "";

            _pickRollTimer.Start();
            _pickStopTimer.Start();
        }

        private List<Student> GetPickCandidates()
        {
            if (ChkNoRepeat.IsChecked == true)
                return _pickPool;
            return _students;
        }

        private void PickRollTimer_Tick(object? sender, EventArgs e)
        {
            var candidates = GetPickCandidates();
            if (candidates.Count == 0) return;

            var pick = candidates[_rng.Next(candidates.Count)];
            tbPickerName.Text = pick.Name;
        }

        private void PickStopTimer_Tick(object? sender, EventArgs e)
        {
            StopPicking();
        }

        private void StopPicking()
        {
            if (!_isPicking) return;

            _isPicking = false;
            _pickRollTimer.Stop();
            _pickStopTimer.Stop();

            var candidates = GetPickCandidates();
            if (candidates.Count == 0)
            {
                BtnPickerStart.Content = "开始";
                return;
            }

            var pick = candidates[_rng.Next(candidates.Count)];
            _pickResult = pick;

            tbPickerName.Text = pick.Name;
            tbPickerSubtitle.Text = string.IsNullOrWhiteSpace(pick.StudentId)
                ? ""
                : $"学号 {pick.StudentId}";
            tbPickerStatus.Text = "抽中！";

            BtnPickerStart.Content = "再抽一次";

            if (ChkNoRepeat.IsChecked == true)
            {
                _pickPool.Remove(pick);
            }
            _pickHistory.Add(pick);

            RefreshHistoryText();
            RefreshPickerCounter();
        }

        private void RefreshHistoryText()
        {
            if (_pickHistory.Count == 0)
            {
                tbPickerHistory.Text = "（暂无）";
                return;
            }

            var names = _pickHistory.Select(s => s.Name).ToList();
            tbPickerHistory.Text = string.Join("、", names);
        }

        private void BtnPickerReset_Click(object sender, RoutedEventArgs e)
        {
            if (_isPicking)
            {
                _pickRollTimer.Stop();
                _pickStopTimer.Stop();
                _isPicking = false;
            }

            ResetPickPool();
        }

        // ═══════════════════════════════════════
        // 班级投票
        // ═══════════════════════════════════════

        private void LoadPolls()
        {
            _pollData = JsonHelper.Load<PollListData>(AppPaths.Polls) ?? new PollListData();
            if (_pollData.Polls == null)
                _pollData.Polls = new List<PollItem>();
        }

        private void SavePolls()
        {
            try
            {
                JsonHelper.Save(AppPaths.Polls, _pollData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"投票保存失败：{ex.Message}", "错误");
            }
        }

        private void RefreshPollCombo()
        {
            cbPollSelect.SelectionChanged -= CbPollSelect_SelectionChanged;

            cbPollSelect.Items.Clear();
            foreach (var p in _pollData.Polls)
            {
                cbPollSelect.Items.Add(new ComboBoxItem
                {
                    Content = p.Title,
                    Tag = p.Id
                });
            }

            if (_currentPoll != null)
            {
                for (int i = 0; i < cbPollSelect.Items.Count; i++)
                {
                    var item = cbPollSelect.Items[i] as ComboBoxItem;
                    if (item != null && (string?)item.Tag == _currentPoll.Id)
                    {
                        cbPollSelect.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (cbPollSelect.Items.Count > 0)
            {
                cbPollSelect.SelectedIndex = 0;
            }

            cbPollSelect.SelectionChanged += CbPollSelect_SelectionChanged;

            if (_currentPoll == null && _pollData.Polls.Count > 0)
            {
                _currentPoll = _pollData.Polls[0];
            }

            if (_currentPoll == null)
            {
                tbPollTitle.Text = "（未选择投票）";
                tbPollTotal.Text = "";
                ugPollStudents.Children.Clear();
                tbPollEmpty.Visibility = Visibility.Visible;
            }
            else
            {
                RebuildPollGrid();
                tbPollEmpty.Visibility = Visibility.Collapsed;
            }
        }

        private void CbPollSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbPollSelect.SelectedItem is not ComboBoxItem item) return;
            if (item.Tag is not string id) return;

            var target = _pollData.Polls.FirstOrDefault(p => p.Id == id);
            if (target == null) return;

            _currentPoll = target;
            RebuildPollGrid();
        }

        private void RebuildPollGrid()
        {
            ugPollStudents.Children.Clear();

            if (_currentPoll == null) return;

            tbPollTitle.Text = $"主题：{_currentPoll.Title}";

            int votedCount = _currentPoll.GetVotedCount();
            int totalVotes = _currentPoll.GetTotalVotes();

            if (_students.Count == 0)
            {
                tbPollTotal.Text = "";
                tbPollEmpty.Text = "名单为空，请先到「名单管理」添加学生";
                tbPollEmpty.Visibility = Visibility.Visible;
                return;
            }

            tbPollTotal.Text = $"已投 {votedCount} / {_students.Count} 人 · 累计 {totalVotes} 票";
            tbPollEmpty.Visibility = Visibility.Collapsed;

            foreach (var s in _students)
            {
                var card = BuildPollCard(s);
                ugPollStudents.Children.Add(card);
            }
        }

        private Border BuildPollCard(Student student)
        {
            if (_currentPoll == null)
                return new Border();

            bool voted = _currentPoll.HasVoted(student.Id);
            int weight = _currentPoll.GetWeight(student.Id);

            var border = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                MinHeight = 64,
                Padding = new Thickness(8, 8, 4, 8),
                Cursor = Cursors.Hand,
                Tag = student
            };

            if (voted)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE"));
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));
                border.BorderThickness = new Thickness(1.5);
            }
            else
            {
                border.Background = Brushes.White;
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8DCE0"));
                border.BorderThickness = new Thickness(1);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };

            var tbName = new TextBlock
            {
                Text = student.Name,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = voted
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"))
            };
            stack.Children.Add(tbName);

            var tbStatus = new TextBlock
            {
                Text = weight == 1 ? (voted ? "已投" : "未投") : $"×{weight} 票{(voted ? " · 已投" : "")}",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = voted
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0"))
            };
            stack.Children.Add(tbStatus);

            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            var btnWeight = new Button
            {
                Content = "⚙",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Width = 26,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Tag = student,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                ToolTip = "修改此人的可投票数"
            };

            btnWeight.Template = CreateCircleHoverTemplate();
            btnWeight.Click += BtnPollWeight_Click;

            Grid.SetColumn(btnWeight, 1);
            grid.Children.Add(btnWeight);

            border.Child = grid;
            border.MouseLeftButtonUp += PollCard_MouseLeftButtonUp;

            return border;
        }

        private void PollCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not Student student) return;
            if (_currentPoll == null) return;

            bool wasVoted = _currentPoll.HasVoted(student.Id);
            _currentPoll.SetVoted(student.Id, !wasVoted);
            SavePolls();

            RebuildPollGrid();
            e.Handled = true;
        }

        private void BtnPollWeight_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Student student) return;
            if (_currentPoll == null) return;

            e.Handled = true;

            int current = _currentPoll.GetWeight(student.Id);
            var input = PromptInput(
                $"设置「{student.Name}」的可投票数（1~99）\n当前：{current}",
                "修改可投票数");

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!int.TryParse(input.Trim(), out int w) || w < 1 || w > 99)
            {
                MessageBox.Show("请输入 1~99 之间的整数。", "提示");
                return;
            }

            _currentPoll.SetWeight(student.Id, w);
            SavePolls();
            RebuildPollGrid();
        }

        private void BtnNewPoll_Click(object sender, RoutedEventArgs e)
        {
            var title = PromptInput("请输入投票主题，例如：是否同意周一提前到校", "新建投票");
            if (string.IsNullOrWhiteSpace(title)) return;

            var poll = new PollItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = title.Trim(),
                CreatedAt = DateTime.Now
            };

            _pollData.Polls.Add(poll);
            _currentPoll = poll;
            SavePolls();
            RefreshPollCombo();
        }

        private void BtnResetPollVotes_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPoll == null)
            {
                MessageBox.Show("请先选择一个投票。", "提示");
                return;
            }

            if (MessageBox.Show(
                $"确定重置「{_currentPoll.Title}」的所有投票状态？\n\n（已投/未投会全部清空，权重保留）",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _currentPoll.ResetAll();
            SavePolls();
            RebuildPollGrid();
        }

        private void BtnDeletePoll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPoll == null)
            {
                MessageBox.Show("请先选择一个投票。", "提示");
                return;
            }

            if (MessageBox.Show(
                $"确定删除投票「{_currentPoll.Title}」？该操作不可恢复。",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _pollData.Polls.Remove(_currentPoll);
            _currentPoll = _pollData.Polls.FirstOrDefault();
            SavePolls();
            RefreshPollCombo();
        }

        // ═══════════════════════════════════════
        // 通用输入弹窗（★ 修复：加 Owner + Topmost）
        // ═══════════════════════════════════════

        private string? PromptInput(string prompt, string title)
        {
            var owner = Window.GetWindow(this);

            var win = new Window
            {
                Title = title,
                Width = 440,
                Height = 200,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
                Owner = owner,
                Topmost = true,
                ShowActivated = true,
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
                Text = title,
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
                Text = prompt,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lbl, 0);
            content.Children.Add(lbl);

            var txt = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(txt, 1);
            content.Children.Add(txt);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current?.TryFindResource("PclButton") as Style
            };
            var btnOk = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 32,
                Style = Application.Current?.TryFindResource("PclButtonPrimary") as Style
            };

            string? result = null;
            btnCancel.Click += (s, ee) => { win.DialogResult = false; win.Close(); };
            btnOk.Click += (s, ee) =>
            {
                result = txt.Text;
                win.DialogResult = true;
                win.Close();
            };

            txt.KeyDown += (s, ee) =>
            {
                if (ee.Key == Key.Enter)
                {
                    result = txt.Text;
                    win.DialogResult = true;
                    win.Close();
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 2);
            content.Children.Add(btnPanel);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            win.Content = root;
            win.Loaded += (s, ee) =>
            {
                txt.Focus();
                win.Activate();
            };

            return win.ShowDialog() == true ? result : null;
        }

        // ═══════════════════════════════════════
        // 工具
        // ═══════════════════════════════════════

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

        private static ControlTemplate CreateCircleHoverTemplate()
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bg";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE")), "Bg"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }
    }
}