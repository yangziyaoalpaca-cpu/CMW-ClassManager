﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CMW
{
    /// <summary>
    /// ★ 27.0.0：数据导出 CSV
    /// 用 UTF-8 BOM 编码，让 Excel 正确识别中文
    /// </summary>
    public static class ExportService
    {
        // ═══════ 作业 ═══════

        public static string ExportHomework(string path)
        {
            try
            {
                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                    ?? new List<HomeworkItem>();

                var sb = new StringBuilder();
                sb.AppendLine("作业名称,科目,开始日期,截止日期,优先级,完成状态,已交人数,未交人数");

                var studentCount = GetStudentCount();

                foreach (var h in list)
                {
                    int submitted = h.SubmittedStudentIds?.Count ?? 0;
                    int unsubmitted = h.IsRollCalled ? Math.Max(0, studentCount - submitted) : 0;

                    sb.AppendLine(string.Join(",",
                        Csv(h.HomeworkName),
                        Csv(h.Subject),
                        Csv(h.StartDate.ToString("yyyy-MM-dd")),
                        Csv(h.EndDate.ToString("yyyy-MM-dd")),
                        Csv(h.PriorityText),
                        Csv(h.IsCompleted ? "已完成" : "未完成"),
                        Csv(h.IsRollCalled ? submitted.ToString() : "-"),
                        Csv(h.IsRollCalled ? unsubmitted.ToString() : "-")));
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ═══════ 待办 ═══════

        public static string ExportTodo(string path)
        {
            try
            {
                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                    ?? new List<TodoItem>();

                var sb = new StringBuilder();
                sb.AppendLine("内容,备注,截止日期,完成状态");

                foreach (var t in list)
                {
                    sb.AppendLine(string.Join(",",
                        Csv(t.Content),
                        Csv(t.Remark),
                        Csv(t.Deadline.HasValue ? t.Deadline.Value.ToString("yyyy-MM-dd") : ""),
                        Csv(t.IsCompleted ? "已完成" : "未完成")));
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ═══════ 课表 ═══════

        public static string ExportSchedule(string path)
        {
            try
            {
                var schedule = JsonHelper.Load<ScheduleData>(AppPaths.Schedule)
                    ?? new ScheduleData();
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                var slots = config.TimeSlots;
                if (slots == null || slots.Count == 0)
                    slots = DefaultTimeSlots.Create();

                var sb = new StringBuilder();
                sb.AppendLine("节次,周一,周二,周三,周四,周五,周六");

                for (int p = 0; p < slots.Count; p++)
                {
                    int period = p + 1;
                    var row = new List<string> { slots[p].Name };

                    for (int w = 1; w <= 6; w++)
                    {
                        var item = schedule.WeekBase.FirstOrDefault(x =>
                            x.WeekDay == w && x.Period == period);
                        string cell = "";
                        if (item != null && !string.IsNullOrWhiteSpace(item.LessonName))
                        {
                            cell = item.LessonName;
                            if (!string.IsNullOrWhiteSpace(item.Teacher))
                                cell += "（" + item.Teacher + "）";
                        }
                        row.Add(cell);
                    }

                    sb.AppendLine(string.Join(",", row.Select(Csv)));
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ═══════ 值日 ═══════

        public static string ExportDuty(string path)
        {
            try
            {
                var model = JsonHelper.Load<DutySaveModel>(AppPaths.Duty)
                    ?? new DutySaveModel();

                var sb = new StringBuilder();
                sb.AppendLine("组名,成员,是否为今日值日");

                foreach (var g in model.DutyGroups ?? new List<DutyGroup>())
                {
                    bool isToday = g.GroupName == model.TodayDutyGroupName;
                    sb.AppendLine(string.Join(",",
                        Csv(g.GroupName),
                        Csv(g.MembersText),
                        Csv(isToday ? "是" : "否")));
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ═══════ 一键导出全部（到一个文件夹） ═══════

        public static string ExportAll(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                var errs = new List<string>();

                string e1 = ExportHomework(Path.Combine(folderPath, $"作业-{ts}.csv"));
                if (!string.IsNullOrEmpty(e1)) errs.Add("作业：" + e1);

                string e2 = ExportTodo(Path.Combine(folderPath, $"待办-{ts}.csv"));
                if (!string.IsNullOrEmpty(e2)) errs.Add("待办：" + e2);

                string e3 = ExportSchedule(Path.Combine(folderPath, $"课表-{ts}.csv"));
                if (!string.IsNullOrEmpty(e3)) errs.Add("课表：" + e3);

                string e4 = ExportDuty(Path.Combine(folderPath, $"值日-{ts}.csv"));
                if (!string.IsNullOrEmpty(e4)) errs.Add("值日：" + e4);

                return errs.Count == 0 ? "" : string.Join("; ", errs);
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ═══════ 工具 ═══════

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static int GetStudentCount()
        {
            try
            {
                var data = JsonHelper.Load<StudentListData>(AppPaths.Students);
                return data?.Students?.Count ?? 0;
            }
            catch { return 0; }
        }
    }
}