using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CMW
{
    public partial class NotesViewerWindow : Window
    {
        private List<NoteRowVm> _rows = new List<NoteRowVm>();
        private NoteRowVm? _selected;

        public NotesViewerWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshList();
        }

        // ═══════════════════════════════════════
        // 列表
        // ═══════════════════════════════════════

        private void RefreshList()
        {
            try
            {
                var notes = NoteService.GetAll();

                _rows = notes.Select(n => new NoteRowVm
                {
                    Note = n,
                    DisplayText = n.DisplayText,
                    CreatedText = n.CreatedText,
                    ThumbSource = LoadThumbSafe(n),
                    NoImageVisibility = n.HasImage ? Visibility.Collapsed : Visibility.Visible,
                    IsSelected = false
                }).ToList();

                icNotes.ItemsSource = null;
                icNotes.ItemsSource = _rows;

                tbSubtitle.Text = _rows.Count == 0
                    ? "截图笔记与文字笔记列表"
                    : $"共 {_rows.Count} 条笔记";

                tbEmpty.Visibility = _rows.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                // 默认选第一条
                if (_rows.Count > 0)
                    SelectRow(_rows[0]);
                else
                    ClearPreview();
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 加载失败：" + ex.Message;
            }
        }

        private static BitmapImage? LoadThumbSafe(NoteItem note)
        {
            try
            {
                if (!note.HasImage) return null;

                string path = NoteService.GetThumbPath(note);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private static BitmapImage? LoadImageSafe(NoteItem note)
        {
            try
            {
                if (!note.HasImage) return null;

                string path = NoteService.GetImagePath(note);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        // ═══════════════════════════════════════
        // 选中
        // ═══════════════════════════════════════

        private void NoteRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not NoteRowVm vm) return;

            SelectRow(vm);
            e.Handled = true;
        }

        private void SelectRow(NoteRowVm vm)
        {
            foreach (var r in _rows)
                r.IsSelected = (r == vm);

            _selected = vm;

            // 刷新选中高亮
            icNotes.ItemsSource = null;
            icNotes.ItemsSource = _rows;

            ShowPreview(vm);
        }

        private void ShowPreview(NoteRowVm vm)
        {
            tbPreviewTitle.Text = $"笔记 · {vm.CreatedText}";

            // 大图
            var img = LoadImageSafe(vm.Note);
            if (img != null)
            {
                PreviewImage.Source = img;
                ImageCard.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewImage.Source = null;
                ImageCard.Visibility = Visibility.Collapsed;
            }

            // 文字
            string text = vm.Note.Content ?? "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                PreviewText.Text = text;
                TextCard.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewText.Text = "";
                TextCard.Visibility = Visibility.Collapsed;
            }

            tbNoContent.Visibility = (img == null && string.IsNullOrWhiteSpace(text))
                ? Visibility.Visible : Visibility.Collapsed;

            tbTip.Text = "";
        }

        private void ClearPreview()
        {
            _selected = null;
            PreviewImage.Source = null;
            ImageCard.Visibility = Visibility.Collapsed;
            TextCard.Visibility = Visibility.Collapsed;
            tbNoContent.Visibility = Visibility.Collapsed;
            tbPreviewTitle.Text = "选择左侧笔记查看详情";
        }

        // ═══════════════════════════════════════
        // 删除
        // ═══════════════════════════════════════

        private void BtnDeleteCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                tbTip.Text = "⚠️ 请先在左侧选择一条笔记";
                return;
            }

            if (MessageBox.Show(
                "确定删除这条笔记？图片也会一并删除，不可恢复。",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            bool ok = NoteService.Delete(_selected.Note.Id);

            if (ok)
            {
                tbTip.Text = "✅ 已删除";
                RefreshList();
            }
            else
            {
                tbTip.Text = "⚠️ 删除失败";
            }
        }

        // ═══════════════════════════════════════
        // 打开目录
        // ═══════════════════════════════════════

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(AppPaths.NotesImagesDir))
                    Directory.CreateDirectory(AppPaths.NotesImagesDir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.NotesImagesDir,
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
        // 关闭
        // ═══════════════════════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // ═══════ ViewModel ═══════

    internal class NoteRowVm : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public NoteItem Note { get; set; } = new NoteItem();
        public string DisplayText { get; set; } = "";
        public string CreatedText { get; set; } = "";
        public BitmapImage? ThumbSource { get; set; }
        public Visibility NoImageVisibility { get; set; } = Visibility.Visible;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this,
                    new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}