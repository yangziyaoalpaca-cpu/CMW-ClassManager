﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CMW
{
    public partial class CalendarPage : UserControl
    {
        private ClassEventListData _data = new ClassEventListData();

        // 当前显示的月份（月份第一天）
        private DateTime _viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        // 分类配色
        private static readonly Dictionary<string, (Brush Bg, Brush Fg)> CategoryColors =
            new Dictionary<string, (Brush, Brush)>
            {
                { "活动",   (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE")),
                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))) },
                { "考试",   (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")),
                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"))) },
                { "班会",   (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))) },
                { "运动会", (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"))) },
                { "其他",   (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")),
                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606060"))) }
            };

        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        public CalendarPage()
        {
            InitializeComponent();
            Loaded += CalendarPage_Loaded;
        }

        private void CalendarPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            RebuildCalendar();
        }

        // ═══════════════════════════════════════
        // 数据
        // ═══════════════════════════════════════

        private void LoadData()
        {
            _data = JsonHelper.Load<ClassEventListData>(AppPaths.Events)
                    ?? new ClassEventListData();
        }

        private void SaveData()
        {
            try
            {
                JsonHelper.Save(AppPaths.Events, _data);
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        // ═══════════════════════════════════════
        // 月份切换
        // ═══════════════════════════════════════

        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _viewMonth = _viewMonth.AddMonths(-1);
            RebuildCalendar();
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _viewMonth = _viewMonth.AddMonths(1);
            RebuildCalendar();
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RebuildCalendar();
        }

        // ═══════════════════════════════════════
        // 日历网格生成
        // ═══════════════════════════════════════

        private void RebuildCalendar()
        {
            tbMonthTitle.Text = _viewMonth.ToString("yyyy 年 M 月");

            gridCalendar.Children.Clear();
            gridCalendar.RowDefinitions.Clear();
            gridCalendar.ColumnDefinitions.Clear();

            for (int i = 0; i < 7; i++)
                gridCalendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 6; i++)
                gridCalendar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 本月第一天是周几（周一 = 0）
            int firstDayOffset = ((int)_viewMonth.DayOfWeek + 6) % 7;

            // 本月总天数
            int daysInMonth = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);

            // 起始日期（可能包含上个月）
            DateTime startDate = _viewMonth.AddDays(-firstDayOffset);

            DateTime today = DateTime.Today.Date;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    int cellIndex = r * 7 + c;
                    DateTime cellDate = startDate.AddDays(cellIndex);

                    var cell = BuildDateCell(cellDate, today);
                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    gridCalendar.Children.Add(cell);
                }
            }
        }

        private Border BuildDateCell(DateTime date, DateTime today)
        {
            bool isCurrentMonth = date.Month == _viewMonth.Month && date.Year == _viewMonth.Year;
            bool isToday = date.Date == today;

            var events = _data.Events
                .Where(e => e.IsOnDate(date))
                .OrderBy(e => e.Date)
                .ToList();

            var border = new Border
            {
                Background = isCurrentMonth ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F8F8")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8DCE0")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                Padding = new Thickness(6),
                Cursor = Cursors.Hand,
                Tag = date,
                MinHeight = 90
            };

            if (isToday)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));
                border.BorderThickness = new Thickness(2);
            }

            var stack = new StackPanel();

            // 日期数字
            var dateRow = new StackPanel { Orientation = Orientation.Horizontal };

            dateRow.Children.Add(new TextBlock
            {
                Text = date.Day.ToString(),
                FontFamily = UiFont,
                FontSize = 14,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isToday
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"))
                    : (isCurrentMonth
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0"))),
                VerticalAlignment = VerticalAlignment.Center
            });

            if (isToday)
            {
                dateRow.Children.Add(new TextBlock
                {
                    Text = " 今天",
                    FontFamily = UiFont,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0")),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 0, 0)
                });
            }

            stack.Children.Add(dateRow);

            // 活动列表（最多显示 2 个）
            int showCount = Math.Min(events.Count, 2);
            for (int i = 0; i < showCount; i++)
            {
                var ev = events[i];
                var colors = CategoryColors.TryGetValue(ev.Category, out var c)
                    ? c
                    : CategoryColors["其他"];

                var tag = new Border
                {
                    Background = colors.Bg,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 3, 0, 0)
                };

                var tb = new TextBlock
                {
                    Text = ev.Title,
                    FontFamily = UiFont,
                    FontSize = 11,
                    Foreground = colors.Fg,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                tag.Child = tb;
                stack.Children.Add(tag);
            }

            if (events.Count > 2)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"+{events.Count - 2} 项",
                    FontFamily = UiFont,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            border.Child = stack;
            border.MouseLeftButtonUp += Cell_MouseLeftButtonUp;

            return border;
        }

        private void Cell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not DateTime date) return;

            e.Handled = true;

            var win = new DayEventWindow(date, _data.Events.Where(ev => ev.IsOnDate(date)).ToList())
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                // 用户可能在弹窗里新增/删除了活动
                LoadData();
                SaveData();
                RebuildCalendar();

                tbTip.Text = "✅ 已更新";
            }
        }

        // ═══════════════════════════════════════
        // 新增活动（快捷：默认今天）
        // ═══════════════════════════════════════

        private void BtnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            var date = DateTime.Today;

            var win = new DayEventWindow(date, _data.Events.Where(ev => ev.IsOnDate(date)).ToList())
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                LoadData();
                SaveData();
                RebuildCalendar();

                tbTip.Text = "✅ 已添加";
            }
        }
    }
}