﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CMW
{
    public partial class HomeworkTemplateWindow : Window
    {
        private List<HomeworkTemplate> _templates = new List<HomeworkTemplate>();

        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        /// <summary>用户点击"应用"后返回的模板；为 null 表示没选</summary>
        public HomeworkTemplate? SelectedTemplate { get; private set; }

        public HomeworkTemplateWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshList();
        }

        private void RefreshList()
        {
            _templates = LoadTemplates();

            panelTemplates.Children.Clear();

            if (_templates.Count == 0)
            {
                panelTemplates.Children.Add(new TextBlock
                {
                    Text = "暂无模板，可在下方新增",
                    FontFamily = UiFont,
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 30)
                });
                return;
            }

            foreach (var tpl in _templates.OrderByDescending(t => t.IsFavorite).ThenBy(t => t.Name))
            {
                panelTemplates.Children.Add(BuildRow(tpl));
            }
        }

        private Border BuildRow(HomeworkTemplate tpl)
        {
            var row = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = tpl.Name,
                FontFamily = UiFont,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);

            var subBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(tpl.Subject) ? "—" : tpl.Subject,
                FontFamily = UiFont,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606060")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(subBlock, 1);
            grid.Children.Add(subBlock);

            var priText = tpl.Priority switch
            {
                "Urgent" => "紧急",
                "VeryUrgent" => "非常紧急",
                _ => "普通"
            };

            var priTag = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    tpl.Priority switch
                    {
                        "VeryUrgent" => "#1E5FD0",
                        "Urgent" => "#D0E2FC",
                        _ => "#F0F0F0"
                    })),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            priTag.Child = new TextBlock
            {
                Text = priText,
                FontFamily = UiFont,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    tpl.Priority switch
                    {
                        "VeryUrgent" => "#FFFFFF",
                        "Urgent" => "#1E5FD0",
                        _ => "#606060"
                    }))
            };
            Grid.SetColumn(priTag, 2);
            grid.Children.Add(priTag);

            var durBlock = new TextBlock
            {
                Text = tpl.DurationDays + " 天",
                FontFamily = UiFont,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(durBlock, 3);
            grid.Children.Add(durBlock);

            var btnApply = new Button
            {
                Content = "应用",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 4, 12, 4),
                FontFamily = UiFont,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tpl
            };
            if (Application.Current?.TryFindResource("PclButtonPrimary") is Style primaryStyle)
                btnApply.Style = primaryStyle;
            btnApply.Click += BtnApply_Click;
            Grid.SetColumn(btnApply, 4);
            grid.Children.Add(btnApply);

            var btnDel = new Button
            {
                Content = "删除",
                Padding = new Thickness(10, 4, 10, 4),
                FontFamily = UiFont,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tpl
            };
            if (Application.Current?.TryFindResource("PclButtonDanger") is Style dangerStyle)
                btnDel.Style = dangerStyle;
            btnDel.Click += BtnDelete_Click;
            Grid.SetColumn(btnDel, 5);
            grid.Children.Add(btnDel);

            row.Child = grid;
            return row;
        }

        // ═══════════════════════════════════════
        // 应用
        // ═══════════════════════════════════════

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not HomeworkTemplate tpl) return;

            SelectedTemplate = tpl;
            DialogResult = true;
            Close();
        }

        // ═══════════════════════════════════════
        // 新增
        // ═══════════════════════════════════════

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string name = (txtName.Text ?? "").Trim();
            string subject = (txtSubject.Text ?? "").Trim();
            string priority = (cbPriority.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal";

            if (string.IsNullOrWhiteSpace(name))
            {
                tbTip.Text = "⚠️ 请输入模板名称";
                return;
            }

            if (!int.TryParse((txtDays.Text ?? "").Trim(), out int days) || days < 1 || days > 60)
            {
                tbTip.Text = "⚠️ 天数需为 1~60";
                return;
            }

            var tpl = new HomeworkTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Subject = subject,
                Priority = priority,
                DurationDays = days
            };

            var list = LoadTemplates();
            list.Add(tpl);
            SaveTemplates(list);

            txtName.Text = "";
            txtSubject.Text = "";
            txtDays.Text = "1";
            cbPriority.SelectedIndex = 0;

            tbTip.Text = $"✅ 已添加模板「{name}」";
            RefreshList();
        }

        // ═══════════════════════════════════════
        // 删除
        // ═══════════════════════════════════════

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not HomeworkTemplate tpl) return;

            if (MessageBox.Show(
                $"确定删除模板「{tpl.Name}」？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var list = LoadTemplates();
            list.RemoveAll(x => x.Id == tpl.Id);
            SaveTemplates(list);

            tbTip.Text = $"✅ 已删除「{tpl.Name}」";
            RefreshList();
        }

        // ═══════════════════════════════════════
        // 关闭
        // ═══════════════════════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═══════════════════════════════════════
        // 数据
        // ═══════════════════════════════════════

        private static List<HomeworkTemplate> LoadTemplates()
        {
            var data = JsonHelper.Load<HomeworkTemplateListData>(AppPaths.HomeworkTemplates);
            return data?.Templates ?? new List<HomeworkTemplate>();
        }

        private static void SaveTemplates(List<HomeworkTemplate> list)
        {
            JsonHelper.Save(AppPaths.HomeworkTemplates, new HomeworkTemplateListData
            {
                Templates = list
            });
        }
    }
}