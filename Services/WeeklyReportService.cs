﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CMW
{
    /// <summary>
    /// ★ 27.0.0：每周学习进度汇报日志
    /// 生成某周（周一至周日）的 Markdown 报告，保存在 Reports/ 目录
    /// 自动维护 reports.json 索引
    /// </summary>
    public static class WeeklyReportService
    {
        // ═══════ 生成 ═══════

        /// <summary>
        /// 生成指定周的周报。若已存在则覆盖。
        /// 返回 (成功, 错误信息, 生成的文件路径)
        /// </summary>
        public static (bool Ok, string Error, string FilePath) GenerateForWeek(DateTime anyDayInWeek)
        {
            try
            {
                EnsureDir();

                var weekStart = GetWeekStart(anyDayInWeek);
                var weekEnd = weekStart.AddDays(6);
                string weekKey = GetWeekKey(weekStart);
                string fileName = weekKey + ".md";
                string fullPath = Path.Combine(AppPaths.ReportsDir, fileName);

                string md = BuildMarkdown(weekKey, weekStart, weekEnd);
                File.WriteAllText(fullPath, md, new UTF8Encoding(true));

                // 更新索引
                var index = LoadIndex();
                index.Reports.RemoveAll(x => x.WeekKey == weekKey);

                var meta = new WeeklyReportMeta
                {
                    Id = Guid.NewGuid().ToString(),
                    WeekKey = weekKey,
                    WeekStart = weekStart,
                    WeekEnd = weekEnd,
                    FileName = fileName,
                    GeneratedAt = DateTime.Now,
                    StudentCount = GetStudentCount(),
                    HomeworkCount = CountHomeworkInRange(weekStart, weekEnd),
                    HomeworkRollCalledCount = CountHomeworkRollCalled(weekStart, weekEnd),
                    TodoTotalCount = CountTodoInRange(weekStart, weekEnd),
                    TodoCompletedCount = CountTodoCompletedInRange(weekStart, weekEnd)
                };

                index.Reports.Add(meta);
                index.Reports = index.Reports
                    .OrderByDescending(x => x.WeekStart)
                    .ToList();

                SaveIndex(index);

                return (true, "", fullPath);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, "");
            }
        }

        // ═══════ 读取 / 删除 ═══════

        public static string ReadReportContent(WeeklyReportMeta meta)
        {
            try
            {
                if (meta == null) return "";
                string path = Path.Combine(AppPaths.ReportsDir, meta.FileName);
                if (!File.Exists(path)) return "（报告文件不存在）";
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) { return "（读取失败：" + ex.Message + "）"; }
        }

        public static List<WeeklyReportMeta> GetAllReports()
        {
            var index = LoadIndex();
            return index.Reports
                .OrderByDescending(x => x.WeekStart)
                .ToList();
        }

        public static bool DeleteReport(WeeklyReportMeta meta)
        {
            try
            {
                if (meta == null) return false;

                string path = Path.Combine(AppPaths.ReportsDir, meta.FileName);
                if (File.Exists(path))
                    File.Delete(path);

                var index = LoadIndex();
                index.Reports.RemoveAll(x => x.WeekKey == meta.WeekKey);
                SaveIndex(index);

                return true;
            }
            catch { return false; }
        }

        // ═══════ 工具 ═══════

        public static DateTime GetWeekStart(DateTime d)
        {
            // 周一为一周的第一天
            int diff = ((int)d.DayOfWeek + 6) % 7;   // 周一=0 ... 周日=6
            return d.Date.AddDays(-diff);
        }

        public static string GetWeekKey(DateTime weekStart)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(weekStart,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);

            // ISO 8601：若某周跨年，可能归到上一年或下一年
            // 简单处理：以 weekStart 的年份为准
            return $"{weekStart.Year}-W{week:D2}";
        }

        // ═══════ 数据统计 ═══════

        private static int GetStudentCount()
        {
            try
            {
                var data = JsonHelper.Load<StudentListData>(AppPaths.Students);
                return data?.Students?.Count ?? 0;
            }
            catch { return 0; }
        }

        private static int CountHomeworkInRange(DateTime start, DateTime end)
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();

                // 统计"这一周内有任意一天活跃"的作业
                int count = 0;
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    count += list.Count(h => h.IsActiveOn(d));
                }
                // 避免重复计数：改为按作业本身
                // 换一种更合适的口径：作业的活跃区间与 [start, end] 有交集
                return list.Count(h =>
                    h.StartDate.Date <= end.Date && h.EndDate.Date >= start.Date);
            }
            catch { return 0; }
        }

        private static int CountHomeworkRollCalled(DateTime start, DateTime end)
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();

                return list.Count(h =>
                    h.IsRollCalled &&
                    h.StartDate.Date <= end.Date &&
                    h.EndDate.Date >= start.Date);
            }
            catch { return 0; }
        }

        private static int CountTodoInRange(DateTime start, DateTime end)
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();

                return list.Count(t =>
                    t.Deadline.HasValue &&
                    t.Deadline.Value.Date >= start.Date &&
                    t.Deadline.Value.Date <= end.Date);
            }
            catch { return 0; }
        }

        private static int CountTodoCompletedInRange(DateTime start, DateTime end)
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();

                return list.Count(t =>
                    t.Deadline.HasValue &&
                    t.Deadline.Value.Date >= start.Date &&
                    t.Deadline.Value.Date <= end.Date &&
                    t.IsCompleted);
            }
            catch { return 0; }
        }

        // ═══════ Markdown 组装 ═══════

        private static string BuildMarkdown(string weekKey, DateTime weekStart, DateTime weekEnd)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# CMW 周报 · {weekKey}");
            sb.AppendLine();
            sb.AppendLine($"> 统计周期：{weekStart:yyyy-MM-dd}（周一）~ {weekEnd:yyyy-MM-dd}（周日）");
            sb.AppendLine($"> 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 概览
            int studentCount = GetStudentCount();
            var hwList = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                ?? new List<HomeworkItem>();
            var hwInRange = hwList.Where(h =>
                h.StartDate.Date <= weekEnd.Date &&
                h.EndDate.Date >= weekStart.Date).ToList();

            int hwTotal = hwInRange.Count;
            int hwCompleted = hwInRange.Count(h => h.IsCompleted);
            int hwRoll = hwInRange.Count(h => h.IsRollCalled);
            int hwNotCompleted = hwTotal - hwCompleted;

            var tdList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                ?? new List<TodoItem>();
            var tdInRange = tdList.Where(t =>
                t.Deadline.HasValue &&
                t.Deadline.Value.Date >= weekStart.Date &&
                t.Deadline.Value.Date <= weekEnd.Date).ToList();

            int tdTotal = tdInRange.Count;
            int tdDone = tdInRange.Count(t => t.IsCompleted);

            sb.AppendLine("## 一、概览");
            sb.AppendLine();
            sb.AppendLine("| 项目 | 数量 |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine($"| 学生总数 | {studentCount} |");
            sb.AppendLine($"| 本周作业条目 | {hwTotal} |");
            sb.AppendLine($"| 已交齐作业 | {hwCompleted} |");
            sb.AppendLine($"| 未交齐作业 | {hwNotCompleted} |");
            sb.AppendLine($"| 已点名作业 | {hwRoll} |");
            sb.AppendLine($"| 本周待办（含截止日） | {tdTotal} |");
            sb.AppendLine($"| 已完成待办 | {tdDone} |");
            sb.AppendLine();

            // 作业明细
            sb.AppendLine("## 二、作业明细");
            sb.AppendLine();

            if (hwInRange.Count == 0)
            {
                sb.AppendLine("_本周无作业_");
            }
            else
            {
                sb.AppendLine("| 作业 | 科目 | 时间范围 | 优先级 | 状态 | 已点名 | 已交 | 未交 |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

                foreach (var h in hwInRange.OrderBy(h => h.EndDate).ThenBy(h => h.PriorityOrder))
                {
                    int submitted = h.SubmittedStudentIds?.Count ?? 0;
                    int unsubmitted = (h.IsRollCalled && studentCount > 0)
                        ? Math.Max(0, studentCount - submitted)
                        : 0;

                    sb.AppendLine(string.Join(" | ",
                        "| " + Escape(h.HomeworkName),
                        Escape(string.IsNullOrWhiteSpace(h.Subject) ? "-" : h.Subject),
                        h.DateRangeText,
                        h.PriorityText,
                        h.IsCompleted ? "已完成" : "未完成",
                        h.IsRollCalled ? "是" : "否",
                        h.IsRollCalled ? submitted.ToString() : "-",
                        h.IsRollCalled ? unsubmitted.ToString() : "-") + " |");
                }
            }
            sb.AppendLine();

            // 待办明细
            sb.AppendLine("## 三、待办明细");
            sb.AppendLine();

            if (tdInRange.Count == 0)
            {
                sb.AppendLine("_本周无待办_");
            }
            else
            {
                sb.AppendLine("| 内容 | 备注 | 截止日 | 状态 |");
                sb.AppendLine("| --- | --- | --- | --- |");

                foreach (var t in tdInRange.OrderBy(t => t.Deadline))
                {
                    sb.AppendLine(string.Join(" | ",
                        "| " + Escape(t.Content),
                        Escape(string.IsNullOrWhiteSpace(t.Remark) ? "-" : t.Remark),
                        t.Deadline.HasValue ? t.Deadline.Value.ToString("MM-dd") : "-",
                        t.IsCompleted ? "已完成" : "未完成") + " |");
                }
            }
            sb.AppendLine();

            // 考试记录
            var examList = JsonHelper.Load<ExamListData>(AppPaths.Exams)?.Exams
                ?? new List<ExamItem>();
            var examInRange = examList.Where(e =>
                e.ExamDate.Date >= weekStart.Date &&
                e.ExamDate.Date <= weekEnd.Date).ToList();

            sb.AppendLine("## 四、本周考试");
            sb.AppendLine();

            if (examInRange.Count == 0)
            {
                sb.AppendLine("_本周无考试记录_");
            }
            else
            {
                sb.AppendLine("| 日期 | 考试 | 科目 | 平均分 | 最高 | 最低 |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

                foreach (var e in examInRange.OrderBy(e => e.ExamDate).ThenBy(e => e.Subject))
                {
                    sb.AppendLine(string.Join(" | ",
                        "| " + e.ExamDate.ToString("MM-dd"),
                        Escape(e.ExamName),
                        Escape(e.Subject),
                        e.AverageText,
                        e.HighestText,
                        e.LowestText) + " |");
                }
            }
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("_本报告由 CMW 自动生成_");

            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
        }

        // ═══════ 目录 / 索引 ═══════

        private static void EnsureDir()
        {
            if (!Directory.Exists(AppPaths.ReportsDir))
                Directory.CreateDirectory(AppPaths.ReportsDir);
        }

        private static WeeklyReportListData LoadIndex()
        {
            EnsureDir();
            return JsonHelper.Load<WeeklyReportListData>(AppPaths.ReportsIndex)
                ?? new WeeklyReportListData();
        }

        private static void SaveIndex(WeeklyReportListData index)
        {
            try
            {
                JsonHelper.Save(AppPaths.ReportsIndex, index);
            }
            catch { }
        }
    }
}