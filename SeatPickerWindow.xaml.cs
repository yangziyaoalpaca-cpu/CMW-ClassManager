﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CMW
{
    public partial class SeatPickerWindow : Window
    {
        private List<StudentPickVm> _vms = new List<StudentPickVm>();
        private List<StudentPickVm> _filtered = new List<StudentPickVm>();
        private bool _isLoading = true;

        public string SelectedStudentId { get; private set; } = "";

        public SeatPickerWindow(List<Student> students, string currentStudentId, int row, int col)
        {
            InitializeComponent();

            tbSubtitle.Text = $"位置：第 {row + 1} 行 第 {col + 1} 列";

            _vms = students.Select(s => new StudentPickVm
            {
                Id = s.Id,
                DisplayText = s.DisplayText,
                Name = s.Name,
                StudentId = s.StudentId,
                IsSelected = s.Id == currentStudentId,
                Student = s
            }).ToList();

            _filtered = new List<StudentPickVm>(_vms);
            icStudents.ItemsSource = _filtered;

            Loaded += (s, e) => _isLoading = false;
        }

        // ═══════════════════════════════════════
        // 搜索
        // ═══════════════════════════════════════

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string key = (txtSearch.Text ?? "").Trim();

            if (string.IsNullOrEmpty(key))
                _filtered = new List<StudentPickVm>(_vms);
            else
                _filtered = _vms.Where(x =>
                    x.Name.Contains(key) ||
                    x.StudentId.Contains(key))
                    .ToList();

            icStudents.ItemsSource = null;
            icStudents.ItemsSource = _filtered;
        }

        // ═══════════════════════════════════════
        // 单选：选中一个取消其他
        // ═══════════════════════════════════════

        private void Student_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (sender is not CheckBox cb || cb.Tag is not StudentPickVm vm) return;

            foreach (var s in _vms)
            {
                if (s == vm) continue;
                if (s.IsSelected)
                {
                    s.IsSelected = false;
                    // 需要刷新显示
                }
            }

            RefreshList();
        }

        private void Student_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // 允许取消
        }

        private void RefreshList()
        {
            string key = (txtSearch.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key))
                _filtered = new List<StudentPickVm>(_vms);
            else
                _filtered = _vms.Where(x =>
                    x.Name.Contains(key) ||
                    x.StudentId.Contains(key))
                    .ToList();

            _isLoading = true;
            icStudents.ItemsSource = null;
            icStudents.ItemsSource = _filtered;
            _isLoading = false;
        }

        // ═══════════════════════════════════════
        // 按钮
        // ═══════════════════════════════════════

        private void BtnClearSeat_Click(object sender, RoutedEventArgs e)
        {
            SelectedStudentId = "";
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vms.FirstOrDefault(x => x.IsSelected);
            if (selected == null)
            {
                tbTip.Text = "⚠️ 未选择学生（点「清除此座位」清空）";
                return;
            }

            SelectedStudentId = selected.Id;
            DialogResult = true;
            Close();
        }
    }

    internal class StudentPickVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public string Name { get; set; } = "";
        public string StudentId { get; set; } = "";
        public bool IsSelected { get; set; }
        public Student Student { get; set; } = new Student();
    }
}