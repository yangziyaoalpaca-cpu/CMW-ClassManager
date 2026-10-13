﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CMW
{
    public partial class SeatPage : UserControl
    {
        private SeatLayoutData _data = new SeatLayoutData();
        private SeatLayout? _current;
        private List<Student> _allStudents = new List<Student>();

        private const double CellWidth = 88;
        private const double CellHeight = 56;

        private static readonly Brush CellEmptyBg = Brushes.White;
        private static readonly Brush CellEmptyBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8DCE0"));
        private static readonly Brush CellFilledBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE"));
        private static readonly Brush CellFilledBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BAAF7"));
        private static readonly Brush CellFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"));
        private static readonly Brush CellFilledFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));
        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        public SeatPage()
        {
            InitializeComponent();
            Loaded += SeatPage_Loaded;
        }

        private void SeatPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadStudents();
                LoadData();

                if (_data.Layouts.Count == 0)
                {
                    _data.Layouts.Add(new SeatLayout
                    {
                        Name = "默认座位表",
                        Rows = 6,
                        Cols = 8
                    });
                    _data.CurrentLayoutId = _data.Layouts[0].Id;
                }

                _current = _data.Layouts.FirstOrDefault(x => x.Id == _data.CurrentLayoutId)
                           ?? _data.Layouts.FirstOrDefault();

                if (_current != null)
                    _data.CurrentLayoutId = _current.Id;

                RefreshLayoutCombo();
                RebuildGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("座位表初始化失败：\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════
        // 数据
        // ═══════════════════════════════════════

        private void LoadStudents()
        {
            var data = JsonHelper.Load<StudentListData>(AppPaths.Students)
                       ?? new StudentListData();
            _allStudents = (data.Students ?? new List<Student>())
                .OrderBy(s => s.Order)
                .ToList();
        }

        private void LoadData()
        {
            _data = JsonHelper.Load<SeatLayoutData>(AppPaths.Seats) ?? new SeatLayoutData();
        }

        private void SaveData()
        {
            try
            {
                if (_current != null)
                    _current.UpdatedAt = DateTime.Now;

                JsonHelper.Save(AppPaths.Seats, _data);
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        // ═══════════════════════════════════════
        // 方案管理
        // ═══════════════════════════════════════

        private void RefreshLayoutCombo()
        {
            cbLayout.SelectionChanged -= CbLayout_SelectionChanged;

            cbLayout.Items.Clear();
            foreach (var layout in _data.Layouts)
            {
                cbLayout.Items.Add(new ComboBoxItem
                {
                    Content = layout.Name,
                    Tag = layout.Id
                });
            }

            for (int i = 0; i < cbLayout.Items.Count; i++)
            {
                var item = cbLayout.Items[i] as ComboBoxItem;
                if (item != null && (string?)item.Tag == _data.CurrentLayoutId)
                {
                    cbLayout.SelectedIndex = i;
                    break;
                }
            }

            if (cbLayout.SelectedIndex < 0 && cbLayout.Items.Count > 0)
                cbLayout.SelectedIndex = 0;

            cbLayout.SelectionChanged += CbLayout_SelectionChanged;

            if (_current != null)
            {
                txtRows.Text = _current.Rows.ToString();
                txtCols.Text = _current.Cols.ToString();
            }
        }

        private void CbLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbLayout.SelectedItem is not ComboBoxItem item) return;
            if (item.Tag is not string id) return;

            var target = _data.Layouts.FirstOrDefault(x => x.Id == id);
            if (target == null) return;

            _current = target;
            _data.CurrentLayoutId = id;

            txtRows.Text = _current.Rows.ToString();
            txtCols.Text = _current.Cols.ToString();

            RebuildGrid();
            SaveData();
        }

        private void BtnNewLayout_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptInput("请输入方案名称（如：春季座位表）", "新建方案");
            if (string.IsNullOrWhiteSpace(name)) return;

            var newLayout = new SeatLayout
            {
                Name = name.Trim(),
                Rows = 6,
                Cols = 8
            };

            _data.Layouts.Add(newLayout);
            _data.CurrentLayoutId = newLayout.Id;
            _current = newLayout;

            SaveData();
            RefreshLayoutCombo();
            RebuildGrid();

            tbTip.Text = $"✅ 已新建方案「{newLayout.Name}」";
        }

        private void BtnDeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            if (_data.Layouts.Count <= 1)
            {
                MessageBox.Show("至少保留一个方案。", "提示");
                return;
            }

            if (MessageBox.Show(
                $"确定删除方案「{_current.Name}」？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _data.Layouts.Remove(_current);
            _data.CurrentLayoutId = _data.Layouts[0].Id;
            _current = _data.Layouts[0];

            SaveData();
            RefreshLayoutCombo();
            RebuildGrid();
        }

        private void BtnApplySize_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            if (!int.TryParse(txtRows.Text.Trim(), out int rows) || rows < 1 || rows > 20)
            {
                MessageBox.Show("行数必须是 1~20 之间的整数。", "提示");
                return;
            }
            if (!int.TryParse(txtCols.Text.Trim(), out int cols) || cols < 1 || cols > 20)
            {
                MessageBox.Show("列数必须是 1~20 之间的整数。", "提示");
                return;
            }

            _current.Rows = rows;
            _current.Cols = cols;

            _current.Assignments.RemoveAll(a => a.Row >= rows || a.Col >= cols);

            SaveData();
            RebuildGrid();

            tbTip.Text = $"✅ 行列已调整为 {rows} × {cols}";
        }

        // ═══════════════════════════════════════
        // 生成网格
        // ═══════════════════════════════════════

        private void RebuildGrid()
        {
            gridSeats.Children.Clear();
            gridSeats.RowDefinitions.Clear();
            gridSeats.ColumnDefinitions.Clear();

            if (_current == null) return;

            for (int r = 0; r < _current.Rows; r++)
                gridSeats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < _current.Cols; c++)
                gridSeats.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            for (int r = 0; r < _current.Rows; r++)
            {
                for (int c = 0; c < _current.Cols; c++)
                {
                    var cell = BuildCell(r, c);
                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    gridSeats.Children.Add(cell);
                }
            }
        }

        private Border BuildCell(int row, int col)
        {
            var assignment = _current?.Assignments.FirstOrDefault(a => a.Row == row && a.Col == col);
            string studentId = assignment?.StudentId ?? "";
            var student = string.IsNullOrEmpty(studentId)
                ? null
                : _allStudents.FirstOrDefault(s => s.Id == studentId);

            bool isFilled = student != null;

            var border = new Border
            {
                Width = CellWidth,
                Height = CellHeight,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Background = isFilled ? CellFilledBg : CellEmptyBg,
                BorderBrush = isFilled ? CellFilledBorder : CellEmptyBorder,
                BorderThickness = new Thickness(isFilled ? 1.5 : 1),
                Cursor = Cursors.Hand,
                Tag = new int[] { row, col },   // ★ 改用 int[] 更可靠
                Focusable = true
            };

            var text = new TextBlock
            {
                Text = isFilled ? student!.Name : "空座",
                FontFamily = UiFont,
                FontSize = 13,
                FontWeight = isFilled ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isFilled ? CellFilledFg : CellFg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 4, 0),
                IsHitTestVisible = false   // ★ 让 TextBlock 不拦截事件
            };

            border.Child = text;

            // ★ 同时订阅 PreviewMouseLeftButtonUp，最可靠
            border.PreviewMouseLeftButtonUp += Cell_MouseLeftButtonUp;

            return border;
        }

        private void Cell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not Border b) return;
                if (b.Tag is not int[] pos || pos.Length < 2) return;
                if (_current == null) return;

                e.Handled = true;

                int row = pos[0];
                int col = pos[1];

                var assignment = _current.Assignments.FirstOrDefault(a => a.Row == row && a.Col == col);
                string currentId = assignment?.StudentId ?? "";

                var owner = Window.GetWindow(this);

                var win = new SeatPickerWindow(_allStudents, currentId, row, col)
                {
                    Owner = owner
                };

                bool? result = win.ShowDialog();

                if (result != true) return;

                if (string.IsNullOrEmpty(win.SelectedStudentId))
                {
                    _current.Assignments.RemoveAll(a => a.Row == row && a.Col == col);
                }
                else
                {
                    var existing = _current.Assignments.FirstOrDefault(
                        a => a.StudentId == win.SelectedStudentId
                          && !(a.Row == row && a.Col == col));

                    if (existing != null)
                    {
                        var otherStudent = _allStudents.FirstOrDefault(s => s.Id == win.SelectedStudentId);
                        string otherName = otherStudent?.Name ?? win.SelectedStudentId;

                        if (MessageBox.Show(
                            $"「{otherName}」已安排在 第 {existing.Row + 1} 行 第 {existing.Col + 1} 列。\n\n" +
                            $"是否移动到当前位置？",
                            "学生已安排",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;

                        _current.Assignments.Remove(existing);
                    }

                    if (assignment == null)
                    {
                        _current.Assignments.Add(new SeatAssignment
                        {
                            Row = row,
                            Col = col,
                            StudentId = win.SelectedStudentId
                        });
                    }
                    else
                    {
                        assignment.StudentId = win.SelectedStudentId;
                    }
                }

                SaveData();
                RebuildGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("编辑座位失败：\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════
        // 清空 / 保存
        // ═══════════════════════════════════════

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            if (_current.Assignments.Count == 0)
            {
                tbTip.Text = "座位表已经是空的";
                return;
            }

            if (MessageBox.Show(
                "确定清空当前方案的所有座位安排？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _current.Assignments.Clear();
            SaveData();
            RebuildGrid();

            tbTip.Text = "✅ 已清空当前方案";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveData();
            tbTip.Text = "✅ 已保存";
        }

        // ═══════════════════════════════════════
        // 通用输入弹窗
        // ═══════════════════════════════════════

        private static string? PromptInput(string prompt, string title)
        {
            var win = new Window
            {
                Title = title,
                Width = 360,
                Height = 180,
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
            titleBar.Child = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontFamily = UiFont,
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
                FontFamily = UiFont,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 8)
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

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 2);
            content.Children.Add(btnPanel);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            win.Content = root;
            win.Loaded += (s, ee) => txt.Focus();

            return win.ShowDialog() == true ? result : null;
        }
    }
}