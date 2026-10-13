﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CMW
{
    /// <summary>
    /// ★ 26.5.0：数据自动备份
    /// 每日首次启动时把关键数据备份到 Backups/yyyy-MM-dd/
    /// 保留最近 7 天，超出自动清理
    /// </summary>
    public static class BackupService
    {
        private const int KeepDays = 7;
        private const string BackupFolderName = "Backups";
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// 每日首次启动时调用（幂等，当天已备份则跳过）
        /// 建议通过 Dispatcher.BeginInvoke(Background) 异步调用
        /// </summary>
        public static void RunDailyBackupIfNeeded()
        {
            try
            {
                string backupRoot = Path.Combine(AppPaths.BaseDir, BackupFolderName);
                string todayDir = Path.Combine(backupRoot, DateTime.Now.ToString(DateFormat));

                // 今天已备份 → 跳过
                if (Directory.Exists(todayDir)) return;

                if (!Directory.Exists(backupRoot))
                    Directory.CreateDirectory(backupRoot);

                Directory.CreateDirectory(todayDir);

                // ── 备份业务文件 ──
                BackupFile(AppPaths.Config, todayDir);
                BackupFile(AppPaths.Schedule, todayDir);
                BackupFile(AppPaths.Duty, todayDir);
                BackupFile(AppPaths.Homework, todayDir);
                BackupFile(AppPaths.Todo, todayDir);
                BackupFile(AppPaths.HomeworkReminders, todayDir);

                // ── 备份自定义背景原图（模糊图可重新生成，不备份）──
                if (File.Exists(AppPaths.CustomBackgroundFile))
                {
                    try
                    {
                        File.Copy(
                            AppPaths.CustomBackgroundFile,
                            Path.Combine(todayDir, "custom_background.jpg"),
                            true);
                    }
                    catch { }
                }

                // ── 清理旧备份 ──
                CleanupOldBackups(backupRoot);
            }
            catch
            {
                // 备份系统自身出错 → 静默，不影响主程序
            }
        }

        // ═══════ 单个文件备份 ═══════

        private static void BackupFile(string srcPath, string dstDir)
        {
            try
            {
                if (!File.Exists(srcPath)) return;

                string name = Path.GetFileName(srcPath);
                string dstPath = Path.Combine(dstDir, name);
                File.Copy(srcPath, dstPath, true);
            }
            catch { }
        }

        // ═══════ 清理旧备份 ═══════

        private static void CleanupOldBackups(string backupRoot)
        {
            try
            {
                var dirs = Directory.GetDirectories(backupRoot)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => DateTime.TryParseExact(
                        d.Name,
                        DateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _))
                    .OrderByDescending(d => d.Name)   // 日期字符串降序 = 时间降序
                    .ToList();

                for (int i = KeepDays; i < dirs.Count; i++)
                {
                    try { dirs[i].Delete(true); } catch { }
                }
            }
            catch { }
        }
    }
}