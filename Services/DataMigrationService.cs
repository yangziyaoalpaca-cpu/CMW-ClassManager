﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CMW
{
    /// <summary>
    /// 数据迁移服务：兼容老版本数据包
    /// 支持从 A1.3 及之前、A1.4、26.1.0 等任意历史版本导入
    /// </summary>
    public static class DataMigrationService
    {
        /// <summary>
        /// 迁移整个数据包（就地修改传入的引用）
        /// </summary>
        public static void MigrateAll(AllDataPackage pack)
        {
            pack.Config = MigrateConfig(pack.Config);
            pack.HomeworkRaw = MigrateHomework(pack.HomeworkRaw);
            pack.ScheduleRaw = MigrateSchedule(pack.ScheduleRaw);
            // Duty / Todo 结构基本没变，不用迁移
        }

        // ═══════════════════════════════════════
        // Config 字段补齐
        // ═══════════════════════════════════════

        private static AppConfig MigrateConfig(AppConfig? old)
        {
            var fresh = new AppConfig();
            if (old == null) return fresh;

            if (string.IsNullOrEmpty(old.FontFamily))
                old.FontFamily = fresh.FontFamily;

            if (string.IsNullOrEmpty(old.TodoRemindTime1))
                old.TodoRemindTime1 = fresh.TodoRemindTime1;

            if (string.IsNullOrEmpty(old.TodoRemindTime2))
                old.TodoRemindTime2 = fresh.TodoRemindTime2;

            if (string.IsNullOrEmpty(old.FirstClassStart))
                old.FirstClassStart = fresh.FirstClassStart;

            if (old.ClassDurationMinute <= 0)
                old.ClassDurationMinute = fresh.ClassDurationMinute;

            if (old.BreakDurationMinute <= 0)
                old.BreakDurationMinute = fresh.BreakDurationMinute;

            if (old.TimeSlots == null || old.TimeSlots.Count == 0)
                old.TimeSlots = DefaultTimeSlots.Create();

            if (string.IsNullOrEmpty(old.IslandCardSize))
                old.IslandCardSize = fresh.IslandCardSize;

            return old;
        }

        // ═══════════════════════════════════════
        // 作业字段迁移：Deadline → StartDate + EndDate
        // ═══════════════════════════════════════

        private static string MigrateHomework(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "[]";

            try
            {
                var arr = JArray.Parse(raw);
                bool migrated = false;

                foreach (var item in arr)
                {
                    if (item is not JObject obj) continue;

                    if (obj["StartDate"] != null && obj["EndDate"] != null) continue;

                    var deadlineToken = obj["Deadline"];
                    if (deadlineToken != null)
                    {
                        string d = deadlineToken.ToString();
                        obj["StartDate"] = d;
                        obj["EndDate"] = d;
                        obj.Remove("Deadline");
                        migrated = true;
                    }
                    else
                    {
                        string today = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        obj["StartDate"] = today;
                        obj["EndDate"] = today;
                        migrated = true;
                    }
                }

                return migrated ? arr.ToString(Formatting.Indented) : raw;
            }
            catch
            {
                return "[]";
            }
        }

        // ═══════════════════════════════════════
        // 课表结构迁移
        // A1.3 及之前：ScheduleItem[8,6] 二维数组
        // A1.4+：{ WeekBase, Overrides }
        //
        // ★ 26.4.1 · B1：新格式也要过一遍
        // 检查 Overrides 中的 WeekDay 字段
        // - 缺 WeekDay → 视为旧 Bug 数据，移除
        //   （旧逻辑会让它跨 6 列生效，语义错误）
        // ═══════════════════════════════════════

        private static string MigrateSchedule(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            try
            {
                string trimmed = raw.TrimStart();

                // ── 新格式 ──
                if (trimmed.StartsWith("{"))
                {
                    return MigrateScheduleNewFormat(raw);
                }

                // ── 老格式：二维数组 ──
                if (trimmed.StartsWith("["))
                {
                    var arr = JArray.Parse(raw);
                    var newData = new ScheduleData();

                    for (int p = 0; p < arr.Count && p < 8; p++)
                    {
                        var row = arr[p] as JArray;
                        if (row == null) continue;

                        for (int w = 0; w < row.Count && w < 6; w++)
                        {
                            var cell = row[w] as JObject;
                            if (cell == null) continue;

                            string course = cell["CourseName"]?.ToString() ?? "";
                            string teacher = cell["TeacherName"]?.ToString() ?? "";

                            if (string.IsNullOrWhiteSpace(course)
                                && string.IsNullOrWhiteSpace(teacher))
                                continue;

                            newData.WeekBase.Add(new ScheduleItem
                            {
                                WeekDay = w + 1,
                                Period = p + 1,
                                LessonName = course,
                                Teacher = teacher
                            });
                        }
                    }

                    return JsonConvert.SerializeObject(newData, Formatting.Indented);
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 新格式（{ WeekBase, Overrides }）迁移
        /// 主要是清理 WeekDay 缺失的 Override
        /// </summary>
        private static string MigrateScheduleNewFormat(string raw)
        {
            try
            {
                var obj = JObject.Parse(raw);
                var overridesToken = obj["Overrides"] as JArray;

                if (overridesToken == null || overridesToken.Count == 0)
                    return raw;

                bool migrated = false;

                // 倒序遍历删除，安全
                for (int i = overridesToken.Count - 1; i >= 0; i--)
                {
                    var ov = overridesToken[i] as JObject;
                    if (ov == null) continue;

                    var wdToken = ov["WeekDay"];
                    if (wdToken == null)
                    {
                        // 旧数据无 WeekDay → 视为失效数据，移除
                        overridesToken.RemoveAt(i);
                        migrated = true;
                    }
                }

                return migrated ? obj.ToString(Formatting.Indented) : raw;
            }
            catch
            {
                return raw;
            }
        }
    }
}