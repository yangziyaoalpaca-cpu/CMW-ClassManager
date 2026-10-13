﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CMW
{
    public partial class ExamPage : UserControl
    {
        private List<ExamItem> _exams = new List<ExamItem>();
        private StackPanel? _currentPanel;

        public ExamPage()
        {
            InitializeComponent();
            Loaded += ExamPage_Loaded;
        }

        private void ExamPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadExams();
            RefreshList();

            if (dpExamDate.SelectedDate == null)
                dpExamDate.SelectedDate = DateTime.Today;

            _currentPanel = PanelAdd;
        }

        // ═══════════════════════════════════════
        // 数据
        // ═══════════════════════════════════════

        private void LoadExams()
        {
            var data = JsonHelper.Load<ExamListData>(AppPaths.Exams) ?? new ExamListData();
            _exams = (data.Exams ?? new List<ExamItem>())
                .OrderByDescending(x => x.ExamDate)
                .ThenBy(x => x.Subject)
                .ToList();
        }

        private void SaveExams()
        {
            try
            {
                JsonHelper.Save(AppPaths.Exams, new ExamListData
                {
                    Exams = _exams
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误");
            }
        }

        private void RefreshList()
        {
            var ordered = _exams
                .OrderByDescending(x => x.ExamDate)
                .ThenBy(x => x.Subject)
                .ToList();

            icExamList.ItemsSource = null;
            icExamList.ItemsSource = ordered;

            tbEmpty.Visibility = ordered.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ═══════════════════════════════════════
        // Tab
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabAdd" => PanelAdd,
                "TabList" => PanelList,
                "TabTrend" => PanelTrend,
                _ => null
            };

            if (target == null || target == _currentPanel) return;
            SwitchPanel(target);

            if (target == PanelTrend)
                DrawTrend();
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
        // 新增
        // ═══════════════════════════════════════

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string name = (txtExamName.Text ?? "").Trim();
            string subject = (txtSubject.Text ?? "").Trim();
            DateTime? date = dpExamDate.SelectedDate;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("请输入考试名称。"); return;
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                MessageBox.Show("请输入科目。"); return;
            }
            if (date == null)
            {
                MessageBox.Show("请选择考试日期。"); return;
            }

            double? avg = ParseNullableDouble(txtAverage.Text);
            double? high = ParseNullableDouble(txtHighest.Text);
            double? low = ParseNullableDouble(txtLowest.Text);

            if (avg.HasValue && high.HasValue && avg.Value > high.Value)
            {
                MessageBox.Show("平均分不能高于最高分。"); return;
            }
            if (avg.HasValue && low.HasValue && avg.Value < low.Value)
            {
                MessageBox.Show("平均分不能低于最低分。"); return;
            }
            if (high.HasValue && low.HasValue && high.Value < low.Value)
            {
                MessageBox.Show("最高分不能低于最低分。"); return;
            }

            _exams.Add(new ExamItem
            {
                Id = Guid.NewGuid().ToString(),
                ExamName = name,
                Subject = subject,
                ExamDate = date.Value,
                ClassAverage = avg,
                ClassHighest = high,
                ClassLowest = low,
                Remark = (txtRemark.Text ?? "").Trim()
            });

            SaveExams();
            RefreshList();

            txtExamName.Text = "";
            txtSubject.Text = "";
            txtAverage.Text = "";
            txtHighest.Text = "";
            txtLowest.Text = "";
            txtRemark.Text = "";
            dpExamDate.SelectedDate = DateTime.Today;

            TabList.IsChecked = true;
        }

        private static double? ParseNullableDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (double.TryParse(s.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v))
                return v;
            return null;
        }

        // ═══════════════════════════════════════
        // 删除
        // ═══════════════════════════════════════

        private void DeleteExam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ExamItem item) return;

            if (MessageBox.Show(
                $"确定删除「{item.ExamName} · {item.Subject}」？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _exams.Remove(item);
            SaveExams();
            RefreshList();
        }

        // ═══════════════════════════════════════
        // ★ 导出 CSV
        // ═══════════════════════════════════════

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_exams.Count == 0)
            {
                MessageBox.Show("暂无成绩记录可导出。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"考试成绩-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("日期,考试名称,科目,班级平均分,最高分,最低分,备注");

                var ordered = _exams
                    .OrderByDescending(x => x.ExamDate)
                    .ThenBy(x => x.Subject)
                    .ToList();

                foreach (var x in ordered)
                {
                    sb.AppendLine(string.Join(",",
                        Csv(x.ExamDate.ToString("yyyy-MM-dd")),
                        Csv(x.ExamName),
                        Csv(x.Subject),
                        Csv(x.ClassAverage.HasValue ? x.ClassAverage.Value.ToString("F1") : ""),
                        Csv(x.ClassHighest.HasValue ? x.ClassHighest.Value.ToString("F1") : ""),
                        Csv(x.ClassLowest.HasValue ? x.ClassLowest.Value.ToString("F1") : ""),
                        Csv(x.Remark)));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));

                MessageBox.Show("✅ 导出成功：" + dlg.FileName,
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // ═══════════════════════════════════════
        // 趋势图（手绘 Polyline）
        // ═══════════════════════════════════════

        private static readonly string[] SubjectPalette = new[]
        {
            "#1E5FD0",
            "#2E7D32",
            "#D32F2F",
            "#6A1B9A",
            "#E65100",
            "#00838F",
            "#558B2F",
            "#AD1457",
            "#4527A0",
            "#00695C",
        };

        private void DrawTrend()
        {
            cvTrend.Children.Clear();

            var withAvg = _exams
                .Where(x => x.ClassAverage.HasValue)
                .ToList();

            if (withAvg.Count == 0)
            {
                tbTrendEmpty.Visibility = Visibility.Visible;
                tbTrendLegend.Text = "";
                return;
            }

            tbTrendEmpty.Visibility = Visibility.Collapsed;

            var bySubject = withAvg
                .GroupBy(x => x.Subject)
                .OrderBy(g => g.Key)
                .ToList();

            double w = cvTrend.ActualWidth > 0 ? cvTrend.ActualWidth : 600;
            double h = cvTrend.ActualHeight > 0 ? cvTrend.ActualHeight : 260;

            if (w < 100) w = 600;
            if (h < 100) h = 260;

            cvTrend.Width = w;
            cvTrend.Height = h;

            double padLeft = 50;
            double padRight = 20;
            double padTop = 20;
            double padBottom = 40;

            double plotW = w - padLeft - padRight;
            double plotH = h - padTop - padBottom;

            if (plotW <= 20 || plotH <= 20) return;

            double minScore = withAvg.Min(x => x.ClassAverage!.Value);
            double maxScore = withAvg.Max(x => x.ClassAverage!.Value);

            double span = maxScore - minScore;
            if (span < 10) span = 10;
            minScore = Math.Max(0, minScore - span * 0.1);
            maxScore = Math.Min(150, maxScore + span * 0.1);

            if (maxScore <= minScore) maxScore = minScore + 10;

            int gridLines = 4;
            for (int i = 0; i <= gridLines; i++)
            {
                double yVal = minScore + (maxScore - minScore) * i / gridLines;
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
                cvTrend.Children.Add(line);

                var tb = new TextBlock
                {
                    Text = yVal.ToString("F0"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                Canvas.SetLeft(tb, 8);
                Canvas.SetTop(tb, yPos - 8);
                cvTrend.Children.Add(tb);
            }

            var allDates = withAvg
                .Select(x => x.ExamDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            for (int i = 0; i < allDates.Count; i++)
            {
                double xPos = allDates.Count <= 1
                    ? padLeft + plotW / 2
                    : padLeft + plotW * i / (allDates.Count - 1);

                var tb = new TextBlock
                {
                    Text = allDates[i].ToString("MM-dd"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, xPos - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, padTop + plotH + 8);
                cvTrend.Children.Add(tb);
            }

            var legendParts = new List<string>();
            int colorIdx = 0;

            foreach (var group in bySubject)
            {
                var pts = group
                    .OrderBy(x => x.ExamDate)
                    .Select(x =>
                    {
                        int di = allDates.IndexOf(x.ExamDate.Date);
                        double xPos = allDates.Count <= 1
                            ? padLeft + plotW / 2
                            : padLeft + plotW * di / (allDates.Count - 1);

                        double ratio = (x.ClassAverage!.Value - minScore) / (maxScore - minScore);
                        double yPos = padTop + plotH - plotH * ratio;

                        return new Point(xPos, yPos);
                    })
                    .ToList();

                string colorHex = SubjectPalette[colorIdx % SubjectPalette.Length];
                var color = (Color)ColorConverter.ConvertFromString(colorHex);

                if (pts.Count >= 2)
                {
                    var poly = new Polyline
                    {
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = 2,
                        StrokeLineJoin = PenLineJoin.Round
                    };
                    foreach (var p in pts) poly.Points.Add(p);
                    cvTrend.Children.Add(poly);
                }

                foreach (var p in pts)
                {
                    var ellipse = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(color),
                        Stroke = Brushes.White,
                        StrokeThickness = 1.5
                    };
                    Canvas.SetLeft(ellipse, p.X - 4);
                    Canvas.SetTop(ellipse, p.Y - 4);
                    cvTrend.Children.Add(ellipse);
                }

                legendParts.Add($"■ {group.Key}");
                colorIdx++;
            }

            tbTrendLegend.Text = "图例：" + string.Join("    ", legendParts)
                + "\n（按科目分线，横轴为日期，纵轴为班级平均分）";
        }
    }
}