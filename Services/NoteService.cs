﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace CMW
{
    /// <summary>
    /// ★ 27.2.0：笔记数据读写 + 图片保存
    /// </summary>
    public static class NoteService
    {
        // ═══════ 读取全部笔记 ═══════

        public static List<NoteItem> GetAll()
        {
            EnsureDirs();
            var data = JsonHelper.Load<NoteListData>(AppPaths.NotesIndex)
                       ?? new NoteListData();
            return data.Notes
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }

        // ═══════ 新增一条（含图片） ═══════

        /// <summary>
        /// 新增笔记。
        /// content 可空；cropped 为截图（可空，空表示纯文字笔记）。
        /// 返回保存后的 NoteItem。
        /// </summary>
        public static NoteItem Add(string content, Bitmap? cropped)
        {
            EnsureDirs();

            var item = new NoteItem
            {
                Id = Guid.NewGuid().ToString(),
                Content = content ?? "",
                CreatedAt = DateTime.Now
            };

            if (cropped != null)
            {
                string imgName = item.Id + ".jpg";
                string thumbName = item.Id + ".jpg";

                string imgPath = Path.Combine(AppPaths.NotesImagesDir, imgName);
                string thumbPath = Path.Combine(AppPaths.NotesThumbsDir, thumbName);

                if (ScreenshotService.SaveJpg(cropped, imgPath, 85L))
                {
                    item.ImageFileName = imgName;

                    if (ScreenshotService.SaveThumbnail(cropped, thumbPath, 400))
                        item.ThumbFileName = thumbName;
                }
            }

            // 写入索引
            var data = JsonHelper.Load<NoteListData>(AppPaths.NotesIndex)
                       ?? new NoteListData();
            data.Notes.Add(item);
            JsonHelper.Save(AppPaths.NotesIndex, data);

            return item;
        }

        // ═══════ 删除一条 ═══════

        public static bool Delete(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return false;

                var data = JsonHelper.Load<NoteListData>(AppPaths.NotesIndex)
                           ?? new NoteListData();
                var target = data.Notes.FirstOrDefault(n => n.Id == id);
                if (target == null) return false;

                // 删图片
                if (!string.IsNullOrWhiteSpace(target.ImageFileName))
                {
                    try
                    {
                        string p = Path.Combine(AppPaths.NotesImagesDir, target.ImageFileName);
                        if (File.Exists(p)) File.Delete(p);
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(target.ThumbFileName))
                {
                    try
                    {
                        string p = Path.Combine(AppPaths.NotesThumbsDir, target.ThumbFileName);
                        if (File.Exists(p)) File.Delete(p);
                    }
                    catch { }
                }

                data.Notes.RemoveAll(n => n.Id == id);
                JsonHelper.Save(AppPaths.NotesIndex, data);
                return true;
            }
            catch { return false; }
        }

        // ═══════ 清空 ═══════

        public static void ClearAll()
        {
            try
            {
                var data = JsonHelper.Load<NoteListData>(AppPaths.NotesIndex)
                           ?? new NoteListData();

                // 逐条删图
                foreach (var n in data.Notes.ToList())
                    Delete(n.Id);

                // 保险：再清空索引
                JsonHelper.Save(AppPaths.NotesIndex, new NoteListData());
            }
            catch { }
        }

        // ═══════ 路径 ═══════

        public static string GetImagePath(NoteItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ImageFileName))
                return "";
            return Path.Combine(AppPaths.NotesImagesDir, item.ImageFileName);
        }

        public static string GetThumbPath(NoteItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ThumbFileName))
                return "";
            return Path.Combine(AppPaths.NotesThumbsDir, item.ThumbFileName);
        }

        // ═══════ 目录 ═══════

        private static void EnsureDirs()
        {
            try
            {
                if (!Directory.Exists(AppPaths.NotesDir))
                    Directory.CreateDirectory(AppPaths.NotesDir);
                if (!Directory.Exists(AppPaths.NotesImagesDir))
                    Directory.CreateDirectory(AppPaths.NotesImagesDir);
                if (!Directory.Exists(AppPaths.NotesThumbsDir))
                    Directory.CreateDirectory(AppPaths.NotesThumbsDir);
            }
            catch { }
        }
    }
}