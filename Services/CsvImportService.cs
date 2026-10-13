﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CMW
{
    /// <summary>
    /// ★ 27.0.0：CSV 课表导入
    /// 格式：
    ///   节次,周一,周二,周三,周四,周五,周六
    ///   1,数学,语文,英语,物理,化学,生物
    ///   2,...
    /// 第 1 列是节次号（1 起）
    /// 其余列按顺序对应周一~周六
    /// </summary>
    public static class CsvImportService
    {
        /// <summary>
        /// 解析 CSV，返回课程列表；失败时 error 非空
        /// </summary>
        public static (List<ScheduleItem>? Items, string Error) ParseCsv(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return (null, "文件不存在");

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2)
                    return (null, "文件至少需要 2 行（表头 + 数据）");

                // 表头至少要有 2 列
                var header = SplitCsvLine(lines[0]);
                if (header.Length < 2)
                    return (null, "表头至少需要 2 列（节次 + 星期）");

                int maxCols = Math.Min(header.Length - 1, 6);   // 最多 6 个星期
                var items = new List<ScheduleItem>();

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cells = SplitCsvLine(line);
                    if (cells.Length < 2) continue;

                    if (!int.TryParse(cells[0].Trim(), out int period)) continue;
                    if (period < 1 || period > 20) continue;

                    for (int w = 0; w < maxCols; w++)
                    {
                        int col = 1 + w;
                        if (col >= cells.Length) break;

                        string lesson = cells[col].Trim();
                        if (string.IsNullOrWhiteSpace(lesson)) continue;

                        items.Add(new ScheduleItem
                        {
                            WeekDay = w + 1,
                            Period = period,
                            LessonName = lesson,
                            Teacher = ""
                        });
                    }
                }

                return (items, "");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// 生成 CSV 模板（供用户下载后填写）
        /// </summary>
        public static string GenerateTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("节次,周一,周二,周三,周四,周五,周六");
            sb.AppendLine("1,数学,语文,英语,物理,化学,生物");
            sb.AppendLine("2,语文,数学,物理,化学,生物,英语");
            sb.AppendLine("3,英语,物理,数学,生物,语文,化学");
            sb.AppendLine("4,物理,英语,化学,数学,生物,语文");
            sb.AppendLine("5,化学,生物,语文,英语,物理,数学");
            sb.AppendLine("6,生物,化学,英语,语文,数学,物理");
            sb.AppendLine("7,自习,自习,自习,自习,自习,自习");
            sb.AppendLine("8,自习,自习,自习,自习,自习,自习");
            return sb.ToString();
        }

        // ═══════ 简单 CSV 拆分（支持引号） ═══════

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuote = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = !inQuote;
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());

            return result.ToArray();
        }
    }
}