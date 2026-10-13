﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CMW
{
    public partial class StatisticsPage : UserControl
    {
        private bool _initialized;
        private string _currentTab = "Overview";

        public StatisticsPage()
        {
            InitializeComponent();
            Loaded += StatisticsPage_Loaded;
        }

        private void StatisticsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                _initialized = true;
                TabOverview.IsChecked = true;
            }

            RefreshAll();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        // ═══════════════════════════════════════
        // Tab
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            if (sender is not RadioButton rb) return;

            PanelOverview.Visibility = Visibility.Collapsed;
            PanelHomework.Visibility = Visibility.Collapsed;
            PanelTodo.Visibility = Visibility.Collapsed;
            PanelDuty.Visibility = Visibility.Collapsed;

            switch (rb.Name)
            {
                case "TabOverview":
                    PanelOverview.Visibility = Visibility.Visible;
                    _currentTab = "Overview";
                    break;
                case "TabHomework":
                    PanelHomework.Visibility = Visibility.Visible;
                    _currentTab = "Homework";
                    DrawHomework();
                    break;
                case "TabTodo":
                    PanelTodo.Visibility = Visibility.Visible;
                    _currentTab = "Todo";
                    DrawTodo();
                    break;
                case "TabDuty":
                    PanelDuty.Visibility = Visibility.Visible;
                    _currentTab = "Duty";
                    DrawDuty();
                    break;
            }
        }

        private void RefreshAll()
        {
            RefreshOverview();
            if (_currentTab == "Homework") DrawHomework();
            else if (_currentTab == "Todo") DrawTodo();
            else if (_currentTab == "Duty") DrawDuty();
        }

        // ═══════════════════════════════════════
        // 概览
        // ═══════════════════════════════════════

        private void RefreshOverview()
        {
            try
            {
                var hwList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();
                var tdList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();
                var examData = JsonHelper.Load<ExamListData>(AppPaths.Exams);
                var today = DateTime.Today;

                // 今日作业数
                var todayHw = hwList.Where(h => h.IsActiveOn(today)).ToList();
                tbTodayHw.Text = todayHw.Count.ToString();

                // 今日作业完成率
                if (todayHw.Count > 0)
                {
                    int done = todayHw.Count(h => h.IsCompleted);
                    double rate = 100.0 * done / todayHw.Count;
                    tbHwRate.Text = $"{rate:F0}%";
                }
                else tbHwRate.Text = "—";

                // 今日待办
                int todayTodo = tdList.Count(t => t.Deadline.HasValue
                    && t.Deadline.Value.Date == today);
                tbTodayTodo.Text = todayTodo.ToString();

                // 今日值日
                string dutyName = DutyPage.GetTodayDutyGroupName();
                tbTodayDuty.Text = string.IsNullOrWhiteSpace(dutyName) ? "未安排" : dutyName;

                // 累计
                tbTotalHw.Text = hwList.Count.ToString();
                tbTotalTodo.Text = tdList.Count.ToString();
                tbTotalExam.Text = (examData?.Exams?.Count ?? 0).ToString();
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // 作业趋势（近 14 天）
        // ═══════════════════════════════════════

        private void DrawHomework()
        {
            cvHomework.Children.Clear();

            try
            {
                var hwList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();

                int days = 14;
                var startDate = DateTime.Today.AddDays(-(days - 1));

                // 每天新增作业数 / 完成作业数
                var addedData = new int[days];
                var doneData = new int[days];

                for (int i = 0; i < days; i++)
                {
                    var d = startDate.AddDays(i).Date;
                    addedData[i] = hwList.Count(h => h.StartDate.Date == d);
                    doneData[i] = hwList.Count(h => h.IsCompleted
                        && h.EndDate.Date == d);   // 用截止日近似"完成日"
                }

                int maxVal = Math.Max(addedData.Max(), doneData.Max());
                if (maxVal == 0)
                {
                    tbHomeworkEmpty.Visibility = Visibility.Visible;
                    return;
                }

                tbHomeworkEmpty.Visibility = Visibility.Collapsed;
                DrawLineChart(cvHomework, addedData, doneData, days,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0")),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                    maxVal);
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // 待办趋势（近 14 天）
        // ═══════════════════════════════════════

        private void DrawTodo()
        {
            cvTodo.Children.Clear();

            try
            {
                var tdList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();

                int days = 14;
                var startDate = DateTime.Today.AddDays(-(days - 1));

                var addedData = new int[days];
                var doneData = new int[days];

                for (int i = 0; i < days; i++)
                {
                    var d = startDate.AddDays(i).Date;
                    addedData[i] = tdList.Count(t => t.Deadline.HasValue
                        && t.Deadline.Value.Date == d);
                    doneData[i] = tdList.Count(t => t.Deadline.HasValue
                        && t.Deadline.Value.Date == d
                        && t.IsCompleted);
                }

                int maxVal = Math.Max(addedData.Max(), doneData.Max());
                if (maxVal == 0)
                {
                    tbTodoEmpty.Visibility = Visibility.Visible;
                    return;
                }

                tbTodoEmpty.Visibility = Visibility.Collapsed;
                DrawLineChart(cvTodo, addedData, doneData, days,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0")),
                    maxVal);
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // 值日统计（柱状图）
        // ═══════════════════════════════════════

        private void DrawDuty()
        {
            cvDuty.Children.Clear();

            try
            {
                var data = JsonHelper.Load<DutySaveModel>(AppPaths.Duty);
                var groups = data?.DutyGroups ?? new List<DutyGroup>();
                if (groups.Count == 0)
                {
                    tbDutyEmpty.Visibility = Visibility.Visible;
                    return;
                }

                // 由于当前系统不记录历史值日次数，这里用"是否当前"的示意
                // 显示每个组是否存在（所有组都 +1），以及当前值日组突出显示
                int n = groups.Count;
                string currentDuty = data?.TodayDutyGroupName ?? "";

                var counts = new int[n];
                for (int i = 0; i < n; i++)
                {
                    counts[i] = groups[i].GroupName == currentDuty ? 1 : 0;
                }

                // 若所有都是 0，改为全部显示 1（表示"存在"）
                if (counts.Sum() == 0)
                {
                    for (int i = 0; i < n; i++) counts[i] = 1;
                }

                tbDutyEmpty.Visibility = Visibility.Collapsed;
                DrawBarChart(cvDuty, counts,
                    groups.Select(g => g.GroupName).ToList(),
                    currentDuty);
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // 折线图
        // ═══════════════════════════════════════

        private void DrawLineChart(Canvas canvas,
            int[] data1, int[] data2, int days,
            Brush brush1, Brush brush2, int maxVal)
        {
            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 600;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 260;
            if (w < 100) w = 600;
            if (h < 100) h = 260;

            canvas.Width = w;
            canvas.Height = h;

            double padLeft = 40;
            double padRight = 20;
            double padTop = 20;
            double padBottom = 40;

            double plotW = w - padLeft - padRight;
            double plotH = h - padTop - padBottom;
            if (plotW <= 20 || plotH <= 20) return;

            // Y 轴刻度
            int gridLines = 4;
            for (int i = 0; i <= gridLines; i++)
            {
                double yVal = maxVal * i / gridLines;
                double yPos = padTop + plotH - plotH * i / gridLines;

                var line = new Line
                {
                    X1 = padLeft,
                    Y1 = yPos,
                    X2 = padLeft + plotW,
                    Y2 = yPos,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);

                var tb = new TextBlock
                {
                    Text = yVal.ToString("F0"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                Canvas.SetLeft(tb, 8);
                Canvas.SetTop(tb, yPos - 8);
                canvas.Children.Add(tb);
            }

            // X 轴日期
            var startDate = DateTime.Today.AddDays(-(days - 1));
            int step = 1;   // 显示间隔
            for (int i = 0; i < days; i += step)
            {
                double xPos = days <= 1 ? padLeft + plotW / 2
                    : padLeft + plotW * i / (days - 1);

                var tb = new TextBlock
                {
                    Text = startDate.AddDays(i).ToString("MM-dd"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, xPos - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, padTop + plotH + 8);
                canvas.Children.Add(tb);
            }

            // 折线 1
            DrawPolyline(canvas, data1, days, maxVal,
                padLeft, padTop, plotW, plotH, brush1);

            // 折线 2
            DrawPolyline(canvas, data2, days, maxVal,
                padLeft, padTop, plotW, plotH, brush2);
        }

        private void DrawPolyline(Canvas canvas, int[] data, int days, int maxVal,
            double padLeft, double padTop, double plotW, double plotH, Brush brush)
        {
            if (days <= 0) return;

            var pts = new List<Point>();
            for (int i = 0; i < days; i++)
            {
                double xPos = days <= 1 ? padLeft + plotW / 2
                    : padLeft + plotW * i / (days - 1);
                double ratio = maxVal <= 0 ? 0 : (double)data[i] / maxVal;
                double yPos = padTop + plotH - plotH * ratio;
                pts.Add(new Point(xPos, yPos));
            }

            if (pts.Count >= 2)
            {
                var poly = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round
                };
                foreach (var p in pts) poly.Points.Add(p);
                canvas.Children.Add(poly);
            }

            foreach (var p in pts)
            {
                var ellipse = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(ellipse, p.X - 3.5);
                Canvas.SetTop(ellipse, p.Y - 3.5);
                canvas.Children.Add(ellipse);
            }
        }

        // ═══════════════════════════════════════
        // 柱状图（用于值日）
        // ═══════════════════════════════════════

        private void DrawBarChart(Canvas canvas, int[] data, List<string> labels, string highlightName)
        {
            double w = canvas.ActualWidth > 0 ? canvas.ActualWidth : 600;
            double h = canvas.ActualHeight > 0 ? canvas.ActualHeight : 260;
            if (w < 100) w = 600;
            if (h < 100) h = 260;

            canvas.Width = w;
            canvas.Height = h;

            int n = data.Length;
            if (n == 0) return;

            double padLeft = 40;
            double padRight = 20;
            double padTop = 20;
            double padBottom = 60;

            double plotW = w - padLeft - padRight;
            double plotH = h - padTop - padBottom;
            if (plotW <= 20 || plotH <= 20) return;

            int maxVal = data.Max();
            if (maxVal == 0) maxVal = 1;

            int gridLines = 4;
            for (int i = 0; i <= gridLines; i++)
            {
                double yVal = maxVal * i / gridLines;
                double yPos = padTop + plotH - plotH * i / gridLines;

                var line = new Line
                {
                    X1 = padLeft,
                    Y1 = yPos,
                    X2 = padLeft + plotW,
                    Y2 = yPos,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);

                var tb = new TextBlock
                {
                    Text = yVal.ToString("F0"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                Canvas.SetLeft(tb, 8);
                Canvas.SetTop(tb, yPos - 8);
                canvas.Children.Add(tb);
            }

            double colWidth = plotW / n;
            double barWidth = colWidth * 0.6;
            if (barWidth < 10) barWidth = 10;
            if (barWidth > 60) barWidth = 60;

            var defaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BAAF7"));
            var highlightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0"));

            for (int i = 0; i < n; i++)
            {
                double xCenter = padLeft + colWidth * i + colWidth / 2;
                double ratio = (double)data[i] / maxVal;
                double barH = plotH * ratio;
                double yTop = padTop + plotH - barH;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = barH,
                    Fill = labels[i] == highlightName ? highlightBrush : defaultBrush,
                    RadiusX = 3,
                    RadiusY = 3
                };
                Canvas.SetLeft(rect, xCenter - barWidth / 2);
                Canvas.SetTop(rect, yTop);
                canvas.Children.Add(rect);

                // 数值
                if (data[i] > 0)
                {
                    var tbVal = new TextBlock
                    {
                        Text = data[i].ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0")),
                        FontFamily = new FontFamily("Microsoft YaHei UI")
                    };
                    tbVal.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(tbVal, xCenter - tbVal.DesiredSize.Width / 2);
                    Canvas.SetTop(tbVal, yTop - 16);
                    canvas.Children.Add(tbVal);
                }

                // 标签
                var tbLbl = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606060")),
                    FontFamily = new FontFamily("Microsoft YaHei UI"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Width = colWidth - 6
                };
                Canvas.SetLeft(tbLbl, padLeft + colWidth * i + 3);
                Canvas.SetTop(tbLbl, padTop + plotH + 6);
                canvas.Children.Add(tbLbl);
            }
        }
    }
}