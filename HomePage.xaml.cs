﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CMW
{
    public partial class HomePage : Page
    {
        private AppConfig _config;
        private bool _isLoading;

        public HomePage()
        {
            InitializeComponent();
            _config = new AppConfig();
            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                ClassNameTextBox.Text = _config.ClassName;

                LoadCustomBackground();
                RefreshStatCards();
                RefreshExamCountdown();

                App.SettingsChanged -= OnSettingsChanged;
                App.SettingsChanged += OnSettingsChanged;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                ClassNameTextBox.Text = _config.ClassName;
                LoadCustomBackground();
                RefreshStatCards();
                RefreshExamCountdown();
            }), DispatcherPriority.Background);
        }

        // ═══════════════════════════════════════
        // 背景加载
        // ═══════════════════════════════════════

        private void LoadCustomBackground()
        {
            try
            {
                string? customPath = _config.BackgroundImagePath;
                if (string.IsNullOrEmpty(customPath) && File.Exists(AppPaths.CustomBackgroundFile))
                    customPath = AppPaths.CustomBackgroundFile;

                bool hasCustom = !string.IsNullOrEmpty(customPath) && File.Exists(customPath);

                if (_config.BackgroundBlurEnabled)
                {
                    if (File.Exists(AppPaths.CustomBackgroundBlurredFile))
                    {
                        BgImage.Source = LoadBitmapFromFile(AppPaths.CustomBackgroundBlurredFile);
                    }
                    else
                    {
                        BgImage.Source = hasCustom
                            ? LoadBitmapFromFile(customPath!)
                            : LoadBitmapFromPack();

                        Dispatcher.BeginInvoke(new Action(GenerateAndReload),
                            DispatcherPriority.Background);
                    }
                }
                else
                {
                    BgImage.Source = hasCustom
                        ? LoadBitmapFromFile(customPath!)
                        : LoadBitmapFromPack();
                }
            }
            catch { }
        }

        private void GenerateAndReload()
        {
            GenerateBlurredBackground();

            if (_config.BackgroundBlurEnabled
                && File.Exists(AppPaths.CustomBackgroundBlurredFile))
            {
                try
                {
                    BgImage.Source = LoadBitmapFromFile(AppPaths.CustomBackgroundBlurredFile);
                }
                catch { }
            }
        }

        public static void GenerateBlurredBackground()
        {
            try
            {
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();

                string? customPath = config.BackgroundImagePath;
                if (string.IsNullOrEmpty(customPath) && File.Exists(AppPaths.CustomBackgroundFile))
                    customPath = AppPaths.CustomBackgroundFile;

                BitmapImage bmp;
                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                    bmp = LoadBitmapFromFile(customPath);
                else
                    bmp = LoadBitmapFromPack();

                var img = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.Fill,
                    Effect = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = config.BlurRadius,
                        KernelType = System.Windows.Media.Effects.KernelType.Gaussian
                    }
                };
                img.Measure(new Size(bmp.PixelWidth, bmp.PixelHeight));
                img.Arrange(new Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight));
                img.UpdateLayout();

                var rtb = new RenderTargetBitmap(
                    bmp.PixelWidth, bmp.PixelHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(img);

                if (!Directory.Exists(AppPaths.CustomBackgroundDir))
                    Directory.CreateDirectory(AppPaths.CustomBackgroundDir);

                var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string tempPath = AppPaths.CustomBackgroundBlurredFile + ".tmp";
                using (var fs = new FileStream(tempPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                if (File.Exists(AppPaths.CustomBackgroundBlurredFile))
                    File.Delete(AppPaths.CustomBackgroundBlurredFile);
                File.Move(tempPath, AppPaths.CustomBackgroundBlurredFile);
            }
            catch { }
        }

        private static BitmapImage LoadBitmapFromFile(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }

        private static BitmapImage LoadBitmapFromPack()
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri("pack://application:,,,/Assets/Background.jpg");
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }

        // ═══════════════════════════════════════
        // ★ 27.0.0：数据统计卡片
        // ═══════════════════════════════════════

        private void RefreshStatCards()
        {
            try
            {
                var hwList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();
                var today = DateTime.Today;
                int hwCount = hwList.Count(h => h.IsActiveOn(today) && !h.IsCompleted);
                tbStatHomework.Text = hwCount.ToString();

                var tdList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();
                int tdCount = tdList.Count(t => t.Deadline.HasValue
                    && t.Deadline.Value.Date == today
                    && !t.IsCompleted);
                tbStatTodo.Text = tdCount.ToString();

                string dutyName = DutyPage.GetTodayDutyGroupName();
                tbStatDuty.Text = string.IsNullOrWhiteSpace(dutyName) ? "未安排" : dutyName;

                tbStatNextClass.Text = GetNextClassText();
            }
            catch { }
        }

        private string GetNextClassText()
        {
            try
            {
                DateTime now = DateTime.Now;
                int weekDay = (int)now.DayOfWeek;
                if (weekDay < 1 || weekDay > 6) return "今日结束";

                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule)
                    ?? new ScheduleData();
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                var slots = config.TimeSlots;
                if (slots == null || slots.Count == 0)
                    slots = DefaultTimeSlots.Create();

                for (int i = 0; i < slots.Count; i++)
                {
                    if (!TimeSpan.TryParse(slots[i].StartTime, out var s)) continue;
                    var startTime = now.Date.Add(s);
                    if (startTime <= now) continue;

                    int period = i + 1;
                    string today = now.ToString("yyyy-MM-dd");

                    var ov = schedule.Overrides.FirstOrDefault(x =>
                        x.Date == today && x.Period == period && x.WeekDay == weekDay);
                    string lesson = ov?.LessonName ?? "";
                    if (string.IsNullOrWhiteSpace(lesson))
                    {
                        var item = schedule.WeekBase.FirstOrDefault(x =>
                            x.WeekDay == weekDay && x.Period == period);
                        lesson = item?.LessonName ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(lesson))
                        return lesson;
                }

                return "今日结束";
            }
            catch { return "--"; }
        }

        // ★ 27.0.0：考试倒计时
        private void RefreshExamCountdown()
        {
            try
            {
                string name = (_config.ExamName ?? "").Trim();
                string dateStr = (_config.ExamDate ?? "").Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dateStr))
                {
                    ExamCountdownCard.Visibility = Visibility.Collapsed;
                    return;
                }

                if (!DateTime.TryParse(dateStr, out var examDate))
                {
                    ExamCountdownCard.Visibility = Visibility.Collapsed;
                    return;
                }

                int days = (examDate.Date - DateTime.Today).Days;

                tbExamName.Text = name;
                tbExamDays.Text = days > 0 ? days.ToString() : "0";

                ExamCountdownCard.Visibility = Visibility.Visible;
            }
            catch
            {
                ExamCountdownCard.Visibility = Visibility.Collapsed;
            }
        }

        // ★ 27.0.0：点击卡片跳转
        private void StatCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b || b.Tag is not string tag) return;

            int navIndex;
            switch (tag)
            {
                case "Homework": navIndex = 4; break;
                case "Todo": navIndex = 2; break;
                case "Duty": navIndex = 3; break;
                case "NextClass": navIndex = 1; break;
                default: return;
            }

            if (Window.GetWindow(this) is MainWindow main)
            {
                main.NavigateByIndex(navIndex);
            }
        }

        private void ClassNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            if (!IsLoaded) return;

            _config.ClassName = ClassNameTextBox.Text;
            JsonHelper.Save(AppPaths.Config, _config);
        }
    }
}