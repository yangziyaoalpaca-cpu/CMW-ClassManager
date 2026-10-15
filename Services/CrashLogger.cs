using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CMW
{
    /// <summary>
    /// ★ 26.5.0：崩溃日志
    /// ★ 32.0.0：日志目录分类到 Logs/crash/，兼容旧路径自动迁移
    /// 捕获三类未处理异常：
    ///   1. UI 线程（DispatcherUnhandledException）
    ///   2. 后台线程（AppDomain.UnhandledException）
    ///   3. Task（TaskScheduler.UnobservedTaskException）
    /// 日志写入 Logs/crash/crash-yyyyMMdd-HHmmss.txt
    /// 保留最近 10 个日志文件
    /// </summary>
    public static class CrashLogger
    {
        private const int MaxLogs = 10;
        private const string LogFolderName = "Logs";
        private const string CrashSubFolderName = "crash";
        private const string LogFilePrefix = "crash-";

        private static bool _attached;

        /// <summary>
        /// 挂载全局异常处理（应在 OnStartup 最早期调用一次）
        /// </summary>
        public static void Attach()
        {
            if (_attached) return;
            _attached = true;

            TryMigrateLegacyLogs();

            if (Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += OnDispatcherException;
            }

            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;
        }

        private static string GetCrashDir()
        {
            return Path.Combine(AppPaths.BaseDir, LogFolderName, CrashSubFolderName);
        }

        private static string GetLegacyDir()
        {
            return Path.Combine(AppPaths.BaseDir, LogFolderName);
        }

        /// <summary>
        /// ★ 32.0.0：把旧 Logs/*.txt 迁移到 Logs/crash/
        /// </summary>
        private static void TryMigrateLegacyLogs()
        {
            try
            {
                string legacyDir = GetLegacyDir();
                string crashDir = GetCrashDir();

                if (!Directory.Exists(legacyDir)) return;

                var legacyFiles = Directory.GetFiles(legacyDir, $"{LogFilePrefix}*.txt");
                if (legacyFiles.Length == 0) return;

                if (!Directory.Exists(crashDir))
                    Directory.CreateDirectory(crashDir);

                foreach (var src in legacyFiles)
                {
                    try
                    {
                        string name = Path.GetFileName(src);
                        string dst = Path.Combine(crashDir, name);
                        if (File.Exists(dst)) continue;
                        File.Move(src, dst);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string GetVersionText()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
                    return info.InformationalVersion;

                var ver = asm.GetName().Version;
                if (ver != null) return ver.ToString();

                return "unknown";
            }
            catch { return "unknown"; }
        }

        // ═══════ UI 线程异常 ═══════

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteLog("UI", e.Exception);
            e.Handled = true;
        }

        // ═══════ 后台线程异常 ═══════

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                WriteLog("AppDomain", ex);
            else
                WriteLog("AppDomain",
                    new Exception(e.ExceptionObject?.ToString() ?? "Unknown exception object"));
        }

        // ═══════ Task 异常 ═══════

        private static void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteLog("Task", e.Exception);
            e.SetObserved();
        }

        // ═══════ 写日志 ═══════

        private static void WriteLog(string source, Exception ex)
        {
            try
            {
                string dir = GetCrashDir();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string fileName = $"{LogFilePrefix}{DateTime.Now:yyyyMMdd-HHmmss}.txt";
                string path = Path.Combine(dir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("===== CMW 崩溃日志 =====");
                sb.AppendLine($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"来源：{source}");
                sb.AppendLine($"版本：{GetVersionText()}");
                sb.AppendLine($"系统：{Environment.OSVersion}");
                sb.AppendLine($"框架：{Environment.Version}");
                sb.AppendLine($"进程：{Environment.ProcessPath}");
                sb.AppendLine();

                sb.AppendLine("===== 异常信息 =====");
                sb.AppendLine($"类型：{ex.GetType().FullName}");
                sb.AppendLine($"消息：{ex.Message}");
                sb.AppendLine();

                sb.AppendLine("===== 堆栈 =====");
                sb.AppendLine(ex.ToString());
                sb.AppendLine();

                if (ex.InnerException != null)
                {
                    sb.AppendLine("===== 内部异常 =====");
                    sb.AppendLine(ex.InnerException.ToString());
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                CleanupOldLogs(dir);
            }
            catch
            {
                // 日志系统自身出错 → 静默，不能因此再崩
            }
        }

        // ═══════ 清理旧日志 ═══════

        private static void CleanupOldLogs(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir, $"{LogFilePrefix}*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                for (int i = MaxLogs; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }
    }
}