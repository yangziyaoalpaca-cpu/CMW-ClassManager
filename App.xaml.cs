using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CMW
{
    public partial class App : Application
    {
        public static IslandWindow? Island { get; set; }
        public static FloatWindow? Float { get; set; }
        public static ClassBoardWindow? Board { get; set; }

        public static event Action? SettingsChanged;
        public static void NotifySettingsChanged() => SettingsChanged?.Invoke();

        public static event Action? TimeSlotsChanged;
        public static void NotifyTimeSlotsChanged() => TimeSlotsChanged?.Invoke();

        public static event Action<double>? BlurRadiusChanged;
        public static void NotifyBlurRadiusChanged(double radius) => BlurRadiusChanged?.Invoke(radius);

        public static event Action? RemindConfigChanged;
        public static void NotifyRemindConfigChanged() => RemindConfigChanged?.Invoke();

        public static event Action? BoardStateChanged;
        public static void NotifyBoardStateChanged() => BoardStateChanged?.Invoke();

        public static event Action? ThemeChanged;
        public static void NotifyThemeChanged() => ThemeChanged?.Invoke();

        public static bool IsBoardActive => Board != null && Board.IsLoaded;

        public static string CurrentFontFamily { get; private set; } = "Microsoft YaHei UI";
        public static string CurrentThemeName { get; private set; } = "PCL2";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CrashLogger.Attach();

            AppConfig? bootConfig = null;
            try
            {
                bootConfig = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                ApplyTheme(bootConfig.ThemeName);
                ApplyFontToApp(bootConfig.FontFamily);
            }
            catch { }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { BackupService.RunDailyBackupIfNeeded(); }
                catch { }
            }), DispatcherPriority.Background);

            try
            {
                Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(Timeline),
                    new FrameworkPropertyMetadata(60));
                RenderOptions.ProcessRenderMode = RenderMode.Default;
            }
            catch { }

            TryMigrateHomeworkJson();

            try { DutyRotateService.TryRotateIfNeeded(); }
            catch { }

            AppConfig? config = bootConfig;
            try
            {
                if (config == null)
                    config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                AutoStartHelper.SyncWithConfig(config.AutoStart);
            }
            catch { }

            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            if (config != null && config.FloatWindowEnabled)
            {
                try
                {
                    Float = new FloatWindow();
                    Float.Show();
                }
                catch { }
            }
        }

        /// <summary>
        /// 应用主题：加载对应的主题资源字典
        /// </summary>
        public static void ApplyTheme(string? themeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(themeName))
                    themeName = "PCL2";

                if (themeName != "PCL2" && themeName != "ClassIsland")
                    themeName = "PCL2";

                CurrentThemeName = themeName;

                string file = themeName == "ClassIsland"
                    ? "Theme.ClassIsland.xaml"
                    : "Theme.PCL2.xaml";

                var uri = new Uri($"pack://application:,,,/{file}", UriKind.Absolute);
                var newDict = new ResourceDictionary { Source = uri };

                var merged = Current.Resources.MergedDictionaries;

                ResourceDictionary? oldTheme = null;
                foreach (var d in merged)
                {
                    if (d.Source != null &&
                        d.Source.OriginalString.Contains("Theme."))
                    {
                        oldTheme = d;
                        break;
                    }
                }

                if (oldTheme != null)
                    merged.Remove(oldTheme);

                merged.Add(newDict);

                NotifyThemeChanged();
            }
            catch { }
        }

        /// <summary>
        /// 应用字体到全局资源
        /// </summary>
        public static void ApplyFontToApp(string? fontName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fontName))
                    fontName = "Microsoft YaHei UI";

                CurrentFontFamily = fontName;

                var newFont = new FontFamily(fontName);

                Current.Resources["GlobalFontFamily"] = newFont;
                Current.Resources["AppleFont"] = newFont;
            }
            catch { }
        }

        public static void ExitApp() => Current.Shutdown();

        private static void TryMigrateHomeworkJson()
        {
            string path = AppPaths.Homework;
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;
                if (!json.TrimStart().StartsWith("[")) return;

                var arr = JArray.Parse(json);
                bool migrated = false;

                foreach (var item in arr)
                {
                    if (item["StartDate"] != null && item["EndDate"] != null) continue;

                    var deadlineToken = item["Deadline"];
                    if (deadlineToken == null) continue;

                    string deadlineValue = deadlineToken.ToString();
                    item["StartDate"] = deadlineValue;
                    item["EndDate"] = deadlineValue;

                    if (item is JObject obj)
                    {
                        obj.Remove("Deadline");
                    }
                    migrated = true;
                }

                if (migrated)
                {
                    File.WriteAllText(path, arr.ToString(Formatting.Indented));
                    JsonHelper.ClearCache();
                }
            }
            catch { }
        }
    }
}