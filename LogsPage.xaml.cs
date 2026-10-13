﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CMW
{
    public partial class LogsPage : UserControl
    {
        private string _currentWeekKey = "";

        public LogsPage()
        {
            InitializeComponent();
            Loaded += LogsPage_Loaded;
        }

        private void LogsPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        // ═══════════════════════════════════════
        // 列表
        // ═══════════════════════════════════════

        private void RefreshList()
        {
            try
            {
                var reports = WeeklyReportService.GetAllReports();

                var vms = reports.Select(r => new ReportRowVm
                {
                    Meta = r,
                    WeekKey = r.WeekKey,
                    RangeText = r.RangeText,
                    SummaryText = $"生成于 {r.GeneratedAt:MM-dd HH:mm} · 作业 {r.HomeworkCount} · 待办 {r.TodoCompletedCount}/{r.TodoTotalCount}"
                }).ToList();

                icReports.ItemsSource = vms;

                tbEmpty.Visibility = vms.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // 生成本周
        // ═══════════════════════════════════════

        private void BtnGenerateThisWeek_Click(object sender, RoutedEventArgs e)
        {
            var (ok, err, path) = WeeklyReportService.GenerateForWeek(DateTime.Today);

            if (!ok)
            {
                MessageBox.Show("生成失败：" + err, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshList();

            MessageBox.Show("✅ 报告已生成：" + path,
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ═══════════════════════════════════════
        // 打开文件夹
        // ═══════════════════════════════════════

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(AppPaths.ReportsDir))
                    Directory.CreateDirectory(AppPaths.ReportsDir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.ReportsDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ═══════════════════════════════════════
        // 查看
        // ═══════════════════════════════════════

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ReportRowVm vm) return;

            _currentWeekKey = vm.WeekKey;
            tbPreviewTitle.Text = $"报告预览 · {vm.WeekKey}";
            tbPreview.Text = WeeklyReportService.ReadReportContent(vm.Meta);
        }

        // ═══════════════════════════════════════
        // 删除
        // ═══════════════════════════════════════

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ReportRowVm vm) return;

            if (MessageBox.Show(
                $"确定删除周报 {vm.WeekKey}？该操作不可恢复。",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            bool ok = WeeklyReportService.DeleteReport(vm.Meta);

            if (ok)
            {
                _currentWeekKey = "";
                tbPreviewTitle.Text = "报告预览";
                tbPreview.Text = "（从左侧列表选择一份报告查看）";
                RefreshList();
            }
        }

        // ═══════════════════════════════════════
        // 导出为文本（.txt）
        // ═══════════════════════════════════════

        private void BtnExportText_Click(object sender, RoutedEventArgs e)
        {
            ExportCurrentPreview(asMarkdown: false);
        }

        // ═══════════════════════════════════════
        // 导出为 Markdown（.md）
        // ═══════════════════════════════════════

        private void BtnExportMd_Click(object sender, RoutedEventArgs e)
        {
            ExportCurrentPreview(asMarkdown: true);
        }

        private void ExportCurrentPreview(bool asMarkdown)
        {
            string content = tbPreview.Text ?? "";
            if (string.IsNullOrWhiteSpace(content)
                || content.StartsWith("（从左侧列表"))
            {
                MessageBox.Show("请先从左侧列表选择一份报告。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string weekKey = string.IsNullOrEmpty(_currentWeekKey)
                ? DateTime.Now.ToString("yyyyMMdd-HHmmss")
                : _currentWeekKey;

            string ext = asMarkdown ? "md" : "txt";
            string defaultName = $"周报-{weekKey}.{ext}";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = asMarkdown
                    ? "Markdown 文件|*.md|文本文件|*.txt|所有文件|*.*"
                    : "文本文件|*.txt|所有文件|*.*",
                FileName = defaultName,
                Title = "导出周报"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // UTF-8 BOM：记事本 / Excel / WPS / 微信 都能正确识别中文
                File.WriteAllText(dlg.FileName, content, new UTF8Encoding(true));

                MessageBox.Show("✅ 已导出：" + dlg.FileName
                    + "\n\n可以用记事本打开，或发送到微信 / QQ。",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // ═══════ ViewModel ═══════

    internal class ReportRowVm
    {
        public WeeklyReportMeta Meta { get; set; } = new WeeklyReportMeta();
        public string WeekKey { get; set; } = "";
        public string RangeText { get; set; } = "";
        public string SummaryText { get; set; } = "";
    }
}