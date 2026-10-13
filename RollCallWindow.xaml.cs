﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CMW
{
    public partial class RollCallWindow : Window
    {
        private HomeworkItem _homework;
        private List<StudentToggleVm> _students = new List<StudentToggleVm>();
        private bool _isLoading;

        public List<string> SubmittedIds { get; private set; } = new List<string>();

        public RollCallWindow(HomeworkItem homework)
        {
            InitializeComponent();
            _homework = homework;

            tbTitle.Text = $"点名 · {homework.HomeworkName}";
            if (!string.IsNullOrWhiteSpace(homework.Subject))
                tbTitle.Text += $"（{homework.Subject}）";

            Loaded += RollCallWindow_Loaded;
        }

        private void RollCallWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                var studentData = JsonHelper.Load<StudentListData>(AppPaths.Students)
                                  ?? new StudentListData();
                var allStudents = (studentData.Students ?? new List<Student>())
                    .OrderBy(s => s.Order)
                    .ToList();

                var submitted = new HashSet<string>(
                    _homework.SubmittedStudentIds ?? new List<string>());

                _students = allStudents.Select(s => new StudentToggleVm
                {
                    Id = s.Id,
                    DisplayText = s.DisplayText,
                    IsSubmitted = submitted.Contains(s.Id),
                    Student = s
                }).ToList();

                icStudents.ItemsSource = _students;

                int total = _students.Count;
                int submittedCount = _students.Count(x => x.IsSubmitted);
                tbSubtitle.Text = $"共 {total} 名学生；点击卡片切换「已交 / 未交」";
                tbCount.Text = $"已交 {submittedCount} / {total}";

                if (total == 0)
                {
                    tbSubtitle.Text = "班级名单为空，请先到「班级名单」页添加学生";
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void Student_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateCount();
        }

        private void Student_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateCount();
        }

        private void UpdateCount()
        {
            int total = _students.Count;
            int submittedCount = _students.Count(x => x.IsSubmitted);
            tbCount.Text = $"已交 {submittedCount} / {total}";
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _students) s.IsSubmitted = true;
            RefreshToggleState();
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _students) s.IsSubmitted = false;
            RefreshToggleState();
        }

        private void BtnInvert_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _students) s.IsSubmitted = !s.IsSubmitted;
            RefreshToggleState();
        }

        private void RefreshToggleState()
        {
            var snapshot = _students.ToList();
            icStudents.ItemsSource = null;
            _isLoading = true;
            icStudents.ItemsSource = snapshot;
            _isLoading = false;

            UpdateCount();
        }

        // ═══════════════════════════════════════
        // ★ 导出未交名单为文本
        // ═══════════════════════════════════════

        private void BtnExportUnsigned_Click(object sender, RoutedEventArgs e)
        {
            var unsigned = _students
                .Where(s => !s.IsSubmitted)
                .Select(s => s.Student.Name)
                .ToList();

            if (unsigned.Count == 0)
            {
                tbTip.Text = "✅ 全部已交，无未交名单";
                MessageBox.Show("全部已交，无未交名单。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 组装文本：标题 + 名单 + 逗号分隔
            string title = _homework.HomeworkName;
            if (!string.IsNullOrWhiteSpace(_homework.Subject))
                title += $"（{_homework.Subject}）";

            var sb = new StringBuilder();
            sb.AppendLine($"【未交名单】{title}");
            sb.AppendLine($"统计时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"未交人数：{unsigned.Count} 人");
            sb.AppendLine();
            sb.AppendLine(string.Join("、", unsigned));
            sb.AppendLine();
            sb.AppendLine("——");
            sb.AppendLine("老师 / 组长：");

            string fileName = SafeFileName($"未交名单-{_homework.HomeworkName}");

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件|*.txt|所有文件|*.*",
                FileName = $"{fileName}.txt",
                Title = "导出未交名单"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // UTF-8 BOM：记事本 / 微信 / QQ 正常显示
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                tbTip.Text = $"✅ 已导出 {unsigned.Count} 人未交名单";

                MessageBox.Show("✅ 已导出：" + dlg.FileName
                    + $"\n\n共 {unsigned.Count} 人未交，可以直接发给组长或老师。",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 导出失败";
                MessageBox.Show("导出失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string SafeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "未交名单";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        // ═══════════════════════════════════════
        // 确定 / 取消
        // ═══════════════════════════════════════

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SubmittedIds = _students
                .Where(s => s.IsSubmitted)
                .Select(s => s.Id)
                .ToList();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // ═══════ 学生行 ViewModel ═══════

    internal class StudentToggleVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public bool IsSubmitted { get; set; }
        public Student Student { get; set; } = new Student();
    }
}