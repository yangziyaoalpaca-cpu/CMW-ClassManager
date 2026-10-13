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

        // ★ 28.0.0：左侧课表常驻窗口
        public static LeftScheduleWindow? LeftSchedule { get; set; }

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

        public static bool IsBoardActive => Board != null && Board.IsLoaded;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CrashLogger.Attach();

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

            AppConfig? config = null;
            try
            {
                config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                AutoStartHelper.SyncWithConfig(config.AutoStart);
            }
            catch { }

            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            // 右侧窗口
            if (config != null && config.FloatWindowEnabled)
            {
                try
                {
                    Float = new FloatWindow();
                    Float.Show();
                }
                catch { }
            }

            // ★ 28.0.0：左侧课表常驻窗口
            if (config != null && config.LeftScheduleEnabled)
            {
                try
                {
                    LeftSchedule = new LeftScheduleWindow();
                    LeftSchedule.Show();
                }
                catch { }
            }
        }

        /// <summary>
        /// ★ 28.0.0：确保左侧课表存在并显示
        /// </summary>
        public static void EnsureLeftSchedule()
        {
            try
            {
                if (LeftSchedule == null || !LeftSchedule.IsLoaded)
                {
                    LeftSchedule = new LeftScheduleWindow();
                    LeftSchedule.Show();
                }
                else if (!LeftSchedule.IsVisible)
                {
                    LeftSchedule.Show();
                }
                else
                {
                    LeftSchedule.Activate();
                }
            }
            catch { }
        }

        /// <summary>
        /// ★ 28.0.0：关闭左侧课表
        /// </summary>
        public static void CloseLeftSchedule()
        {
            try { LeftSchedule?.Close(); }
            catch { }
            LeftSchedule = null;
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