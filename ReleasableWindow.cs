using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace CMW
{
    /// <summary>
    /// ★ 32.0.0：可释放窗口基类
    /// 提供统一的资源释放机制，避免窗口关闭后定时器继续运行、事件继续触发。
    /// 派生类通过 RegisterTimer / RegisterCleanup 注册需要在关闭时释放的资源。
    /// </summary>
    public abstract class ReleasableWindow : Window
    {
        private readonly List<DispatcherTimer> _timers = new List<DispatcherTimer>();
        private readonly List<Action> _cleanups = new List<Action>();
        private bool _released;

        /// <summary>注册需要在窗口关闭时停止的定时器</summary>
        protected void RegisterTimer(DispatcherTimer timer)
        {
            if (timer == null) return;
            if (!_timers.Contains(timer)) _timers.Add(timer);
        }

        /// <summary>注册需要在窗口关闭时执行的清理操作（事件退订、资源释放等）</summary>
        protected void RegisterCleanup(Action cleanup)
        {
            if (cleanup == null) return;
            _cleanups.Add(cleanup);
        }

        /// <summary>手动触发一次释放（幂等，多次调用只执行一次）</summary>
        protected void Release()
        {
            if (_released) return;
            _released = true;

            foreach (var t in _timers)
            {
                try { t.Stop(); } catch { }
            }
            _timers.Clear();

            foreach (var c in _cleanups)
            {
                try { c(); } catch { }
            }
            _cleanups.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            Release();
            base.OnClosed(e);
        }
    }
}