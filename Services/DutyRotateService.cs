using System;
using System.Collections.Generic;
using System.Linq;

namespace CMW
{
    /// <summary>
    /// ★ 28.0.0：值日组自动轮换服务
    /// 从 DutyPage 抽离出来，让 App 启动 / 主窗口激活 都能触发检查
    /// （原来只在 DutyPage_Loaded 触发，导致用户不进值日页就不轮换）
    /// </summary>
    public static class DutyRotateService
    {
        /// <summary>
        /// 检查并执行轮换（幂等，当天已轮换则跳过）
        /// 触发点：
        ///   1. App.OnStartup
        ///   2. MainWindow.Activated
        ///   3. DutyPage_Loaded（保险）
        /// </summary>
        public static void TryRotateIfNeeded()
        {
            try
            {
                var config = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                if (!config.DutyAutoRotateEnabled) return;

                var duty = JsonHelper.Load<DutySaveModel>(AppPaths.Duty) ?? new DutySaveModel();
                if (duty.DutyGroups == null || duty.DutyGroups.Count == 0) return;

                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (config.DutyLastRotateDate == today) return;

                int currentIdx = -1;
                if (!string.IsNullOrEmpty(duty.TodayDutyGroupName))
                {
                    currentIdx = duty.DutyGroups.FindIndex(
                        g => g.GroupName == duty.TodayDutyGroupName);
                }

                int nextIdx = (currentIdx + 1) % duty.DutyGroups.Count;
                duty.TodayDutyGroupName = duty.DutyGroups[nextIdx].GroupName;

                config.DutyLastRotateDate = today;

                JsonHelper.Save(AppPaths.Config, config);
                JsonHelper.Save(AppPaths.Duty, duty);
            }
            catch { }
        }
    }
}