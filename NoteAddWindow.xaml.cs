﻿using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CMW
{
    public partial class NoteAddWindow : Window
    {
        private readonly Bitmap? _cropped;

        /// <summary>
        /// 传入截图（可空）。
        /// 生命周期由本窗口负责，OnClosed 里 Dispose。
        /// </summary>
        public NoteAddWindow(Bitmap? cropped)
        {
            InitializeComponent();

            _cropped = cropped;

            if (cropped != null)
            {
                try
                {
                    PreviewImage.Source = BitmapToImageSource(cropped);
                    PreviewImage.Visibility = Visibility.Visible;
                    EmptyHint.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    PreviewImage.Visibility = Visibility.Collapsed;
                    EmptyHint.Visibility = Visibility.Visible;
                }
            }
            else
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                EmptyHint.Visibility = Visibility.Visible;
            }

            Loaded += (s, e) => txtContent.Focus();
        }

        // ═══════════════════════════════════════
        // 顶栏拖动 / 关闭
        // ═══════════════════════════════════════

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═══════════════════════════════════════
        // 保存
        // ═══════════════════════════════════════

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string text = (txtContent.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text) && _cropped == null)
            {
                tbTip.Text = "⚠️ 请输入文字或截图后再保存";
                return;
            }

            try
            {
                NoteService.Add(text, _cropped);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        private void BtnSaveImageOnly_Click(object sender, RoutedEventArgs e)
        {
            if (_cropped == null)
            {
                tbTip.Text = "⚠️ 没有截图，无法只存图";
                return;
            }

            try
            {
                NoteService.Add("", _cropped);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ═══════════════════════════════════════
        // 清理
        // ═══════════════════════════════════════

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _cropped?.Dispose();
            }
            catch { }

            base.OnClosed(e);
        }

        // ═══════════════════════════════════════
        // Bitmap → ImageSource
        // ═══════════════════════════════════════

        private static BitmapImage BitmapToImageSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
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