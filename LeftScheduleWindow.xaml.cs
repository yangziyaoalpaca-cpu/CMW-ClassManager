using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CMW
{
    /// <summary>
    /// ★ 28.0.0：左侧课表常驻窗口
    /// 竖向排版，显示当天的星期 + 上午 4 节 + 下午 4 节 + 晚自习
    /// 每节课显示课程简称（可在设置页配置），超宽自动缩字体
    /// </summary>
    public partial class LeftScheduleWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;
        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");

        public LeftScheduleWindow()
        {
            InitializeComponent();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _refreshTimer.Tick += (s, e) => RefreshContent();

            Loaded += LeftScheduleWindow_Loaded;
            Closed += LeftScheduleWindow_Closed;

            App.SettingsChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplySizeAndPosition();
                RefreshContent();
            }), DispatcherPriority.Background);
        }

        private void LeftScheduleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySizeAndPosition();
            RefreshContent();
            _refreshTimer.Start();
        }

        private void LeftScheduleWindow_Closed(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            App.SettingsChanged -= OnSettingsChanged;
        }

        /// <summary>
        /// ★ 28.0.0：关闭按钮 → 关闭窗口 + 保存设置
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                cfg.LeftScheduleEnabled = false;
                JsonHelper.Save(AppPaths.Config, cfg);
            }
            catch { }

            Close();
        }

        private void ApplySizeAndPosition()
        {
            try
            {
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                var wa = SystemParameters.WorkArea;

                double w = config.LeftScheduleWidth;
                if (w < 60) w = 90;
                if (w > 200) w = 200;

                double h = wa.Width * 0.6;
                if (h > wa.Height - 40) h = wa.Height - 40;
                if (h < 200) h = 200;

                SetCurrentValue(WidthProperty, w);
                SetCurrentValue(HeightProperty, h);
                SetCurrentValue(LeftProperty, wa.Left);
                SetCurrentValue(TopProperty, wa.Top + (wa.Height - h) / 2);
            }
            catch { }
        }

        private void RefreshContent()
        {
            try
            {
                ContentPanel.Children.Clear();

                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule) ?? new ScheduleData();
                var slots = config.TimeSlots;
                if (slots == null || slots.Count == 0)
                    slots = DefaultTimeSlots.Create();

                DateTime today = DateTime.Today;
                int weekDay = (int)today.DayOfWeek;

                string[] weekNames = { "日", "一", "二", "三", "四", "五", "六" };

                if (weekDay < 1 || weekDay > 6)
                {
                    AddLine(weekNames[weekDay], 28);
                    AddLine("休", 24);
                    return;
                }

                string todayStr = today.ToString("yyyy-MM-dd");

                int effectiveWeekDay = weekDay;
                if (schedule.DayOverrides != null)
                {
                    var dayOv = schedule.DayOverrides.FirstOrDefault(x => x.Date == todayStr);
                    if (dayOv != null && dayOv.SourceWeekDay >= 1 && dayOv.SourceWeekDay <= 6)
                        effectiveWeekDay = dayOv.SourceWeekDay;
                }

                AddLine(weekNames[weekDay], 28);

                for (int i = 0; i < slots.Count; i++)
                {
                    string slotName = slots[i].Name ?? "";
                    if (!IsDisplaySlot(slotName)) continue;

                    int period = i + 1;

                    var ov = schedule.Overrides?.FirstOrDefault(x =>
                        x.Date == todayStr && x.Period == period && x.WeekDay == weekDay);
                    string lesson = ov?.LessonName ?? "";

                    if (string.IsNullOrWhiteSpace(lesson))
                    {
                        var item = schedule.WeekBase?.FirstOrDefault(x =>
                            x.WeekDay == effectiveWeekDay && x.Period == period);
                        lesson = item?.LessonName ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(lesson))
                    {
                        AddLine("·", 20);
                    }
                    else
                    {
                        string display = lesson;
                        if (config.LeftScheduleSubjectMap != null &&
                            config.LeftScheduleSubjectMap.TryGetValue(lesson, out string? shortName) &&
                            !string.IsNullOrWhiteSpace(shortName))
                        {
                            display = shortName;
                        }

                        AddLine(display, 24);
                    }
                }
            }
            catch { }
        }

        private static bool IsDisplaySlot(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            if (name.Contains("晚自习")) return true;

            if (name.StartsWith("第") && name.EndsWith("节"))
            {
                string mid = name.Substring(1, name.Length - 2);
                if (int.TryParse(mid, out int n) && n >= 1 && n <= 8)
                    return true;
            }

            return false;
        }

        private void AddLine(string text, double baseSize)
        {
            double maxWidth = ActualWidth - 12;
            if (maxWidth < 20) maxWidth = 60;

            double size = GetAdaptiveFontSize(text, maxWidth, baseSize);

            var tb = new TextBlock
            {
                Text = text,
                FontFamily = UiFont,
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 3, 0, 3)
            };

            ContentPanel.Children.Add(tb);
        }

        private static double GetAdaptiveFontSize(string text, double maxWidth, double baseSize)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return baseSize;

                var typeface = new Typeface(UiFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

                for (double size = baseSize; size >= 8; size -= 0.5)
                {
                    var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        typeface, size, Brushes.White, 1.0);
                    if (ft.Width <= maxWidth)
                        return size;
                }
                return 8;
            }
            catch { return baseSize; }
        }
    }
}