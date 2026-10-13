using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CMW
{
    public partial class SubjectMapWindow : Window
    {
        private AppConfig _config = new AppConfig();
        private Dictionary<string, string> _map = new Dictionary<string, string>();

        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        public SubjectMapWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadAndRefresh();
        }

        private void LoadAndRefresh()
        {
            _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
            if (_config.LeftScheduleSubjectMap == null)
                _config.LeftScheduleSubjectMap = new Dictionary<string, string>();

            _map = new Dictionary<string, string>(_config.LeftScheduleSubjectMap);
            RefreshList();
        }

        private void RefreshList()
        {
            panelItems.Children.Clear();

            if (_map.Count == 0)
            {
                panelItems.Children.Add(new TextBlock
                {
                    Text = "暂无简称配置。\n\n左侧课表会直接显示课程全名。\n可在此添加快捷简称，如「数学 → 数」。",
                    FontFamily = UiFont,
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 30),
                    TextWrapping = TextWrapping.Wrap
                });

                tbCount.Text = "共 0 条简称";
                return;
            }

            foreach (var kv in _map.OrderBy(x => x.Key))
            {
                panelItems.Children.Add(BuildRow(kv.Key, kv.Value));
            }

            tbCount.Text = $"共 {_map.Count} 条简称";
        }

        private Border BuildRow(string subject, string shortName)
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tbSubject = new TextBlock
            {
                Text = subject,
                FontFamily = UiFont,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020")),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tbSubject, 0);
            grid.Children.Add(tbSubject);

            var arrow = new TextBlock
            {
                Text = "→",
                FontFamily = UiFont,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrow, 1);
            grid.Children.Add(arrow);

            var tbShort = new TextBlock
            {
                Text = shortName,
                FontFamily = UiFont,
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E5FD0")),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tbShort, 2);
            grid.Children.Add(tbShort);

            var btnDel = new Button
            {
                Content = "删除",
                Padding = new Thickness(10, 4, 10, 4),
                FontFamily = UiFont,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = subject
            };
            if (Application.Current?.TryFindResource("PclButtonDanger") is Style s) btnDel.Style = s;
            btnDel.Click += BtnDelete_Click;
            Grid.SetColumn(btnDel, 3);
            grid.Children.Add(btnDel);

            row.Child = grid;
            return row;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string subj = (txtSubject.Text ?? "").Trim();
            string shortName = (txtShortName.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(subj))
            {
                tbTip.Text = "⚠️ 请输入课程原名称";
                return;
            }

            if (string.IsNullOrWhiteSpace(shortName))
            {
                tbTip.Text = "⚠️ 请输入简称（建议 1~2 字）";
                return;
            }

            _map[subj] = shortName;
            SaveMap();

            txtSubject.Text = "";
            txtShortName.Text = "";

            tbTip.Text = $"✅ 已设置「{subj} → {shortName}」";
            RefreshList();

            // 通知左侧课表刷新
            App.NotifySettingsChanged();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string subj) return;

            _map.Remove(subj);
            SaveMap();

            tbTip.Text = $"✅ 已删除「{subj}」的简称";
            RefreshList();

            App.NotifySettingsChanged();
        }

        private void SaveMap()
        {
            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                cfg.LeftScheduleSubjectMap = new Dictionary<string, string>(_map);
                JsonHelper.Save(AppPaths.Config, cfg);
                _config = cfg;
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}