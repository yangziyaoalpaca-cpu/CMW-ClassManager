using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace CMW
{
    public class AppConfig
    {
        public string ClassName { get; set; } = "";
        public int BlurRadius { get; set; } = 40;
        public string FontFamily { get; set; } = "Microsoft YaHei UI";

        public string TodoRemindTime1 { get; set; } = "12:00";
        public string TodoRemindTime2 { get; set; } = "18:00";

        public string FirstClassStart { get; set; } = "08:00";
        public int ClassDurationMinute { get; set; } = 45;
        public int BreakDurationMinute { get; set; } = 10;

        public List<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();

        public bool IslandDraggable { get; set; } = false;
        public bool GlassEnabled { get; set; } = true;
        public bool GlassFullEffects { get; set; } = true;
        public bool CardHoverAnimation { get; set; } = true;
        public bool CardPressAnimation { get; set; } = false;
        public bool AutoStart { get; set; } = false;

        public string BackgroundImagePath { get; set; } = "";
        public bool BackgroundBlurEnabled { get; set; } = true;

        public bool NotifyClickOutsideToClose { get; set; } = false;
        public bool PerformanceMode { get; set; } = false;
        public string IslandCardSize { get; set; } = "Medium";

        public bool HomeworkRemindEnabled { get; set; } = false;
        public List<string> HomeworkRemindTimes { get; set; } = new List<string> { "17:00", "20:00" };
        public int HomeworkRemindScrollSpeed { get; set; } = 60;
        public int HomeworkRemindDurationSeconds { get; set; } = 120;

        public bool FloatWindowEnabled { get; set; } = false;
        public string FloatWindowContentMode { get; set; } = "TodayInfo";
        public string FloatWindowPositionMode { get; set; } = "Fixed";
        public double FloatWindowLeft { get; set; } = -1;
        public double FloatWindowTop { get; set; } = -1;
        public double FloatWindowWidth { get; set; } = 520;
        public double FloatWindowHeight { get; set; } = 120;
        public bool FloatWindowAutoSwitch { get; set; } = false;
        public bool FloatWindowOpenOnClassEnd { get; set; } = false;

        public string IslandCapsuleTextMode { get; set; } = "CMW";

        public bool DutyAutoRotateEnabled { get; set; } = false;
        public string DutyLastRotateDate { get; set; } = "";

        public int LastNavIndex { get; set; } = 0;

        public string SilentStart { get; set; } = "";
        public string SilentEnd { get; set; } = "";

        public double IslandFontScale { get; set; } = 1.0;
        public int IslandAutoCollapseSeconds { get; set; } = 8;

        public string ExamName { get; set; } = "";
        public string ExamDate { get; set; } = "";

        public double RightPanelHeightPercent { get; set; } = 70;
        public string RightPanelContentMode { get; set; } = "Auto";
        public int RightPanelAutoSwitchSeconds { get; set; } = 15;
        public bool RightPanelAutoHideOnClassStart { get; set; } = true;
        public bool RightPanelAutoShowOnClassEnd { get; set; } = true;
        public double RightPanelFontScale { get; set; } = 1.0;

        public string ClearDataPassword { get; set; } = "12345678";

        // ═══════ ★ 28.0.0 新增 ═══════

        public bool RightPanelUseOwnTimeSlots { get; set; } = false;
        public List<TimeSlot> RightPanelTimeSlots { get; set; } = new List<TimeSlot>();

        public bool ClassStartRemindEnabled { get; set; } = true;
        public bool ClassEndRemindEnabled { get; set; } = true;
        public string ClassRemindStyle { get; set; } = "Both";
        public int MiniRemindDurationSeconds { get; set; } = 5;

        public bool DutyRemindEnabled { get; set; } = false;
        public string DutyRemindTime { get; set; } = "07:00";
        public bool TodoCustomRemindEnabled { get; set; } = false;
        public string TodoCustomRemindTime { get; set; } = "08:00";

        public bool BoardAutoScheduleEnabled { get; set; } = false;
        public string BoardAutoStartTime { get; set; } = "07:30";
        public string BoardAutoEndTime { get; set; } = "18:00";

        public bool LeftScheduleEnabled { get; set; } = false;
        public double LeftScheduleWidth { get; set; } = 90;
        public Dictionary<string, string> LeftScheduleSubjectMap { get; set; } = new Dictionary<string, string>();

        public bool PowerScheduleEnabled { get; set; } = false;
        public string PowerOnTime { get; set; } = "07:00";
        public string PowerOffTime { get; set; } = "22:00";

        // ★ 28.0.0：灵动岛通知条尺寸（百分比，可自定义）
        public double NotifyBarWidthPercent { get; set; } = 96;   // 屏幕宽度的 96%
        public double NotifyBarHeightPercent { get; set; } = 10;  // 屏幕高度的 10%
    }

    public class TimeSlot
    {
        public string Name { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public bool EnableNotify { get; set; } = true;
        public bool IsLongBreak { get; set; } = false;
    }

    public class ScheduleItem
    {
        public int WeekDay { get; set; }
        public int Period { get; set; }
        public string LessonName { get; set; } = "";
        public string Teacher { get; set; } = "";
    }

    public class ScheduleOverride
    {
        public string Date { get; set; } = "";
        public int WeekDay { get; set; }
        public int Period { get; set; }
        public string LessonName { get; set; } = "";
        public string Teacher { get; set; } = "";
    }

    public class DayOverride
    {
        public string Date { get; set; } = "";
        public int SourceWeekDay { get; set; } = 1;
    }

    public class ScheduleData
    {
        public List<ScheduleItem> WeekBase { get; set; } = new List<ScheduleItem>();
        public List<ScheduleOverride> Overrides { get; set; } = new List<ScheduleOverride>();
        public List<DayOverride> DayOverrides { get; set; } = new List<DayOverride>();
    }

    public class DutyGroup
    {
        public string GroupName { get; set; } = "";
        public string MembersText { get; set; } = "";
    }

    public class DutySaveModel
    {
        public List<DutyGroup> DutyGroups { get; set; } = new List<DutyGroup>();
        public string? TodayDutyGroupName { get; set; }
    }

    public class HomeworkItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string HomeworkName { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now;
        public bool NeedSubmit { get; set; } = true;
        public string Priority { get; set; } = "Normal";
        public bool IsCompleted { get; set; }
        public List<string> SubmittedStudentIds { get; set; } = new List<string>();
        public bool IsRollCalled { get; set; } = false;

        [JsonIgnore]
        public string DateRangeText
        {
            get
            {
                if (StartDate.Date == EndDate.Date)
                    return EndDate.ToString("MM-dd HH:mm");
                return $"{StartDate:MM-dd HH:mm} 至 {EndDate:MM-dd HH:mm}";
            }
        }

        [JsonIgnore]
        public string PriorityText => Priority switch
        {
            "Urgent" => "紧急",
            "VeryUrgent" => "非常紧急",
            _ => "普通"
        };

        [JsonIgnore]
        public int PriorityOrder => Priority switch
        {
            "VeryUrgent" => 0,
            "Urgent" => 1,
            _ => 2
        };

        [JsonIgnore]
        public bool IsExpired => DateTime.Now > EndDate;

        public bool IsActiveOn(DateTime date)
        {
            var d = date.Date;
            return d >= StartDate.Date && d <= EndDate.Date;
        }
    }

    public class TodoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public string Remark { get; set; } = "";
        public DateTime? Deadline { get; set; }
        public bool IsCompleted { get; set; }

        [JsonIgnore]
        public string DeadlineText => Deadline.HasValue
            ? Deadline.Value.ToString("MM-dd")
            : "无截止";
    }

    public class TodoSummaryItem
    {
        public string TodoContent { get; set; } = "";
        public string Remark { get; set; } = "";
        public string DeadlineText { get; set; } = "";
        public bool IsCompleted { get; set; }
    }

    public class TodayInfo
    {
        public string Schedule { get; set; } = "";
        public string Duty { get; set; } = "";
        public string Homework { get; set; } = "";
        public string Todo { get; set; } = "";
    }

    public class AllDataPackage
    {
        public AppConfig Config { get; set; } = new AppConfig();
        public string ScheduleRaw { get; set; } = "";
        public string DutyRaw { get; set; } = "";
        public string HomeworkRaw { get; set; } = "";
        public string TodoRaw { get; set; } = "";
    }

    public class HomeworkReminderRecord
    {
        public string RemindTimeStr { get; set; } = "";
        public DateTime RemindAt { get; set; }
        public List<string> UnfinishedHomework { get; set; } = new List<string>();
    }

    public class RecycleBinItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public DateTime DeletedAt { get; set; } = DateTime.Now;
        public string OriginalJson { get; set; } = "";
    }

    public class Student
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string StudentId { get; set; } = "";
        public int Order { get; set; } = 0;

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StudentId))
                    return Name;
                return $"{StudentId} {Name}";
            }
        }
    }

    public class StudentListData
    {
        public List<Student> Students { get; set; } = new List<Student>();
    }

    public class ExamItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ExamName { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime ExamDate { get; set; } = DateTime.Today;
        public double? ClassAverage { get; set; }
        public double? ClassHighest { get; set; }
        public double? ClassLowest { get; set; }
        public string Remark { get; set; } = "";

        [JsonIgnore]
        public string DateText => ExamDate.ToString("yyyy-MM-dd");

        [JsonIgnore]
        public string AverageText => ClassAverage.HasValue ? ClassAverage.Value.ToString("F1") : "-";

        [JsonIgnore]
        public string HighestText => ClassHighest.HasValue ? ClassHighest.Value.ToString("F1") : "-";

        [JsonIgnore]
        public string LowestText => ClassLowest.HasValue ? ClassLowest.Value.ToString("F1") : "-";
    }

    public class ExamListData
    {
        public List<ExamItem> Exams { get; set; } = new List<ExamItem>();
    }

    public class WeeklyReportMeta
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WeekKey { get; set; } = "";
        public DateTime WeekStart { get; set; } = DateTime.Today;
        public DateTime WeekEnd { get; set; } = DateTime.Today;
        public string FileName { get; set; } = "";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int StudentCount { get; set; } = 0;
        public int HomeworkCount { get; set; } = 0;
        public int HomeworkRollCalledCount { get; set; } = 0;
        public int TodoCompletedCount { get; set; } = 0;
        public int TodoTotalCount { get; set; } = 0;

        [JsonIgnore]
        public string RangeText => $"{WeekStart:MM-dd} ~ {WeekEnd:MM-dd}";
    }

    public class WeeklyReportListData
    {
        public List<WeeklyReportMeta> Reports { get; set; } = new List<WeeklyReportMeta>();
    }

    public class SeatAssignment
    {
        public int Row { get; set; } = 0;
        public int Col { get; set; } = 0;
        public string StudentId { get; set; } = "";
    }

    public class SeatLayout
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "默认座位表";
        public int Rows { get; set; } = 6;
        public int Cols { get; set; } = 8;
        public List<SeatAssignment> Assignments { get; set; } = new List<SeatAssignment>();
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class SeatLayoutData
    {
        public List<SeatLayout> Layouts { get; set; } = new List<SeatLayout>();
        public string CurrentLayoutId { get; set; } = "";
    }

    public class ClassEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; }
        public string Category { get; set; } = "活动";
        public string Remark { get; set; } = "";

        [JsonIgnore]
        public string DateText
        {
            get
            {
                if (EndDate.HasValue && EndDate.Value.Date != Date.Date)
                    return $"{Date:yyyy-MM-dd} ~ {EndDate.Value:yyyy-MM-dd}";
                return Date.ToString("yyyy-MM-dd");
            }
        }

        public bool IsOnDate(DateTime d)
        {
            var day = d.Date;
            if (day < Date.Date) return false;
            if (EndDate.HasValue)
                return day <= EndDate.Value.Date;
            return day == Date.Date;
        }
    }

    public class ClassEventListData
    {
        public List<ClassEvent> Events { get; set; } = new List<ClassEvent>();
    }

    public class HomeworkTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Priority { get; set; } = "Normal";
        public int DurationDays { get; set; } = 1;
        public bool IsFavorite { get; set; } = false;
    }

    public class HomeworkTemplateListData
    {
        public List<HomeworkTemplate> Templates { get; set; } = new List<HomeworkTemplate>();
    }

    public class PollItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Dictionary<string, bool> Voted { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> Weights { get; set; } = new Dictionary<string, int>();

        [JsonIgnore]
        public string CreatedText => CreatedAt.ToString("MM-dd HH:mm");

        public bool HasVoted(string studentId)
        {
            if (string.IsNullOrEmpty(studentId)) return false;
            if (Voted == null) return false;
            return Voted.TryGetValue(studentId, out bool v) && v;
        }

        public void SetVoted(string studentId, bool voted)
        {
            if (string.IsNullOrEmpty(studentId)) return;
            if (Voted == null) Voted = new Dictionary<string, bool>();
            Voted[studentId] = voted;
        }

        public int GetWeight(string studentId)
        {
            if (string.IsNullOrEmpty(studentId)) return 1;
            if (Weights == null) return 1;
            return Weights.TryGetValue(studentId, out int w) ? w : 1;
        }

        public void SetWeight(string studentId, int weight)
        {
            if (string.IsNullOrEmpty(studentId)) return;
            if (weight < 1) weight = 1;
            if (weight > 99) weight = 99;
            if (Weights == null) Weights = new Dictionary<string, int>();
            Weights[studentId] = weight;
        }

        public int GetVotedCount()
        {
            if (Voted == null) return 0;
            int count = 0;
            foreach (var kv in Voted)
                if (kv.Value) count++;
            return count;
        }

        public int GetTotalVotes()
        {
            if (Voted == null) return 0;
            int total = 0;
            foreach (var kv in Voted)
            {
                if (kv.Value)
                    total += GetWeight(kv.Key);
            }
            return total;
        }

        public void ResetAll()
        {
            Voted = new Dictionary<string, bool>();
        }
    }

    public class PollListData
    {
        public List<PollItem> Polls { get; set; } = new List<PollItem>();
    }

    public class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public string ImageFileName { get; set; } = "";
        public string ThumbFileName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string CreatedText => CreatedAt.ToString("MM-dd HH:mm");

        [JsonIgnore]
        public bool HasImage => !string.IsNullOrWhiteSpace(ImageFileName);

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Content))
                {
                    if (Content.Length > 40)
                        return Content.Substring(0, 40) + "…";
                    return Content;
                }

                if (HasImage)
                    return "（图片笔记）";

                return "（空）";
            }
        }
    }

    public class NoteListData
    {
        public List<NoteItem> Notes { get; set; } = new List<NoteItem>();
    }

    public static class AppPaths
    {
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        public static string Config => Path.Combine(BaseDir, "config.json");
        public static string Schedule => Path.Combine(BaseDir, "schedule.json");
        public static string Duty => Path.Combine(BaseDir, "duty.json");
        public static string Homework => Path.Combine(BaseDir, "homework.json");
        public static string Todo => Path.Combine(BaseDir, "todo.json");

        public static string HomeworkReminders => Path.Combine(BaseDir, "homework_reminders.json");
        public static string RecycleBin => Path.Combine(BaseDir, "recyclebin.json");
        public static string Students => Path.Combine(BaseDir, "students.json");
        public static string Exams => Path.Combine(BaseDir, "exams.json");

        public static string ReportsDir => Path.Combine(BaseDir, "Reports");
        public static string ReportsIndex => Path.Combine(ReportsDir, "reports.json");

        public static string Seats => Path.Combine(BaseDir, "seats.json");
        public static string Events => Path.Combine(BaseDir, "events.json");
        public static string HomeworkTemplates => Path.Combine(BaseDir, "homework_templates.json");

        public static string Polls => Path.Combine(BaseDir, "polls.json");

        public static string NotesDir => Path.Combine(BaseDir, "Notes");
        public static string NotesImagesDir => Path.Combine(NotesDir, "Images");
        public static string NotesThumbsDir => Path.Combine(NotesDir, "Thumbs");
        public static string NotesIndex => Path.Combine(NotesDir, "notes.json");

        public static string CustomBackgroundDir => Path.Combine(BaseDir, "CustomAssets");
        public static string CustomBackgroundFile =>
            Path.Combine(CustomBackgroundDir, "custom_background.jpg");
        public static string CustomBackgroundBlurredFile =>
            Path.Combine(CustomBackgroundDir, "custom_background_blurred.jpg");

        public static string ScriptsDir => Path.Combine(BaseDir, "Scripts");
        public static string PowerOnScript => Path.Combine(ScriptsDir, "power_on.bat");
        public static string PowerOffScript => Path.Combine(ScriptsDir, "power_off.bat");
    }

    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-dd HH:mm:ss"
        };

        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
        private static readonly Dictionary<string, DateTime> _cacheTime = new Dictionary<string, DateTime>();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);

        public static T? Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;

            if (_cache.TryGetValue(path, out var cached) && cached is T typed)
            {
                if (_cacheTime.TryGetValue(path, out var time)
                    && DateTime.Now - time < CacheDuration)
                {
                    return typed;
                }
            }

            try
            {
                string json = File.ReadAllText(path);
                var result = JsonConvert.DeserializeObject<T>(json, Settings);
                if (result != null)
                {
                    _cache[path] = result;
                    _cacheTime[path] = DateTime.Now;
                }
                return result;
            }
            catch { return null; }
        }

        public static void Save<T>(string path, T data)
        {
            string json = JsonConvert.SerializeObject(data, Settings);
            File.WriteAllText(path, json);

            if (data != null)
            {
                _cache[path] = data;
                _cacheTime[path] = DateTime.Now;
            }
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _cacheTime.Clear();
        }
    }

    public static class DefaultTimeSlots
    {
        public static List<TimeSlot> Create()
        {
            return new List<TimeSlot>
            {
                new TimeSlot { Name = "早读",   StartTime = "07:20", EndTime = "07:50", EnableNotify = true, IsLongBreak = true },
                new TimeSlot { Name = "第1节",  StartTime = "08:00", EndTime = "08:45", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "第2节",  StartTime = "08:55", EndTime = "09:40", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "大课间", StartTime = "09:40", EndTime = "10:10", EnableNotify = true, IsLongBreak = true },
                new TimeSlot { Name = "第3节",  StartTime = "10:10", EndTime = "10:55", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "第4节",  StartTime = "11:05", EndTime = "11:50", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "中自习", StartTime = "12:30", EndTime = "13:10", EnableNotify = true, IsLongBreak = true },
                new TimeSlot { Name = "第5节",  StartTime = "14:00", EndTime = "14:45", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "第6节",  StartTime = "14:55", EndTime = "15:40", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "第7节",  StartTime = "15:50", EndTime = "16:35", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "第8节",  StartTime = "16:45", EndTime = "17:30", EnableNotify = true, IsLongBreak = false },
                new TimeSlot { Name = "前自习", StartTime = "18:00", EndTime = "18:40", EnableNotify = true, IsLongBreak = true },
                new TimeSlot { Name = "晚自习", StartTime = "19:00", EndTime = "21:00", EnableNotify = true, IsLongBreak = true }
            };
        }
    }

    public static class AutoStartHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CMW";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (key == null) return false;
                var value = key.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.SetValue(AppName, $"\"{exePath}\"");
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.DeleteValue(AppName, false);
            }
            catch { }
        }

        public static void SyncWithConfig(bool enabled)
        {
            if (enabled) Enable();
            else Disable();
        }
    }

    public static class FontHelper
    {
        public static List<string> AvailableFonts { get; } = new List<string>
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "Microsoft JhengHei",
            "SimSun",
            "SimHei",
            "KaiTi",
            "FangSong",
            "DengXian",
            "Consolas",
            "PingFang SC"
        };
    }
}