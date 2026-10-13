﻿using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CMW
{
    public partial class ScreenshotOverlayWindow : Window
    {
        // 物理全屏截图（外部传入）
        private readonly Bitmap? _fullScreenBitmap;

        // DPI 缩放（物理 / 逻辑）
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        // 拖选状态（逻辑坐标）
        private bool _isDragging;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _currentPoint;
        private bool _hasSelection;

        /// <summary>裁剪结果（物理像素）；取消时为 null</summary>
        public Bitmap? CroppedBitmap { get; private set; }

        /// <summary>
        /// ★ 由外部传入已抓好的全屏截图
        /// </summary>
        public ScreenshotOverlayWindow(Bitmap? fullScreen)
        {
            InitializeComponent();

            _fullScreenBitmap = fullScreen;

            Loaded += ScreenshotOverlayWindow_Loaded;

            // 鼠标
            PreviewMouseLeftButtonDown += OnPointerDown;
            PreviewMouseMove += OnPointerMove;
            PreviewMouseLeftButtonUp += OnPointerUp;

            // 触摸
            PreviewTouchDown += OnTouchDown;
            PreviewTouchMove += OnTouchMove;
            PreviewTouchUp += OnTouchUp;

            // 键盘
            PreviewKeyDown += OnKeyDown;
        }

        // ═══════════════════════════════════════
        // 初始化
        // ═══════════════════════════════════════

        private void ScreenshotOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null || _fullScreenBitmap == null)
            {
                DialogResult = false;
                Close();
                return;
            }

            var bounds = screen.Bounds;

            double logicalW = SystemParameters.PrimaryScreenWidth;
            double logicalH = SystemParameters.PrimaryScreenHeight;

            _dpiScaleX = bounds.Width / logicalW;
            _dpiScaleY = bounds.Height / logicalH;

            SetCurrentValue(LeftProperty, bounds.Left / _dpiScaleX);
            SetCurrentValue(TopProperty, bounds.Top / _dpiScaleY);
            SetCurrentValue(WidthProperty, logicalW);
            SetCurrentValue(HeightProperty, logicalH);

            // 显示传入的图
            ScreenImage.Source = BitmapToImageSource(_fullScreenBitmap);

            // 初始全屏灰遮罩
            UpdateMask(new Rect(0, 0, logicalW, logicalH), false);
        }

        // ═══════════════════════════════════════
        // 指针（鼠标）
        // ═══════════════════════════════════════

        private void OnPointerDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInToolbar(e.OriginalSource)) return;

            _startPoint = e.GetPosition(RootGrid);
            _currentPoint = _startPoint;
            _isDragging = true;
            _hasSelection = false;

            Toolbar.Visibility = Visibility.Collapsed;
            HintBorder.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeHintBorder.Visibility = Visibility.Visible;

            UpdateSelection(_startPoint, _startPoint);

            e.Handled = true;
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            _currentPoint = e.GetPosition(RootGrid);
            UpdateSelection(_startPoint, _currentPoint);

            e.Handled = true;
        }

        private void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            _currentPoint = e.GetPosition(RootGrid);
            UpdateSelection(_startPoint, _currentPoint);

            var rect = GetSelectionRect();
            if (rect.Width < 5 || rect.Height < 5)
            {
                _hasSelection = false;
                SelectionBorder.Visibility = Visibility.Collapsed;
                SizeHintBorder.Visibility = Visibility.Collapsed;
                Toolbar.Visibility = Visibility.Collapsed;
                HintBorder.Visibility = Visibility.Visible;
                UpdateMask(new Rect(0, 0, ActualWidth, ActualHeight), false);
                return;
            }

            _hasSelection = true;
            PositionToolbar(rect);

            e.Handled = true;
        }

        // ═══════════════════════════════════════
        // 指针（触摸）
        // ═══════════════════════════════════════

        private void OnTouchDown(object? sender, TouchEventArgs e)
        {
            if (IsInToolbar(e.OriginalSource)) return;

            _startPoint = e.GetTouchPoint(RootGrid).Position;
            _currentPoint = _startPoint;
            _isDragging = true;
            _hasSelection = false;

            Toolbar.Visibility = Visibility.Collapsed;
            HintBorder.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Visible;
            SizeHintBorder.Visibility = Visibility.Visible;

            UpdateSelection(_startPoint, _startPoint);

            e.Handled = true;
        }

        private void OnTouchMove(object? sender, TouchEventArgs e)
        {
            if (!_isDragging) return;

            _currentPoint = e.GetTouchPoint(RootGrid).Position;
            UpdateSelection(_startPoint, _currentPoint);

            e.Handled = true;
        }

        private void OnTouchUp(object? sender, TouchEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            _currentPoint = e.GetTouchPoint(RootGrid).Position;
            UpdateSelection(_startPoint, _currentPoint);

            var rect = GetSelectionRect();
            if (rect.Width < 5 || rect.Height < 5)
            {
                _hasSelection = false;
                SelectionBorder.Visibility = Visibility.Collapsed;
                SizeHintBorder.Visibility = Visibility.Collapsed;
                Toolbar.Visibility = Visibility.Collapsed;
                HintBorder.Visibility = Visibility.Visible;
                UpdateMask(new Rect(0, 0, ActualWidth, ActualHeight), false);
                return;
            }

            _hasSelection = true;
            PositionToolbar(rect);

            e.Handled = true;
        }

        // ═══════════════════════════════════════
        // 计算选区 / 更新视觉
        // ═══════════════════════════════════════

        private Rect GetSelectionRect()
        {
            double x = Math.Min(_startPoint.X, _currentPoint.X);
            double y = Math.Min(_startPoint.Y, _currentPoint.Y);
            double w = Math.Abs(_currentPoint.X - _startPoint.X);
            double h = Math.Abs(_currentPoint.Y - _startPoint.Y);
            return new Rect(x, y, w, h);
        }

        private void UpdateSelection(System.Windows.Point start, System.Windows.Point cur)
        {
            double x = Math.Min(start.X, cur.X);
            double y = Math.Min(start.Y, cur.Y);
            double w = Math.Abs(cur.X - start.X);
            double h = Math.Abs(cur.Y - start.Y);

            double sw = ActualWidth;
            double sh = ActualHeight;
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > sw) w = sw - x;
            if (y + h > sh) h = sh - y;

            if (w < 0) w = 0;
            if (h < 0) h = 0;

            SelectionBorder.Width = w;
            SelectionBorder.Height = h;
            SelectionBorder.Margin = new Thickness(x, y, 0, 0);
            SelectionBorder.HorizontalAlignment = HorizontalAlignment.Left;
            SelectionBorder.VerticalAlignment = VerticalAlignment.Top;

            SizeHintText.Text = $"{(int)Math.Round(w * _dpiScaleX)} × {(int)Math.Round(h * _dpiScaleY)}";
            double hintLeft = x + w - 60;
            double hintTop = y + h + 4;
            if (hintLeft < 0) hintLeft = 0;
            if (hintTop + 24 > sh) hintTop = y + h - 26;
            SizeHintBorder.Margin = new Thickness(hintLeft, hintTop, 0, 0);
            SizeHintBorder.HorizontalAlignment = HorizontalAlignment.Left;
            SizeHintBorder.VerticalAlignment = VerticalAlignment.Top;
            SizeHintBorder.Opacity = (w > 0 && h > 0) ? 1 : 0;

            UpdateMask(new Rect(x, y, w, h), w > 0 && h > 0);
        }

        private void UpdateMask(Rect selection, bool cutHole)
        {
            var fullScreen = new RectangleGeometry(
                new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)));

            if (!cutHole || selection.Width <= 0 || selection.Height <= 0)
            {
                MaskPath.Data = fullScreen;
                return;
            }

            var result = new CombinedGeometry(
                GeometryCombineMode.Exclude, fullScreen,
                new RectangleGeometry(selection));

            MaskPath.Data = result;
        }

        private void PositionToolbar(Rect selection)
        {
            Toolbar.Visibility = Visibility.Visible;
            Toolbar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var tbSize = Toolbar.DesiredSize;

            double tbLeft = selection.Right - tbSize.Width;
            double tbTop = selection.Top - tbSize.Height - 8;

            if (tbTop < 0)
                tbTop = selection.Bottom + 8;

            if (tbLeft < 0) tbLeft = 0;
            if (tbLeft + tbSize.Width > ActualWidth)
                tbLeft = ActualWidth - tbSize.Width;
            if (tbTop + tbSize.Height > ActualHeight)
                tbTop = ActualHeight - tbSize.Height;

            Toolbar.Margin = new Thickness(tbLeft, tbTop, 0, 0);
            Toolbar.HorizontalAlignment = HorizontalAlignment.Left;
            Toolbar.VerticalAlignment = VerticalAlignment.Top;
        }

        private static bool IsInToolbar(object? originalSource)
        {
            if (originalSource is not DependencyObject d) return false;
            DependencyObject? cur = d;
            while (cur != null)
            {
                if (cur is StackPanel sp && sp.Name == "Toolbar") return true;
                cur = VisualTreeHelper.GetParent(cur);
            }
            return false;
        }

        // ═══════════════════════════════════════
        // 键盘
        // ═══════════════════════════════════════

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter && _hasSelection)
            {
                DoCrop();
            }
        }

        // ═══════════════════════════════════════
        // 完成 / 取消
        // ═══════════════════════════════════════

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DoCrop();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DoCrop()
        {
            if (_fullScreenBitmap == null || !_hasSelection)
            {
                DialogResult = false;
                Close();
                return;
            }

            var rect = GetSelectionRect();

            int px = (int)Math.Round(rect.X * _dpiScaleX);
            int py = (int)Math.Round(rect.Y * _dpiScaleY);
            int pw = (int)Math.Round(rect.Width * _dpiScaleX);
            int ph = (int)Math.Round(rect.Height * _dpiScaleY);

            if (pw <= 0 || ph <= 0)
            {
                DialogResult = false;
                Close();
                return;
            }

            var physRect = new Rectangle(px, py, pw, ph);
            CroppedBitmap = ScreenshotService.CropBitmap(_fullScreenBitmap, physRect);

            DialogResult = CroppedBitmap != null;
            Close();
        }

        // ═══════════════════════════════════════
        // Bitmap → ImageSource
        // ═══════════════════════════════════════

        private static BitmapImage BitmapToImageSource(Bitmap bmp)
        {
            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }
}