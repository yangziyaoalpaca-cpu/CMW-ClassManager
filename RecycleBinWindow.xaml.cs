using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CMW
{
    public partial class RecycleBinWindow : ReleasableWindow
    {
        public RecycleBinWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshList();
        }

        // ═══════ 列表刷新 ═══════

        private void RefreshList()
        {
            var items = RecycleBinService.GetAll();
            var rows = items.Select(x => new RecycleBinRowVm
            {
                Id = x.Id,
                TypeDisplay = MapType(x.Type),
                DisplayText = string.IsNullOrWhiteSpace(x.DisplayText) ? "（无内容）" : x.DisplayText,
                DeletedAt = x.DeletedAt
            }).ToList();

            icRecycleList.ItemsSource = rows;

            tbEmpty.Visibility = rows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            tbSubtitle.Text = rows.Count == 0
                ? "删除的作业 / 待办 / 值日组会进入这里，最多保留 50 条"
                : $"共 {rows.Count} 条记录（最多保留 50 条）";
        }

        private static string MapType(string type)
        {
            return type switch
            {
                RecycleBinService.TypeHomework => "作业",
                RecycleBinService.TypeTodo => "待办",
                RecycleBinService.TypeDutyGroup => "值日组",
                _ => string.IsNullOrWhiteSpace(type) ? "未知" : type
            };
        }

        // ═══════ 恢复 ═══════

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;

            bool ok = RecycleBinService.Restore(id);

            if (ok)
            {
                tbTip.Text = "✅ 已恢复";
                App.NotifySettingsChanged();
            }
            else
            {
                tbTip.Text = "⚠️ 恢复失败（可能已存在同名项）";
            }

            RefreshList();
        }

        // ═══════ 彻底删除 ═══════

        private void BtnDeletePermanently_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;

            var result = MessageBox.Show(
                "确定彻底删除这条记录？不可恢复。",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            RecycleBinService.DeletePermanently(id);
            tbTip.Text = "✅ 已彻底删除";
            RefreshList();
        }

        // ═══════ 清空 ═══════

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            var items = RecycleBinService.GetAll();
            if (items.Count == 0)
            {
                tbTip.Text = "回收站已经是空的";
                return;
            }

            var result = MessageBox.Show(
                $"确定清空回收站？共 {items.Count} 条记录将彻底删除，不可恢复。",
                "确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            RecycleBinService.ClearAll();
            tbTip.Text = "✅ 回收站已清空";
            RefreshList();
        }

        // ═══════ 关闭 ═══════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // ═══════ 列表行 ViewModel ═══════

    internal class RecycleBinRowVm
    {
        public string Id { get; set; } = "";
        public string TypeDisplay { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public DateTime DeletedAt { get; set; }
    }
}