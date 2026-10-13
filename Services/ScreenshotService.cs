﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CMW
{
    /// <summary>
    /// ★ 27.2.0：屏幕截图
    /// 用 GDI+ 的 CopyFromScreen 抓物理像素
    /// </summary>
    public static class ScreenshotService
    {
        public static Bitmap? CaptureScreen()
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return null;

                var bounds = screen.Bounds;
                var bmp = new Bitmap(bounds.Width, bounds.Height);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top,
                        0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                return bmp;
            }
            catch { return null; }
        }

        public static Bitmap? CropBitmap(Bitmap source, Rectangle rect)
        {
            try
            {
                if (source == null) return null;
                if (rect.Width <= 0 || rect.Height <= 0) return null;

                int x = Math.Max(0, rect.X);
                int y = Math.Max(0, rect.Y);
                int r = Math.Min(source.Width, rect.Right);
                int b = Math.Min(source.Height, rect.Bottom);

                if (r <= x || b <= y) return null;

                var target = new Rectangle(x, y, r - x, b - y);
                var cropped = new Bitmap(target.Width, target.Height);

                using (var g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(source,
                        new Rectangle(0, 0, target.Width, target.Height),
                        target, GraphicsUnit.Pixel);
                }

                return cropped;
            }
            catch { return null; }
        }

        public static bool SaveJpg(Bitmap bmp, string path, long quality = 85L)
        {
            try
            {
                if (bmp == null) return false;

                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var encoder = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                if (encoder == null) return false;

                var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                bmp.Save(path, encoder, parameters);
                return true;
            }
            catch { return false; }
        }

        public static bool SaveThumbnail(Bitmap source, string path, int targetWidth = 400)
        {
            try
            {
                if (source == null) return false;
                if (targetWidth <= 0) targetWidth = 400;

                int srcW = source.Width;
                int srcH = source.Height;

                int thumbW = Math.Min(srcW, targetWidth);
                int thumbH = (int)Math.Round((double)srcH * thumbW / srcW);
                if (thumbH <= 0) thumbH = 1;

                using var thumb = new Bitmap(thumbW, thumbH);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, thumbW, thumbH);
                }

                return SaveJpg(thumb, path, 80L);
            }
            catch { return false; }
        }

        public static (int Width, int Height) GetScreenPhysicalSize()
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return (0, 0);
                return (screen.Bounds.Width, screen.Bounds.Height);
            }
            catch { return (0, 0); }
        }
    }
}