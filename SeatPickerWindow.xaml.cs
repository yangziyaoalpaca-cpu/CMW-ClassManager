using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CMW
{
    public partial class SeatPickerWindow : Window
    {
        private List<StudentPickVm> _vms = new List<StudentPickVm>();
        private List<StudentPickVm> _filtered = new List<StudentPickVm>();

        public string SelectedStudentId { get; private set; } = "";

        public SeatPickerWindow(List<Student> students, string currentStudentId, int row, int col)
        {
            InitializeComponent();

            tbSubtitle.Text = $"位置：第 {row + 1} 行 第 {col + 1} 列 · 点击学生姓名即可安排";

            _vms = students.Select(s => new StudentPickVm
            {
                Id = s.Id,
                DisplayText = s.DisplayText,
                Name = s.Name,
                StudentId = s.StudentId,
                IsSelected = s.Id == currentStudentId,
                SelectedCheckVisibility = s.Id == currentStudentId
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                Student = s
            }).ToList();

            _filtered = new List<StudentPickVm>(_vms);
            icStudents.ItemsSource = _filtered;

            if (_vms.Count == 0)
            {
                tbEmpty.Visibility = Visibility.Visible;
                tbTip.Text = "名单为空，请先在「名单」页添加学生";
            }
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

            tbEmpty.Visibility = _filtered.Count == 0 && _vms.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (_filtered.Count == 0 && _vms.Count > 0)
                tbEmpty.Text = "没有匹配的学生";
            else if (_vms.Count == 0)
                tbEmpty.Text = "名单为空，请先到「名单」页添加学生";
        }

        // ═══════════════════════════════════════
        // ★ 33.0.0：单击学生卡片 → 立即安排并关闭
        // ═══════════════════════════════════════

        private void StudentCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not StudentPickVm vm) return;

            SelectedStudentId = vm.Id;
            DialogResult = true;
            Close();
            e.Handled = true;
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
    }

    internal class StudentPickVm
    {
        public string Id { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public string Name { get; set; } = "";
        public string StudentId { get; set; } = "";
        public bool IsSelected { get; set; }
        public Visibility SelectedCheckVisibility { get; set; } = Visibility.Collapsed;
        public Student Student { get; set; } = new Student();
    }
}