﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CMW
{
    public partial class DayEventWindow : Window
    {
        private readonly DateTime _date;
        private List<ClassEvent> _events = new List<ClassEvent>();

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

        public DayEventWindow(DateTime date, List<ClassEvent> eventsOnDate)
        {
            InitializeComponent();

            _date = date.Date;
            _events = eventsOnDate ?? new List<ClassEvent>();

            tbDateTitle.Text = _date.ToString("yyyy 年 M 月 d 日 dddd");

            Loaded += (s, e) =>
            {
                RefreshList();
                txtEventTitle.Focus();
            };
        }

        // ═══════════════════════════════════════
        // 列表
        // ═══════════════════════════════════════

        private void RefreshList()
        {
            panelEvents.Children.Clear();

            if (_events.Count == 0)
            {
                panelEvents.Children.Add(new TextBlock
                {
                    Text = "本日暂无活动",
                    FontFamily = UiFont,
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 30)
                });
                return;
            }

            foreach (var ev in _events.OrderBy(e => e.Date))
            {
                var colors = CategoryColors.TryGetValue(ev.Category, out var c)
                    ? c
                    : CategoryColors["其他"];

                var row = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(12, 10, 12, 10)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tag = new Border
                {
                    Background = colors.Bg,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                tag.Child = new TextBlock
                {
                    Text = ev.Category,
                    FontFamily = UiFont,
                    FontSize = 11,
                    Foreground = colors.Fg
                };
                Grid.SetColumn(tag, 0);
                grid.Children.Add(tag);

                var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
                stack.Children.Add(new TextBlock
                {
                    Text = ev.Title,
                    FontFamily = UiFont,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020")),
                    TextWrapping = TextWrapping.Wrap
                });

                if (!string.IsNullOrWhiteSpace(ev.Remark))
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = ev.Remark,
                        FontFamily = UiFont,
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }
                else if (ev.EndDate.HasValue && ev.EndDate.Value.Date != ev.Date.Date)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = "跨天至 " + ev.EndDate.Value.ToString("MM-dd"),
                        FontFamily = UiFont,
                        FontSize = 11,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                Grid.SetColumn(stack, 1);
                grid.Children.Add(stack);

                var btnDel = new Button
                {
                    Content = "删除",
                    FontFamily = UiFont,
                    FontSize = 12,
                    Padding = new Thickness(10, 4, 10, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = ev
                };
                if (Application.Current?.TryFindResource("PclButtonDanger") is Style dangerStyle)
                    btnDel.Style = dangerStyle;
                btnDel.Click += BtnDelete_Click;
                Grid.SetColumn(btnDel, 2);
                grid.Children.Add(btnDel);

                row.Child = grid;
                panelEvents.Children.Add(row);
            }
        }

        // ═══════════════════════════════════════
        // 新增
        // ═══════════════════════════════════════

        private void TxtEventTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAdd_Click(sender, e);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string title = (txtEventTitle.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                tbTip.Text = "⚠️ 请输入活动名称";
                return;
            }

            string category = (cbCategory.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "活动";
            string remark = (txtRemark.Text ?? "").Trim();

            var newEvent = new ClassEvent
            {
                Title = title,
                Date = _date,
                Category = category,
                Remark = remark
            };

            _events.Add(newEvent);

            SaveToFile();

            txtEventTitle.Text = "";
            txtRemark.Text = "";

            RefreshList();

            tbTip.Text = $"✅ 已添加「{title}」";
        }

        // ═══════════════════════════════════════
        // 删除
        // ═══════════════════════════════════════

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ClassEvent ev) return;

            if (MessageBox.Show(
                $"确定删除活动「{ev.Title}」？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var allData = JsonHelper.Load<ClassEventListData>(AppPaths.Events)
                          ?? new ClassEventListData();
            allData.Events.RemoveAll(x => x.Id == ev.Id);
            JsonHelper.Save(AppPaths.Events, allData);

            _events.Remove(ev);
            RefreshList();

            tbTip.Text = $"✅ 已删除「{ev.Title}」";
        }

        // ═══════════════════════════════════════
        // 保存
        // ═══════════════════════════════════════

        private void SaveToFile()
        {
            try
            {
                var allData = JsonHelper.Load<ClassEventListData>(AppPaths.Events)
                              ?? new ClassEventListData();

                foreach (var ev in _events)
                {
                    if (!allData.Events.Any(x => x.Id == ev.Id))
                        allData.Events.Add(ev);
                }

                JsonHelper.Save(AppPaths.Events, allData);
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        // ═══════════════════════════════════════
        // 关闭
        // ═══════════════════════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}