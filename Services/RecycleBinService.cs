﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CMW
{
    /// <summary>
    /// ★ 26.5.0：回收站服务
    /// 删除的作业 / 待办 / 值日组先进入回收站，可恢复或彻底删除
    /// 回收站保留最近 50 条
    /// </summary>
    public static class RecycleBinService
    {
        private const int MaxItems = 50;

        // ═══════ 类型常量 ═══════

        public const string TypeHomework = "HomeworkItem";
        public const string TypeTodo = "TodoItem";
        public const string TypeDutyGroup = "DutyGroup";

        // ═══════ 加入回收站 ═══════

        /// <summary>
        /// 把删除的对象加入回收站
        /// </summary>
        public static void Add(string type, string displayText, object original)
        {
            try
            {
                var list = LoadList();

                string json = JsonConvert.SerializeObject(original);

                list.Add(new RecycleBinItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = type,
                    DisplayText = displayText,
                    DeletedAt = DateTime.Now,
                    OriginalJson = json
                });

                // 只保留最近 MaxItems 条
                if (list.Count > MaxItems)
                {
                    list = list
                        .OrderByDescending(x => x.DeletedAt)
                        .Take(MaxItems)
                        .OrderBy(x => x.DeletedAt)
                        .ToList();
                }

                JsonHelper.Save(AppPaths.RecycleBin, list);
            }
            catch { }
        }

        // ═══════ 读取全部 ═══════

        public static List<RecycleBinItem> GetAll()
        {
            var list = LoadList();
            return list
                .OrderByDescending(x => x.DeletedAt)
                .ToList();
        }

        // ═══════ 恢复 ═══════

        /// <summary>
        /// 恢复指定项 → 追加回原数据文件 → 从回收站移除
        /// </summary>
        public static bool Restore(string id)
        {
            try
            {
                var list = LoadList();
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) return false;

                bool ok = item.Type switch
                {
                    TypeHomework => RestoreHomework(item.OriginalJson),
                    TypeTodo => RestoreTodo(item.OriginalJson),
                    TypeDutyGroup => RestoreDutyGroup(item.OriginalJson),
                    _ => false
                };

                if (!ok) return false;

                list.RemoveAll(x => x.Id == id);
                JsonHelper.Save(AppPaths.RecycleBin, list);
                return true;
            }
            catch { return false; }
        }

        // ═══════ 彻底删除 ═══════

        public static void DeletePermanently(string id)
        {
            try
            {
                var list = LoadList();
                list.RemoveAll(x => x.Id == id);
                JsonHelper.Save(AppPaths.RecycleBin, list);
            }
            catch { }
        }

        // ═══════ 清空 ═══════

        public static void ClearAll()
        {
            try
            {
                JsonHelper.Save(AppPaths.RecycleBin, new List<RecycleBinItem>());
            }
            catch { }
        }

        // ═══════ 内部：读写 ═══════

        private static List<RecycleBinItem> LoadList()
        {
            return JsonHelper.Load<List<RecycleBinItem>>(AppPaths.RecycleBin)
                   ?? new List<RecycleBinItem>();
        }

        // ═══════ 恢复实现 ═══════

        private static bool RestoreHomework(string json)
        {
            try
            {
                var hw = JsonConvert.DeserializeObject<HomeworkItem>(json);
                if (hw == null) return false;

                var list = JsonHelper.Load<List<HomeworkItem>>(AppPaths.Homework)
                           ?? new List<HomeworkItem>();

                // 防重复恢复：Id 已存在则跳过
                if (list.Any(x => x.Id == hw.Id)) return false;

                list.Add(hw);
                JsonHelper.Save(AppPaths.Homework, list);
                return true;
            }
            catch { return false; }
        }

        private static bool RestoreTodo(string json)
        {
            try
            {
                var td = JsonConvert.DeserializeObject<TodoItem>(json);
                if (td == null) return false;

                var list = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                           ?? new List<TodoItem>();

                if (list.Any(x => x.Id == td.Id)) return false;

                list.Add(td);
                JsonHelper.Save(AppPaths.Todo, list);
                return true;
            }
            catch { return false; }
        }

        private static bool RestoreDutyGroup(string json)
        {
            try
            {
                var dg = JsonConvert.DeserializeObject<DutyGroup>(json);
                if (dg == null) return false;

                var model = JsonHelper.Load<DutySaveModel>(AppPaths.Duty)
                            ?? new DutySaveModel();
                if (model.DutyGroups == null)
                    model.DutyGroups = new List<DutyGroup>();

                // 防重复：同名组已存在则跳过
                if (model.DutyGroups.Any(x => x.GroupName == dg.GroupName))
                    return false;

                model.DutyGroups.Add(dg);
                JsonHelper.Save(AppPaths.Duty, model);
                return true;
            }
            catch { return false; }
        }
    }
}