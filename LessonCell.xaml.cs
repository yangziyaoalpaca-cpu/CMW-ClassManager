using System.Windows;
using System.Windows.Controls;

namespace CMW
{
    public partial class LessonCell : UserControl
    {
        public string LessonName
        {
            get => (string)GetValue(LessonNameProperty);
            set => SetValue(LessonNameProperty, value);
        }

        public static readonly DependencyProperty LessonNameProperty =
            DependencyProperty.Register(
                nameof(LessonName), typeof(string), typeof(LessonCell),
                new PropertyMetadata(string.Empty, OnLessonNameChanged));

        public string TeacherName
        {
            get => (string)GetValue(TeacherNameProperty);
            set => SetValue(TeacherNameProperty, value);
        }

        public static readonly DependencyProperty TeacherNameProperty =
            DependencyProperty.Register(
                nameof(TeacherName), typeof(string), typeof(LessonCell),
                new PropertyMetadata(string.Empty, OnTeacherNameChanged));

        public string PeriodText
        {
            get => (string)GetValue(PeriodTextProperty);
            set => SetValue(PeriodTextProperty, value);
        }

        public static readonly DependencyProperty PeriodTextProperty =
            DependencyProperty.Register(
                nameof(PeriodText), typeof(string), typeof(LessonCell),
                new PropertyMetadata(string.Empty));

        public LessonCell()
        {
            InitializeComponent();
            UpdateWatermarks();
        }

        private static void OnLessonNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LessonCell cell && cell.tbCourse != null)
            {
                string val = e.NewValue as string ?? "";
                if (cell.tbCourse.Text != val)
                    cell.tbCourse.Text = val;
            }
        }

        private static void OnTeacherNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LessonCell cell && cell.tbTeacher != null)
            {
                string val = e.NewValue as string ?? "";
                if (cell.tbTeacher.Text != val)
                    cell.tbTeacher.Text = val;
            }
        }

        private void TbCourse_TextChanged(object sender, TextChangedEventArgs e)
        {
            WatermarkCourse.Visibility = string.IsNullOrEmpty(tbCourse.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            if (LessonName != tbCourse.Text)
                LessonName = tbCourse.Text;
        }

        private void TbTeacher_TextChanged(object sender, TextChangedEventArgs e)
        {
            WatermarkTeacher.Visibility = string.IsNullOrEmpty(tbTeacher.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            if (TeacherName != tbTeacher.Text)
                TeacherName = tbTeacher.Text;
        }

        private void UpdateWatermarks()
        {
            WatermarkCourse.Visibility = string.IsNullOrEmpty(tbCourse.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            WatermarkTeacher.Visibility = string.IsNullOrEmpty(tbTeacher.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}