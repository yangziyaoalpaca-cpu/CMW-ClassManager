using System.Windows;

namespace CMW
{
    /// <summary>
    /// ★ 32.0.0：BIOS 定时唤醒（RTC Wake）设置引导
    /// 纯图文引导，不做硬件检测、不引入额外依赖。
    /// </summary>
    public partial class BiosWakeGuideWindow : Window
    {
        public BiosWakeGuideWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}