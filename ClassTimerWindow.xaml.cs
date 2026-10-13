using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CMW
{
    public partial class ClassTimerWindow : Window
    {
        private const int DefaultSeconds = 300;   // 5 分钟
        private const int MinSeconds = 60;        // 最少 1 分钟
        private const int MaxSeconds = 60 * 60;   // 最多 60 分钟

        private int _totalSeconds = DefaultSeconds;
        private int _remainingSeconds = DefaultSeconds;
        private bool _isRunning;
        private bool _isFlashing;

        private readonly DispatcherTimer _timer;

        private static readonly Brush NormalBg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        private static readonly Brush AlarmBg = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);

        public ClassTimerWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;

            _remainingSeconds = _totalSeconds;

            Loaded += ClassTimerWindow_Loaded;
            KeyDown += ClassTimerWindow_KeyDown;
        }

        private void ClassTimerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDisplay();
        }

        private void ClassTimerWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Space)
            {
                ToggleStartPause();
                e.Handled = true;
            }
        }

        // ═══════ 拖动 ═══════

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        // ═══════ 显示 ═══════

        private void UpdateDisplay()
        {
            int m = _remainingSeconds / 60;
            int s = _remainingSeconds % 60;
            tbTime.Text = $"{m:D2}:{s:D2}";

            if (_isFlashing) return;

            if (_isRunning)
            {
                tbStatus.Text = "计时中...";
                BtnStartPause.Content = "暂停";
            }
            else
            {
                tbStatus.Text = _remainingSeconds == _totalSeconds ? "就绪" : "已暂停";
                BtnStartPause.Content = _remainingSeconds < _totalSeconds && _remainingSeconds > 0
                    ? "继续"
                    : "开始";
            }

            BtnMinus.IsEnabled = !_isRunning;
            BtnPlus.IsEnabled = !_isRunning;
        }

        // ═══════ 控制 ═══════

        private void BtnMinus_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _totalSeconds = Math.Max(MinSeconds, _totalSeconds - 60);
            _remainingSeconds = _totalSeconds;
            UpdateDisplay();
        }

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _totalSeconds = Math.Min(MaxSeconds, _totalSeconds + 60);
            _remainingSeconds = _totalSeconds;
            UpdateDisplay();
        }

        private void BtnStartPause_Click(object sender, RoutedEventArgs e)
        {
            ToggleStartPause();
        }

        private void ToggleStartPause()
        {
            if (_isRunning)
            {
                _isRunning = false;
                _timer.Stop();
                UpdateDisplay();
                return;
            }

            if (_remainingSeconds <= 0)
            {
                _remainingSeconds = _totalSeconds;
            }

            _isRunning = true;
            _timer.Start();
            UpdateDisplay();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _isRunning = false;
            _timer.Stop();
            _remainingSeconds = _totalSeconds;

            _isFlashing = false;
            RootGrid.Background = null;
            Background = NormalBg;

            UpdateDisplay();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;

                if (_remainingSeconds == 0)
                {
                    _isRunning = false;
                    _timer.Stop();
                    UpdateDisplay();
                    _ = FlashAndBeepAsync();
                    return;
                }
            }

            UpdateDisplay();
        }

        // ═══════ 时间到：闪烁 + 蜂鸣 ═══════

        private async Task FlashAndBeepAsync()
        {
            _isFlashing = true;

            tbStatus.Text = "⏰ 时间到！";
            BtnStartPause.Content = "开始";

            try { MessageBeep(0x00000030); } catch { }

            for (int i = 0; i < 8; i++)
            {
                Background = (i % 2 == 0) ? AlarmBg : NormalBg;
                await Task.Delay(200);
            }

            Background = NormalBg;
            _isFlashing = false;
            tbStatus.Text = "时间到，可点「重置」";
        }

        // ═══════ 置顶 ═══════

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            BtnPin.Foreground = Topmost
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B9DF0"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0FFFFFF"));
            BtnPin.ToolTip = Topmost ? "取消置顶" : "置顶";
        }

        // ═══════ 关闭 ═══════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}