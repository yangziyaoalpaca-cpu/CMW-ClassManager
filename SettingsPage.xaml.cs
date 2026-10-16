using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CMW
{
    public partial class SettingsPage : UserControl
    {
        // ★ 33.0.0 修复：字段直接初始化，避免 InitializeComponent 期间为 null
        private AppConfig _currentConfig = new AppConfig();
        private bool _isLoading;
        private StackPanel? _currentPanel;

        private bool _blurNeedsRegen = false;

        private List<string> _dutyRemindTimes = new List<string>();
        private List<string> _todoRemindTimes = new List<string>();

        // C2：横条组显示顺序（工作副本）
        private List<int> _barGroupOrder = new List<int>();
        private const double BarGroupRowHeight = 44;
        private const double BarGroupDragThreshold = 8;

        private Border? _draggingBarRow = null;
        private int _draggingOriginalIndex = -1;
        private double _dragStartScreenY = 0;
        private bool _isBarGroupDragging = false;

        private static readonly Dictionary<int, string> BarGroupNames = new Dictionary<int, string>
        {
            { 0, "日期时间组" },
            { 1, "课表组" },
            { 2, "状态组" },
            { 4, "作业组" },
            { 5, "值日组" },
            { 6, "待办组" }
        };

        // C3：多条考试倒计时（工作副本）
        private List<ExamCountdownItem> _examCountdowns = new List<ExamCountdownItem>();

        public SettingsPage()
        {
            // ★ 33.0.0 修复：InitializeComponent 期间阻止 SelectionChanged/ValueChanged 访问
            _isLoading = true;

            InitializeComponent();
            _currentConfig = new AppConfig();
            Loaded += SettingsPage_Loaded;

            sliderGlobalRadius.ValueChanged += (s, e) =>
            {
                int r = (int)sliderGlobalRadius.Value;
                tbGlobalRadius.Text = $"当前：{r} px";
                RadiusPreview.CornerRadius = new CornerRadius(r);
            };

            sliderBlur.ValueChanged += (s, e) =>
            {
                tbBlurValue.Text = $"当前值：{sliderBlur.Value:F0}";
                if (!_isLoading)
                {
                    App.NotifyBlurRadiusChanged(sliderBlur.Value);
                    _blurNeedsRegen = true;
                }
            };

            sliderIslandFontScale.ValueChanged += (s, e) =>
            {
                tbIslandFontScale.Text = $"当前：{sliderIslandFontScale.Value:F1} 倍";
            };
            sliderIslandAutoCollapse.ValueChanged += (s, e) =>
            {
                tbIslandAutoCollapse.Text = $"当前：{sliderIslandAutoCollapse.Value:F0} 秒";
            };
            sliderMiniRemindDuration.ValueChanged += (s, e) =>
            {
                tbMiniRemindDuration.Text = $"当前：{sliderMiniRemindDuration.Value:F0} 秒";
            };
            sliderNotifyWidth.ValueChanged += (s, e) =>
            {
                tbNotifyWidth.Text = $"当前：{sliderNotifyWidth.Value:F0} %";
            };
            sliderNotifyHeight.ValueChanged += (s, e) =>
            {
                tbNotifyHeight.Text = $"当前：{sliderNotifyHeight.Value:F0} %";
            };
            sliderRightWidth.ValueChanged += (s, e) =>
            {
                tbRightWidth.Text = $"当前：{sliderRightWidth.Value:F0} px";
            };
            sliderRightHeight.ValueChanged += (s, e) =>
            {
                tbRightHeight.Text = $"当前：{sliderRightHeight.Value:F0} %";
            };
            sliderRightAutoSec.ValueChanged += (s, e) =>
            {
                tbRightAutoSec.Text = $"当前：{sliderRightAutoSec.Value:F0} 秒";
            };
            sliderRightFontScale.ValueChanged += (s, e) =>
            {
                tbRightFontScale.Text = $"当前：{sliderRightFontScale.Value:F1} 倍";
            };

            sliderBlueFlash.ValueChanged += (s, e) =>
            {
                tbBlueFlash.Text = $"当前：{sliderBlueFlash.Value:F0} 秒";
            };

            sliderBarWidth.ValueChanged += (s, e) =>
            {
                double v = sliderBarWidth.Value;
                tbBarWidth.Text = $"当前：{v:F0} px";
                UpdateBarWidthActual(v);
            };

            sliderCircleDiameter.ValueChanged += (s, e) =>
            {
                tbCircleDiameter.Text = $"当前：{sliderCircleDiameter.Value:F0} px";
            };
            sliderRotateSeconds.ValueChanged += (s, e) =>
            {
                tbRotateSeconds.Text = $"当前：{sliderRotateSeconds.Value:F0} 秒";
            };
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                LoadConfig();
                LoadFontList();
                LoadThemeList();
                BindUiFromConfig();
                _currentPanel = PanelBasic;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabBasic" => PanelBasic,
                "TabBackground" => PanelBackground,
                "TabIsland" => PanelIsland,
                "TabFloat" => PanelFloat,
                "TabTeaching" => PanelTeaching,
                "TabStartup" => PanelStartup,
                "TabData" => PanelData,
                "TabAbout" => PanelAbout,
                _ => null
            };

            if (target == null || target == _currentPanel) return;
            SwitchPanel(target);
        }

        private void SwitchPanel(StackPanel target)
        {
            if (_currentPanel != null)
                _currentPanel.Visibility = Visibility.Collapsed;

            target.Visibility = Visibility.Visible;
            target.Opacity = 0;

            var translate = new TranslateTransform(30, 0);
            target.RenderTransform = translate;

            var txAnim = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            var opAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            txAnim.Completed += (s, e) => { translate.X = 0; };
            opAnim.Completed += (s, e) => { target.Opacity = 1; };

            translate.BeginAnimation(TranslateTransform.XProperty, txAnim);
            target.BeginAnimation(UIElement.OpacityProperty, opAnim);

            _currentPanel = target;
        }

        private void LoadConfig()
        {
            _currentConfig = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
        }

        private void SaveConfigToFile()
        {
            JsonHelper.Save(AppPaths.Config, _currentConfig);
        }

        private void LoadFontList()
        {
            cbFontFamily.Items.Clear();
            foreach (var font in FontHelper.AvailableFonts)
            {
                cbFontFamily.Items.Add(new ComboBoxItem { Content = font });
            }
        }

        private void LoadThemeList()
        {
            cbTheme.Items.Clear();
            cbTheme.Items.Add(new ComboBoxItem { Content = "PCL2（浅色）", Tag = "PCL2" });
            cbTheme.Items.Add(new ComboBoxItem { Content = "ClassIsland（深色）", Tag = "ClassIsland" });
        }

        private void BindUiFromConfig()
        {
            txtClassName.Text = _currentConfig.ClassName;

            // ★ 33.0.0：全局 R 角
            int globalRadius = _currentConfig.GlobalRadius;
            if (globalRadius < 4) globalRadius = 4;
            if (globalRadius > 24) globalRadius = 24;
            sliderGlobalRadius.Value = globalRadius;
            tbGlobalRadius.Text = $"当前：{globalRadius} px";
            RadiusPreview.CornerRadius = new CornerRadius(globalRadius);

            sliderBlur.Value = _currentConfig.BlurRadius;
            tbBlurValue.Text = $"当前值：{_currentConfig.BlurRadius:F0}";

            string theme = _currentConfig.ThemeName ?? "PCL2";
            if (theme == "ClassIsland")
                cbTheme.SelectedIndex = 1;
            else
                cbTheme.SelectedIndex = 0;

            toggleBlurEnabled.IsChecked = _currentConfig.BackgroundBlurEnabled;

            // B3：背景样式
            string preset = _currentConfig.BackgroundPreset ?? "None";
            int presetIdx = 0;
            switch (preset)
            {
                case "DeepBlue": presetIdx = 1; break;
                case "MorningMist": presetIdx = 2; break;
                case "NightPurple": presetIdx = 3; break;
                case "InkGreen": presetIdx = 4; break;
                case "None":
                default: presetIdx = 0; break;
            }
            if (presetIdx >= 0 && presetIdx < cbBackgroundPreset.Items.Count)
                cbBackgroundPreset.SelectedIndex = presetIdx;

            bool found = false;
            foreach (ComboBoxItem item in cbFontFamily.Items)
            {
                string name = item.Content?.ToString() ?? "";
                if (string.Equals(name, _currentConfig.FontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    cbFontFamily.SelectedItem = item;
                    found = true;
                    break;
                }
            }
            if (!found && cbFontFamily.Items.Count > 0)
                cbFontFamily.SelectedIndex = 0;

            toggleFontAutoFit.IsChecked = _currentConfig.FontAutoFitEnabled;

            UpdateBackgroundPathDisplay();

            // C3：多条考试倒计时（含旧字段迁移）
            _examCountdowns = new List<ExamCountdownItem>(
                _currentConfig.ExamCountdownList ?? new List<ExamCountdownItem>());

            if (_examCountdowns.Count == 0
                && !string.IsNullOrWhiteSpace(_currentConfig.ExamName)
                && !string.IsNullOrWhiteSpace(_currentConfig.ExamDate))
            {
                _examCountdowns.Add(new ExamCountdownItem
                {
                    Name = _currentConfig.ExamName,
                    Date = _currentConfig.ExamDate
                });
            }

            RefreshExamCountdownList();

            txtNewExamName.Text = "";
            dpNewExamDate.SelectedDate = null;

            toggleDutyAutoRotate.IsChecked = _currentConfig.DutyAutoRotateEnabled;

            // A9：上下课表现方式
            switch (_currentConfig.ClassChangeStyle)
            {
                case "NotifyOnly":
                    cbClassChangeStyle.SelectedIndex = 1;
                    break;
                case "ColorAndBar":
                    cbClassChangeStyle.SelectedIndex = 2;
                    break;
                case "None":
                    cbClassChangeStyle.SelectedIndex = 3;
                    break;
                case "ColorThenNotify":
                default:
                    cbClassChangeStyle.SelectedIndex = 0;
                    break;
            }

            double nw = _currentConfig.NotifyBarWidthPercent;
            if (nw < 50) nw = 96;
            if (nw > 100) nw = 100;
            sliderNotifyWidth.Value = nw;
            tbNotifyWidth.Text = $"当前：{nw:F0} %";

            double nh = _currentConfig.NotifyBarHeightPercent;
            if (nh < 5) nh = 10;
            if (nh > 30) nh = 30;
            sliderNotifyHeight.Value = nh;
            tbNotifyHeight.Text = $"当前：{nh:F0} %";

            switch (_currentConfig.IslandCardSize)
            {
                case "Small": cbIslandSize.SelectedIndex = 0; break;
                case "Large": cbIslandSize.SelectedIndex = 2; break;
                case "XLarge": cbIslandSize.SelectedIndex = 3; break;
                case "Medium":
                default: cbIslandSize.SelectedIndex = 1; break;
            }

            switch (_currentConfig.IslandCapsuleTextMode)
            {
                case "Countdown": cbCapsuleTextMode.SelectedIndex = 1; break;
                case "NextClass": cbCapsuleTextMode.SelectedIndex = 2; break;
                case "HomeworkCount": cbCapsuleTextMode.SelectedIndex = 3; break;
                case "Auto": cbCapsuleTextMode.SelectedIndex = 4; break;
                case "CMW":
                default: cbCapsuleTextMode.SelectedIndex = 0; break;
            }

            cbNotifyMode.SelectedIndex = _currentConfig.NotifyClickOutsideToClose ? 1 : 0;

            double fs = _currentConfig.IslandFontScale;
            if (fs < 0.8) fs = 0.8;
            if (fs > 1.5) fs = 1.5;
            sliderIslandFontScale.Value = fs;
            tbIslandFontScale.Text = $"当前：{fs:F1} 倍";

            int auto = _currentConfig.IslandAutoCollapseSeconds;
            if (auto < 2) auto = 2;
            if (auto > 60) auto = 60;
            sliderIslandAutoCollapse.Value = auto;
            tbIslandAutoCollapse.Text = $"当前：{auto} 秒";

            txtSilentStart.Text = _currentConfig.SilentStart ?? "";
            txtSilentEnd.Text = _currentConfig.SilentEnd ?? "";

            // 横条
            toggleBarEnabled.IsChecked = _currentConfig.IslandBarEnabled;
            toggleBarAutoExtend.IsChecked = _currentConfig.IslandBarAutoExtendOnClassChange;
            toggleBarManualExtend.IsChecked = _currentConfig.IslandBarManualExtendEnabled;

            // ★ 新增：横条常驻 + 缩回目标
            toggleBarPersistent.IsChecked = _currentConfig.IslandBarPersistent;

            switch (_currentConfig.MinimizeTarget)
            {
                case "Bar": cbMinimizeTarget.SelectedIndex = 1; break;
                case "Island":
                default: cbMinimizeTarget.SelectedIndex = 0; break;
            }

            int blueSec = _currentConfig.IslandBlueFlashSeconds;
            if (blueSec < 0) blueSec = 2;
            if (blueSec > 10) blueSec = 10;
            sliderBlueFlash.Value = blueSec;
            tbBlueFlash.Text = $"当前：{blueSec} 秒";

            double bw = _currentConfig.IslandBarWidth;
            if (bw < 50) bw = 50;
            if (bw > 1920) bw = 1920;
            sliderBarWidth.Value = bw;
            tbBarWidth.Text = $"当前：{bw:F0} px";
            UpdateBarWidthActual(bw);

            double cd = _currentConfig.IslandCircleDiameter;
            if (cd < 30) cd = 40;
            if (cd > 150) cd = 150;
            sliderCircleDiameter.Value = cd;
            tbCircleDiameter.Text = $"当前：{cd:F0} px";

            toggleBarAutoRotate.IsChecked = _currentConfig.IslandBarAutoRotateEnabled;

            int rot = _currentConfig.IslandBarRotateSeconds;
            if (rot < 3) rot = 8;
            if (rot > 60) rot = 60;
            sliderRotateSeconds.Value = rot;
            tbRotateSeconds.Text = $"当前：{rot} 秒";

            toggleGroupDate.IsChecked = _currentConfig.IslandBarShowDateGroup;
            toggleGroupSchedule.IsChecked = _currentConfig.IslandBarShowScheduleGroup;
            toggleGroupStatus.IsChecked = _currentConfig.IslandBarShowStatusGroup;
            toggleGroupHomework.IsChecked = _currentConfig.IslandBarShowHomeworkGroup;
            toggleGroupDuty.IsChecked = _currentConfig.IslandBarShowDutyGroup;
            toggleGroupTodo.IsChecked = _currentConfig.IslandBarShowTodoGroup;

            // C2：加载并重建组顺序面板
            _barGroupOrder = new List<int>(
                _currentConfig.IslandBarGroupOrder ?? new List<int> { 0, 1, 2, 4, 5, 6 });

            var allIds = new[] { 0, 1, 2, 4, 5, 6 };
            foreach (var id in allIds)
            {
                if (!_barGroupOrder.Contains(id)) _barGroupOrder.Add(id);
            }
            _barGroupOrder = _barGroupOrder.Where(id => allIds.Contains(id)).ToList();

            RebuildBarGroupOrderPanel();

            toggleClassStartRemind.IsChecked = _currentConfig.ClassStartRemindEnabled;
            toggleClassEndRemind.IsChecked = _currentConfig.ClassEndRemindEnabled;

            switch (_currentConfig.ClassRemindStyle)
            {
                case "Mini": cbClassRemindStyle.SelectedIndex = 1; break;
                case "Both": cbClassRemindStyle.SelectedIndex = 2; break;
                case "None": cbClassRemindStyle.SelectedIndex = 3; break;
                case "Popup":
                default: cbClassRemindStyle.SelectedIndex = 0; break;
            }

            int mini = _currentConfig.MiniRemindDurationSeconds;
            if (mini < 2) mini = 5;
            if (mini > 30) mini = 30;
            sliderMiniRemindDuration.Value = mini;
            tbMiniRemindDuration.Text = $"当前：{mini} 秒";

            _dutyRemindTimes = new List<string>(_currentConfig.DutyRemindTimeList ?? new List<string>());
            if (_dutyRemindTimes.Count == 0 && !string.IsNullOrWhiteSpace(_currentConfig.DutyRemindTime))
                _dutyRemindTimes.Add(_currentConfig.DutyRemindTime);
            _dutyRemindTimes = _dutyRemindTimes
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => TimeSpan.TryParse(t, out var ts) ? ts : TimeSpan.Zero)
                .ToList();

            _todoRemindTimes = new List<string>(_currentConfig.TodoRemindTimeList ?? new List<string>());
            if (_todoRemindTimes.Count == 0 && !string.IsNullOrWhiteSpace(_currentConfig.TodoCustomRemindTime))
                _todoRemindTimes.Add(_currentConfig.TodoCustomRemindTime);
            _todoRemindTimes = _todoRemindTimes
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => TimeSpan.TryParse(t, out var ts) ? ts : TimeSpan.Zero)
                .ToList();

            toggleDutyRemind.IsChecked = _currentConfig.DutyRemindEnabled;
            toggleTodoCustomRemind.IsChecked = _currentConfig.TodoCustomRemindEnabled;
            RefreshDutyTimes();
            RefreshTodoTimes();

            toggleRightEnabled.IsChecked = _currentConfig.FloatWindowEnabled;
            toggleRightAutoHide.IsChecked = _currentConfig.RightPanelAutoHideOnClassStart;
            toggleRightAutoShow.IsChecked = _currentConfig.RightPanelAutoShowOnClassEnd;
            toggleRightOwnTimeSlots.IsChecked = _currentConfig.RightPanelUseOwnTimeSlots;

            double rw = _currentConfig.FloatWindowWidth;
            if (rw < 200) rw = 360;
            if (rw > 600) rw = 600;
            sliderRightWidth.Value = rw;
            tbRightWidth.Text = $"当前：{rw:F0} px";

            double rh = _currentConfig.RightPanelHeightPercent;
            if (rh < 30) rh = 70;
            if (rh > 90) rh = 90;
            sliderRightHeight.Value = rh;
            tbRightHeight.Text = $"当前：{rh:F0} %";

            switch (_currentConfig.RightPanelContentMode)
            {
                case "Homework": cbRightContentMode.SelectedIndex = 0; break;
                case "Duty": cbRightContentMode.SelectedIndex = 1; break;
                case "Todo": cbRightContentMode.SelectedIndex = 2; break;
                case "Auto":
                default: cbRightContentMode.SelectedIndex = 3; break;
            }

            int rsec = _currentConfig.RightPanelAutoSwitchSeconds;
            if (rsec < 5) rsec = 15;
            if (rsec > 60) rsec = 60;
            sliderRightAutoSec.Value = rsec;
            tbRightAutoSec.Text = $"当前：{rsec} 秒";

            double rfs = _currentConfig.RightPanelFontScale;
            if (rfs < 0.8) rfs = 0.8;
            if (rfs > 1.5) rfs = 1.5;
            sliderRightFontScale.Value = rfs;
            tbRightFontScale.Text = $"当前：{rfs:F1} 倍";

            toggleBoardAutoSchedule.IsChecked = _currentConfig.BoardAutoScheduleEnabled;
            txtBoardStartTime.Text = _currentConfig.BoardAutoStartTime ?? "07:30";
            txtBoardEndTime.Text = _currentConfig.BoardAutoEndTime ?? "18:00";

            toggleAutoStart.IsChecked = _currentConfig.AutoStart;
            togglePowerSchedule.IsChecked = _currentConfig.PowerScheduleEnabled;
            txtPowerOnTime.Text = _currentConfig.PowerOnTime ?? "07:00";
            txtPowerOffTime.Text = _currentConfig.PowerOffTime ?? "22:00";

            pwdCurrent.Password = "";
            pwdNew.Password = "";
            pwdConfirm.Password = "";
            tbPwdTip.Text = "";
        }

        // ═══════════════════════════════════════════════════════════
        // ★ 33.0.0：切换即生效的事件处理器
        // ═══════════════════════════════════════════════════════════

        private void CbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbTheme.SelectedItem is not ComboBoxItem item) return;

            string themeName = item.Tag?.ToString() ?? "PCL2";
            if (themeName == _currentConfig.ThemeName) return;

            try
            {
                _currentConfig.ThemeName = themeName;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.ApplyTheme(themeName);
                tbTip.Text = $"✅ 已切换到「{item.Content}」主题";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 主题切换失败：" + ex.Message;
            }
        }

        private void CbFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbFontFamily.SelectedItem is not ComboBoxItem item) return;

            string fontName = item.Content?.ToString() ?? "Microsoft YaHei UI";
            if (fontName == _currentConfig.FontFamily) return;

            try
            {
                _currentConfig.FontFamily = fontName;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.ApplyFontToApp(fontName);
                tbTip.Text = $"✅ 已切换到「{fontName}」字体";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 字体切换失败：" + ex.Message;
            }
        }

        private void CbClassChangeStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbClassChangeStyle.SelectedItem is not ComboBoxItem item) return;

            string style = item.Tag?.ToString() ?? "ColorThenNotify";
            if (style == _currentConfig.ClassChangeStyle) return;

            try
            {
                _currentConfig.ClassChangeStyle = style;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifyRemindConfigChanged();
                tbTip.Text = "✅ 上下课表现方式已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbBackgroundPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbBackgroundPreset.SelectedItem is not ComboBoxItem item) return;

            string preset = item.Tag?.ToString() ?? "None";
            if (preset == _currentConfig.BackgroundPreset) return;

            try
            {
                _currentConfig.BackgroundPreset = preset;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = $"✅ 背景样式已切换到「{item.Content}」";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbIslandSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbIslandSize.SelectedItem is not ComboBoxItem item) return;

            string size = item.Tag?.ToString() ?? "Medium";
            if (size == _currentConfig.IslandCardSize) return;

            try
            {
                _currentConfig.IslandCardSize = size;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = "✅ 灵动岛展开大小已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbCapsuleTextMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbCapsuleTextMode.SelectedItem is not ComboBoxItem item) return;

            string mode = item.Tag?.ToString() ?? "CMW";
            if (mode == _currentConfig.IslandCapsuleTextMode) return;

            try
            {
                _currentConfig.IslandCapsuleTextMode = mode;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = "✅ 胶囊显示内容已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbNotifyMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            bool manual = cbNotifyMode.SelectedIndex == 1;
            if (manual == _currentConfig.NotifyClickOutsideToClose) return;

            try
            {
                _currentConfig.NotifyClickOutsideToClose = manual;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = "✅ 通知收回方式已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbClassRemindStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbClassRemindStyle.SelectedItem is not ComboBoxItem item) return;

            string style = item.Tag?.ToString() ?? "Popup";
            if (style == _currentConfig.ClassRemindStyle) return;

            try
            {
                _currentConfig.ClassRemindStyle = style;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifyRemindConfigChanged();
                tbTip.Text = "✅ 提醒样式已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void CbRightContentMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbRightContentMode.SelectedItem is not ComboBoxItem item) return;

            string mode = item.Tag?.ToString() ?? "Auto";
            if (mode == _currentConfig.RightPanelContentMode) return;

            try
            {
                _currentConfig.RightPanelContentMode = mode;
                _currentConfig.FloatWindowContentMode = mode;
                _currentConfig.FloatWindowAutoSwitch = mode == "Auto";
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = "✅ 右侧窗口内容模式已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        // ★ 新增：主窗口缩回目标切换即生效
        private void CbMinimizeTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (cbMinimizeTarget.SelectedItem is not ComboBoxItem item) return;

            string target = item.Tag?.ToString() ?? "Island";
            if (target == _currentConfig.MinimizeTarget) return;

            try
            {
                _currentConfig.MinimizeTarget = target;
                JsonHelper.Save(AppPaths.Config, _currentConfig);
                App.NotifySettingsChanged();
                tbTip.Text = "✅ 主窗口缩回目标已切换";
            }
            catch (Exception ex)
            {
                tbTip.Text = "❌ 切换失败：" + ex.Message;
            }
        }

        private void UpdateBarWidthActual(double value)
        {
            try
            {
                var wa = SystemParameters.WorkArea;
                double maxByScreen = wa.Width * 0.9;
                double actual = value;
                if (actual > maxByScreen) actual = maxByScreen;
                if (actual < 50) actual = 50;

                if (Math.Abs(actual - value) > 0.5)
                {
                    tbBarWidthActual.Text = $"（当前屏幕实际生效：{actual:F0} px，被屏幕宽度 90% 限制）";
                }
                else
                {
                    tbBarWidthActual.Text = "";
                }
            }
            catch
            {
                tbBarWidthActual.Text = "";
            }
        }

        private void UpdateBackgroundPathDisplay()
        {
            if (!string.IsNullOrEmpty(_currentConfig.BackgroundImagePath)
                && File.Exists(_currentConfig.BackgroundImagePath))
            {
                txtBackgroundPath.Text = _currentConfig.BackgroundImagePath;
            }
            else
            {
                txtBackgroundPath.Text = "（内置默认背景）";
            }
        }

        // ═══════════════════════════════════════════════════════════
        // C2：横条组显示顺序 · 面板重建 + 拖动排序
        // ═══════════════════════════════════════════════════════════
        private void RebuildBarGroupOrderPanel()
        {
            panelBarGroupOrder.Children.Clear();
            for (int i = 0; i < _barGroupOrder.Count; i++)
            {
                var row = BuildBarGroupOrderRow(_barGroupOrder[i]);
                panelBarGroupOrder.Children.Add(row);
            }
        }

        private Border BuildBarGroupOrderRow(int groupId)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("ThemeBorderLightBrush"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0),
                MinHeight = BarGroupRowHeight,
                Tag = groupId
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var handle = new Border
            {
                Width = 40,
                Height = 40,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.SizeNS,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var handleText = new TextBlock
            {
                Text = "≡",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 20,
                Foreground = (Brush)FindResource("ThemeTextHintBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            handle.Child = handleText;

            handle.PreviewMouseLeftButtonDown += BarOrderHandle_PreviewMouseLeftButtonDown;
            handle.PreviewMouseMove += BarOrderHandle_PreviewMouseMove;
            handle.PreviewMouseLeftButtonUp += BarOrderHandle_PreviewMouseLeftButtonUp;
            handle.PreviewTouchDown += BarOrderHandle_PreviewTouchDown;
            handle.PreviewTouchMove += BarOrderHandle_PreviewTouchMove;
            handle.PreviewTouchUp += BarOrderHandle_PreviewTouchUp;

            Grid.SetColumn(handle, 0);
            grid.Children.Add(handle);

            var label = new TextBlock
            {
                Text = BarGroupNames.TryGetValue(groupId, out var name) ? name : $"组 {groupId}",
                FontFamily = (FontFamily)FindResource("GlobalFontFamily"),
                FontSize = 14,
                Foreground = (Brush)FindResource("ThemeTextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            border.Child = grid;

            var tt = new TranslateTransform(0, 0);
            border.RenderTransform = tt;

            return border;
        }

        private Border? FindRowFromHandle(object sender)
        {
            if (sender is not DependencyObject d) return null;
            DependencyObject? cur = d;
            while (cur != null)
            {
                if (cur is Border b && b.Tag is int) return b;
                cur = VisualTreeHelper.GetParent(cur);
            }
            return null;
        }

        private void BarOrderHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = FindRowFromHandle(sender);
            if (row == null) return;
            BeginBarGroupDrag(row);
            try { Mouse.Capture(row); } catch { }
            e.Handled = true;
        }

        private void BarOrderHandle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingBarRow == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndBarGroupDrag();
                return;
            }
            double curY = System.Windows.Forms.Cursor.Position.Y;
            UpdateBarGroupDrag(curY);
        }

        private void BarOrderHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingBarRow == null) return;
            EndBarGroupDrag();
            try { Mouse.Capture(null); } catch { }
            e.Handled = true;
        }

        private int _activeTouchId = -1;

        private void BarOrderHandle_PreviewTouchDown(object? sender, TouchEventArgs e)
        {
            var row = FindRowFromHandle(sender!);
            if (row == null) return;
            BeginBarGroupDrag(row);
            _activeTouchId = e.TouchDevice.Id;
            try { row.CaptureTouch(e.TouchDevice); } catch { }
            e.Handled = true;
        }

        private void BarOrderHandle_PreviewTouchMove(object? sender, TouchEventArgs e)
        {
            if (_draggingBarRow == null) return;
            if (e.TouchDevice.Id != _activeTouchId) return;
            double curY = System.Windows.Forms.Cursor.Position.Y;
            UpdateBarGroupDrag(curY);
            e.Handled = true;
        }

        private void BarOrderHandle_PreviewTouchUp(object? sender, TouchEventArgs e)
        {
            if (_draggingBarRow == null) return;
            if (e.TouchDevice.Id != _activeTouchId) return;
            EndBarGroupDrag();
            try { _draggingBarRow.ReleaseTouchCapture(e.TouchDevice); } catch { }
            _activeTouchId = -1;
            e.Handled = true;
        }

        private void BeginBarGroupDrag(Border row)
        {
            _draggingBarRow = row;
            _draggingOriginalIndex = panelBarGroupOrder.Children.IndexOf(row);
            _dragStartScreenY = System.Windows.Forms.Cursor.Position.Y;
            _isBarGroupDragging = false;

            if (row.RenderTransform is TranslateTransform tt)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = 0;
            }

            row.Opacity = 0.75;
            row.SetValue(Panel.ZIndexProperty, 100);
        }

        private void UpdateBarGroupDrag(double currentScreenY)
        {
            if (_draggingBarRow == null) return;

            double delta = currentScreenY - _dragStartScreenY;

            if (!_isBarGroupDragging)
            {
                if (Math.Abs(delta) < BarGroupDragThreshold) return;
                _isBarGroupDragging = true;
            }

            if (_draggingBarRow.RenderTransform is TranslateTransform tt)
            {
                tt.Y = delta;
            }
        }

        private void EndBarGroupDrag()
        {
            if (_draggingBarRow == null) return;

            var row = _draggingBarRow;
            int originalIndex = _draggingOriginalIndex;
            bool moved = _isBarGroupDragging;

            if (row.RenderTransform is TranslateTransform tt)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = 0;
            }
            row.Opacity = 1;
            row.SetValue(Panel.ZIndexProperty, 0);

            _draggingBarRow = null;
            _draggingOriginalIndex = -1;
            _isBarGroupDragging = false;

            if (!moved) return;

            double delta = System.Windows.Forms.Cursor.Position.Y - _dragStartScreenY;
            int steps = (int)Math.Round(delta / BarGroupRowHeight);

            int newIndex = originalIndex + steps;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= _barGroupOrder.Count) newIndex = _barGroupOrder.Count - 1;

            if (newIndex == originalIndex) return;

            int movedId = _barGroupOrder[originalIndex];
            _barGroupOrder.RemoveAt(originalIndex);
            _barGroupOrder.Insert(newIndex, movedId);

            RebuildBarGroupOrderPanel();
        }

        // ═══════════════════════════════════════════════════════════
        // C3：多条考试倒计时 · 列表
        // ═══════════════════════════════════════════════════════════
        private void RefreshExamCountdownList()
        {
            icExamCountdowns.ItemsSource = null;
            icExamCountdowns.ItemsSource = _examCountdowns;
        }

        private void BtnAddExamCountdown_Click(object sender, RoutedEventArgs e)
        {
            string name = (txtNewExamName.Text ?? "").Trim();
            DateTime? date = dpNewExamDate.SelectedDate;

            if (string.IsNullOrWhiteSpace(name))
            {
                tbTip.Text = "⚠️ 请输入考试名称";
                return;
            }

            if (!date.HasValue)
            {
                tbTip.Text = "⚠️ 请选择考试日期";
                return;
            }

            _examCountdowns.Add(new ExamCountdownItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Date = date.Value.ToString("yyyy-MM-dd")
            });

            txtNewExamName.Text = "";
            dpNewExamDate.SelectedDate = null;

            RefreshExamCountdownList();
            tbTip.Text = $"✅ 已添加「{name}」（记得保存设置）";
        }

        private void BtnDeleteExamCountdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ExamCountdownItem item) return;
            _examCountdowns.RemoveAll(x => x.Id == item.Id);
            RefreshExamCountdownList();
            tbTip.Text = $"✅ 已删除「{item.Name}」（记得保存设置）";
        }

        // ═══════════════════════════════════════════════════════════
        // 保存
        // ═══════════════════════════════════════════════════════════
        private void ReadUiToConfig()
        {
            _currentConfig.ClassName = txtClassName.Text.Trim();

            // ★ 33.0.0：全局 R 角
            _currentConfig.GlobalRadius = (int)sliderGlobalRadius.Value;

            if (cbTheme.SelectedItem is ComboBoxItem themeItem)
            {
                _currentConfig.ThemeName = themeItem.Tag?.ToString() ?? "PCL2";
            }

            _currentConfig.BlurRadius = (int)sliderBlur.Value;

            if (cbBackgroundPreset.SelectedItem is ComboBoxItem presetItem)
            {
                _currentConfig.BackgroundPreset = presetItem.Tag?.ToString() ?? "None";
            }

            if (cbFontFamily.SelectedItem is ComboBoxItem fontItem)
            {
                string fname = fontItem.Content?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(fname))
                    fname = "Microsoft YaHei UI";
                _currentConfig.FontFamily = fname;
            }

            _currentConfig.FontAutoFitEnabled = toggleFontAutoFit.IsChecked == true;
            _currentConfig.BackgroundBlurEnabled = toggleBlurEnabled.IsChecked == true;

            _currentConfig.ExamCountdownList = new List<ExamCountdownItem>(_examCountdowns);
            if (_examCountdowns.Count > 0)
            {
                _currentConfig.ExamName = _examCountdowns[0].Name;
                _currentConfig.ExamDate = _examCountdowns[0].Date;
            }
            else
            {
                _currentConfig.ExamName = "";
                _currentConfig.ExamDate = "";
            }

            _currentConfig.DutyAutoRotateEnabled = toggleDutyAutoRotate.IsChecked == true;

            _currentConfig.ClassChangeStyle =
                (cbClassChangeStyle.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ColorThenNotify";

            _currentConfig.NotifyBarWidthPercent = sliderNotifyWidth.Value;
            _currentConfig.NotifyBarHeightPercent = sliderNotifyHeight.Value;

            _currentConfig.IslandCardSize =
                (cbIslandSize.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Medium";
            _currentConfig.IslandCapsuleTextMode =
                (cbCapsuleTextMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CMW";

            _currentConfig.NotifyClickOutsideToClose = cbNotifyMode.SelectedIndex == 1;

            _currentConfig.IslandFontScale = sliderIslandFontScale.Value;
            _currentConfig.IslandAutoCollapseSeconds = (int)sliderIslandAutoCollapse.Value;

            _currentConfig.SilentStart = (txtSilentStart.Text ?? "").Trim();
            _currentConfig.SilentEnd = (txtSilentEnd.Text ?? "").Trim();

            _currentConfig.IslandBarEnabled = toggleBarEnabled.IsChecked == true;
            _currentConfig.IslandBarAutoExtendOnClassChange = toggleBarAutoExtend.IsChecked == true;
            _currentConfig.IslandBarManualExtendEnabled = toggleBarManualExtend.IsChecked == true;

            // ★ 新增：横条常驻 + 缩回目标
            _currentConfig.IslandBarPersistent = toggleBarPersistent.IsChecked == true;
            _currentConfig.MinimizeTarget =
                (cbMinimizeTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Island";

            _currentConfig.IslandBlueFlashSeconds = (int)sliderBlueFlash.Value;

            double barWidth = sliderBarWidth.Value;
            if (barWidth < 50) barWidth = 50;
            _currentConfig.IslandBarWidth = barWidth;

            _currentConfig.IslandCircleDiameter = sliderCircleDiameter.Value;
            _currentConfig.IslandBarAutoRotateEnabled = toggleBarAutoRotate.IsChecked == true;
            _currentConfig.IslandBarRotateSeconds = (int)sliderRotateSeconds.Value;
            _currentConfig.IslandBarShowDateGroup = toggleGroupDate.IsChecked == true;
            _currentConfig.IslandBarShowScheduleGroup = toggleGroupSchedule.IsChecked == true;
            _currentConfig.IslandBarShowStatusGroup = toggleGroupStatus.IsChecked == true;
            _currentConfig.IslandBarShowHomeworkGroup = toggleGroupHomework.IsChecked == true;
            _currentConfig.IslandBarShowDutyGroup = toggleGroupDuty.IsChecked == true;
            _currentConfig.IslandBarShowTodoGroup = toggleGroupTodo.IsChecked == true;

            _currentConfig.IslandBarGroupOrder = new List<int>(_barGroupOrder);

            _currentConfig.ClassStartRemindEnabled = toggleClassStartRemind.IsChecked == true;
            _currentConfig.ClassEndRemindEnabled = toggleClassEndRemind.IsChecked == true;
            _currentConfig.ClassRemindStyle =
                (cbClassRemindStyle.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Popup";
            _currentConfig.MiniRemindDurationSeconds = (int)sliderMiniRemindDuration.Value;

            _currentConfig.DutyRemindEnabled = toggleDutyRemind.IsChecked == true;
            _currentConfig.DutyRemindTimeList = new List<string>(_dutyRemindTimes);
            _currentConfig.DutyRemindTime = _dutyRemindTimes.Count > 0 ? _dutyRemindTimes[0] : "07:00";

            _currentConfig.TodoCustomRemindEnabled = toggleTodoCustomRemind.IsChecked == true;
            _currentConfig.TodoRemindTimeList = new List<string>(_todoRemindTimes);
            _currentConfig.TodoCustomRemindTime = _todoRemindTimes.Count > 0 ? _todoRemindTimes[0] : "08:00";

            _currentConfig.FloatWindowEnabled = toggleRightEnabled.IsChecked == true;
            _currentConfig.RightPanelAutoHideOnClassStart = toggleRightAutoHide.IsChecked == true;
            _currentConfig.RightPanelAutoShowOnClassEnd = toggleRightAutoShow.IsChecked == true;

            bool ownSlotsNow = toggleRightOwnTimeSlots.IsChecked == true;
            if (ownSlotsNow && !_currentConfig.RightPanelUseOwnTimeSlots)
            {
                if (_currentConfig.RightPanelTimeSlots == null || _currentConfig.RightPanelTimeSlots.Count == 0)
                {
                    _currentConfig.RightPanelTimeSlots = _currentConfig.TimeSlots
                        .Select(t => new TimeSlot
                        {
                            Name = t.Name,
                            StartTime = t.StartTime,
                            EndTime = t.EndTime,
                            EnableNotify = t.EnableNotify,
                            IsLongBreak = t.IsLongBreak
                        }).ToList();
                }
            }
            _currentConfig.RightPanelUseOwnTimeSlots = ownSlotsNow;

            _currentConfig.FloatWindowWidth = sliderRightWidth.Value;
            _currentConfig.RightPanelHeightPercent = sliderRightHeight.Value;

            _currentConfig.RightPanelContentMode =
                (cbRightContentMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
            _currentConfig.RightPanelAutoSwitchSeconds = (int)sliderRightAutoSec.Value;
            _currentConfig.RightPanelFontScale = sliderRightFontScale.Value;

            _currentConfig.FloatWindowContentMode = _currentConfig.RightPanelContentMode;
            _currentConfig.FloatWindowAutoSwitch = _currentConfig.RightPanelContentMode == "Auto";

            _currentConfig.BoardAutoScheduleEnabled = toggleBoardAutoSchedule.IsChecked == true;
            _currentConfig.BoardAutoStartTime = (txtBoardStartTime.Text ?? "").Trim();
            _currentConfig.BoardAutoEndTime = (txtBoardEndTime.Text ?? "").Trim();

            _currentConfig.AutoStart = toggleAutoStart.IsChecked == true;
            _currentConfig.PowerScheduleEnabled = togglePowerSchedule.IsChecked == true;
            _currentConfig.PowerOnTime = (txtPowerOnTime.Text ?? "").Trim();
            _currentConfig.PowerOffTime = (txtPowerOffTime.Text ?? "").Trim();
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            int oldRadius = _currentConfig.BlurRadius;
            string oldTheme = _currentConfig.ThemeName;
            int oldGlobalRadius = _currentConfig.GlobalRadius;

            ReadUiToConfig();
            SaveConfigToFile();

            App.ApplyFontToApp(_currentConfig.FontFamily);

            if (oldTheme != _currentConfig.ThemeName)
            {
                App.ApplyTheme(_currentConfig.ThemeName);
            }

            // ★ 33.0.0：全局 R 角变化时应用
            if (oldGlobalRadius != _currentConfig.GlobalRadius)
            {
                App.ApplyGlobalRadius(_currentConfig.GlobalRadius);
            }

            try
            {
                AutoStartHelper.SyncWithConfig(_currentConfig.AutoStart);
            }
            catch { }

            bool needRegen = _blurNeedsRegen || (oldRadius != _currentConfig.BlurRadius);
            if (needRegen
                && _currentConfig.BackgroundBlurEnabled
                && (_currentConfig.BackgroundPreset ?? "None") == "None")
            {
                tbTip.Text = "⏳ 正在生成模糊背景...";
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                HomePage.GenerateBlurredBackground();

                _blurNeedsRegen = false;
            }

            App.NotifySettingsChanged();

            if (_currentConfig.AutoStart)
                tbTip.Text = "✅ 设置已保存，开机自启动已开启";
            else
                tbTip.Text = "✅ 设置已保存并立即生效";
        }

        // ═══════ 值日 / 待办时间段 ═══════

        private void TxtNewDutyTime_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddDutyTime_Click(sender, e);
        }

        private void BtnAddDutyTime_Click(object sender, RoutedEventArgs e)
        {
            string t = (txtNewDutyTime.Text ?? "").Trim();
            if (!TimeSpan.TryParse(t, out _))
            {
                tbTip.Text = "⚠️ 时间格式错误，应为 HH:mm";
                return;
            }
            if (_dutyRemindTimes.Contains(t))
            {
                tbTip.Text = $"⚠️ {t} 已存在";
                return;
            }

            _dutyRemindTimes.Add(t);
            _dutyRemindTimes = _dutyRemindTimes
                .OrderBy(x => TimeSpan.TryParse(x, out var ts) ? ts : TimeSpan.Zero)
                .ToList();

            txtNewDutyTime.Text = "";
            RefreshDutyTimes();
            tbTip.Text = $"✅ 已添加 {t}（记得保存）";
        }

        private void BtnDeleteDutyTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string t) return;
            _dutyRemindTimes.Remove(t);
            RefreshDutyTimes();
            tbTip.Text = $"✅ 已删除 {t}（记得保存）";
        }

        private void RefreshDutyTimes()
        {
            icDutyTimes.ItemsSource = null;
            icDutyTimes.ItemsSource = _dutyRemindTimes;
        }

        private void TxtNewTodoTime_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddTodoTime_Click(sender, e);
        }

        private void BtnAddTodoTime_Click(object sender, RoutedEventArgs e)
        {
            string t = (txtNewTodoTime.Text ?? "").Trim();
            if (!TimeSpan.TryParse(t, out _))
            {
                tbTip.Text = "⚠️ 时间格式错误，应为 HH:mm";
                return;
            }
            if (_todoRemindTimes.Contains(t))
            {
                tbTip.Text = $"⚠️ {t} 已存在";
                return;
            }

            _todoRemindTimes.Add(t);
            _todoRemindTimes = _todoRemindTimes
                .OrderBy(x => TimeSpan.TryParse(x, out var ts) ? ts : TimeSpan.Zero)
                .ToList();

            txtNewTodoTime.Text = "";
            RefreshTodoTimes();
            tbTip.Text = $"✅ 已添加 {t}（记得保存）";
        }

        private void BtnDeleteTodoTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string t) return;
            _todoRemindTimes.Remove(t);
            RefreshTodoTimes();
            tbTip.Text = $"✅ 已删除 {t}（记得保存）";
        }

        private void RefreshTodoTimes()
        {
            icTodoTimes.ItemsSource = null;
            icTodoTimes.ItemsSource = _todoRemindTimes;
        }

        // ═══════ 其他 ═══════

        private void BtnChooseBackground_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
                Title = "选择主页背景图片"
            };
            if (openDlg.ShowDialog() != true) return;

            try
            {
                string srcPath = openDlg.FileName;
                if (!Directory.Exists(AppPaths.CustomBackgroundDir))
                    Directory.CreateDirectory(AppPaths.CustomBackgroundDir);

                File.Copy(srcPath, AppPaths.CustomBackgroundFile, true);
                _currentConfig.BackgroundImagePath = AppPaths.CustomBackgroundFile;

                _blurNeedsRegen = true;

                UpdateBackgroundPathDisplay();
                tbTip.Text = "✅ 背景已选择，点「保存设置」生效";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择背景失败：{ex.Message}", "错误");
            }
        }

        private void BtnResetBackground_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(AppPaths.CustomBackgroundFile))
                    File.Delete(AppPaths.CustomBackgroundFile);

                try
                {
                    if (File.Exists(AppPaths.CustomBackgroundBlurredFile))
                        File.Delete(AppPaths.CustomBackgroundBlurredFile);
                }
                catch { }
            }
            catch { }

            _currentConfig.BackgroundImagePath = "";
            _blurNeedsRegen = true;

            UpdateBackgroundPathDisplay();
            tbTip.Text = "✅ 已恢复默认背景，点「保存设置」生效";
        }

        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new SaveFileDialog
            {
                Filter = "CMW数据包(*.cmwdata)|*.cmwdata",
                FileName = "cmw_backup.cmwdata"
            };
            if (saveDlg.ShowDialog() == true)
            {
                ReadUiToConfig();
                var pack = new AllDataPackage
                {
                    Config = _currentConfig,
                    ScheduleRaw = ReadFileText(AppPaths.Schedule),
                    DutyRaw = ReadFileText(AppPaths.Duty),
                    HomeworkRaw = ReadFileText(AppPaths.Homework),
                    TodoRaw = ReadFileText(AppPaths.Todo)
                };
                JsonHelper.Save(saveDlg.FileName, pack);
                tbTip.Text = "✅ 数据导出成功";
            }
        }

        private void BtnImportAll_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new OpenFileDialog
            {
                Filter = "CMW数据包(*.cmwdata)|*.cmwdata"
            };
            if (openDlg.ShowDialog() == true)
            {
                var pack = JsonHelper.Load<AllDataPackage>(openDlg.FileName);
                if (pack == null)
                {
                    tbTip.Text = "❌ 数据包解析失败";
                    return;
                }

                DataMigrationService.MigrateAll(pack);

                JsonHelper.Save(AppPaths.Config, pack.Config);
                WriteFileText(AppPaths.Schedule, pack.ScheduleRaw);
                WriteFileText(AppPaths.Duty, pack.DutyRaw);
                WriteFileText(AppPaths.Homework, pack.HomeworkRaw);
                WriteFileText(AppPaths.Todo, pack.TodoRaw);
                JsonHelper.ClearCache();

                _isLoading = true;
                try
                {
                    LoadConfig();
                    BindUiFromConfig();
                }
                finally
                {
                    _isLoading = false;
                }

                try
                {
                    AutoStartHelper.SyncWithConfig(_currentConfig.AutoStart);
                }
                catch { }

                if (_currentConfig.BackgroundBlurEnabled
                    && (_currentConfig.BackgroundPreset ?? "None") == "None")
                {
                    HomePage.GenerateBlurredBackground();
                }

                App.ApplyFontToApp(_currentConfig.FontFamily);
                App.ApplyTheme(_currentConfig.ThemeName);
                App.ApplyGlobalRadius(_currentConfig.GlobalRadius);
                App.NotifySettingsChanged();

                tbTip.Text = "✅ 导入完成并立即生效";
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "选择导出 CSV 的文件夹";

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string err = ExportService.ExportAll(dlg.SelectedPath);

            if (string.IsNullOrEmpty(err))
            {
                tbTip.Text = "✅ CSV 已导出到：" + dlg.SelectedPath;
                MessageBox.Show(
                    "✅ 已导出 4 个 CSV 文件：\n\n" +
                    "· 作业-时间戳.csv\n" +
                    "· 待办-时间戳.csv\n" +
                    "· 课表-时间戳.csv\n" +
                    "· 值日-时间戳.csv\n\n" +
                    "用 Excel 或 WPS 打开即可（UTF-8 BOM 编码，中文不乱码）",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                tbTip.Text = "⚠️ 部分文件导出失败";
                MessageBox.Show("导出时出现错误：\n" + err,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnOpenRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            var win = new RecycleBinWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        private void BtnViewNotes_Click(object sender, RoutedEventArgs e)
        {
            var win = new NotesViewerWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        private void BtnClearNotes_Click(object sender, RoutedEventArgs e)
        {
            var notes = NoteService.GetAll();
            if (notes.Count == 0)
            {
                tbTip.Text = "笔记已经是空的";
                return;
            }

            if (MessageBox.Show(
                $"确定清空全部 {notes.Count} 条笔记？\n\n图片也会一并删除，不可恢复。",
                "警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            NoteService.ClearAll();
            tbTip.Text = "✅ 所有笔记已清空";
        }

        private void BtnSaveClearPwd_Click(object sender, RoutedEventArgs e)
        {
            string cur = pwdCurrent.Password ?? "";
            string nw = pwdNew.Password ?? "";
            string cf = pwdConfirm.Password ?? "";

            if (string.IsNullOrEmpty(cur)) { tbPwdTip.Text = "⚠️ 请输入当前密码"; pwdCurrent.Focus(); return; }
            if (string.IsNullOrEmpty(nw)) { tbPwdTip.Text = "⚠️ 请输入新密码"; pwdNew.Focus(); return; }
            if (string.IsNullOrEmpty(cf)) { tbPwdTip.Text = "⚠️ 请再次输入新密码"; pwdConfirm.Focus(); return; }

            string expected = string.IsNullOrEmpty(_currentConfig.ClearDataPassword)
                ? "12345678"
                : _currentConfig.ClearDataPassword;

            if (cur != expected)
            {
                tbPwdTip.Text = "❌ 当前密码错误";
                pwdCurrent.Focus();
                pwdCurrent.SelectAll();
                return;
            }

            if (nw.Length < 4) { tbPwdTip.Text = "⚠️ 新密码至少 4 位"; pwdNew.Focus(); return; }
            if (nw != cf) { tbPwdTip.Text = "❌ 两次输入的新密码不一致"; pwdConfirm.Focus(); pwdConfirm.SelectAll(); return; }
            if (nw == cur) { tbPwdTip.Text = "⚠️ 新密码不能与当前密码相同"; pwdNew.Focus(); return; }

            try
            {
                var cfg = JsonHelper.Load<AppConfig>(AppPaths.Config) ?? new AppConfig();
                cfg.ClearDataPassword = nw;
                JsonHelper.Save(AppPaths.Config, cfg);
                _currentConfig.ClearDataPassword = nw;

                pwdCurrent.Password = "";
                pwdNew.Password = "";
                pwdConfirm.Password = "";

                tbPwdTip.Text = "✅ 密码已更新，立即生效";
            }
            catch (Exception ex)
            {
                tbPwdTip.Text = "❌ 保存失败：" + ex.Message;
            }
        }

        private void BtnClearAllData_Click(object sender, RoutedEventArgs e)
        {
            string listContent =
                "· 课表（schedule.json）\n" +
                "· 值日组（duty.json）\n" +
                "· 作业（homework.json）\n" +
                "· 待办（todo.json）\n" +
                "· 作业催收记录\n" +
                "· 回收站\n" +
                "· 班级名单（students.json）\n" +
                "· 考试记录（exams.json）\n" +
                "· 座位表（seats.json）\n" +
                "· 班级日历（events.json）\n" +
                "· 作业模板\n" +
                "· 投票记录（polls.json）\n" +
                "· 全部笔记（含图片）\n" +
                "· 全部周报";

            var r1 = MessageBox.Show(
                "⚠️ 即将清空以下全部内容：\n\n" + listContent +
                "\n\n（软件设置会保留）\n\n是否继续？",
                "第 1 步 / 共 3 步 · 确认范围",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (r1 != MessageBoxResult.Yes) return;

            string expected = string.IsNullOrEmpty(_currentConfig.ClearDataPassword)
                ? "12345678" : _currentConfig.ClearDataPassword;

            string? input = PromptPassword("请输入清空密码（默认 12345678）：",
                "第 2 步 / 共 3 步 · 密码验证");

            if (input == null) { tbTip.Text = "已取消"; return; }
            if (input != expected)
            {
                MessageBox.Show("❌ 密码错误，操作已取消。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                tbTip.Text = "❌ 密码错误";
                return;
            }

            var r3 = MessageBox.Show(
                "最后确认：\n\n此操作不可恢复，确定要清空所有数据吗？",
                "第 3 步 / 共 3 步 · 最终确认",
                MessageBoxButton.YesNo, MessageBoxImage.Stop);

            if (r3 != MessageBoxResult.Yes) { tbTip.Text = "已取消"; return; }

            ExecuteClearAllData();
            tbTip.Text = "✅ 所有业务数据已清空（软件设置保留）";

            MessageBox.Show("✅ 所有业务数据已清空。\n\n（软件设置和密码已保留）",
                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteClearAllData()
        {
            TryDelete(AppPaths.Schedule);
            TryDelete(AppPaths.Duty);
            TryDelete(AppPaths.Homework);
            TryDelete(AppPaths.Todo);
            TryDelete(AppPaths.HomeworkReminders);
            TryDelete(AppPaths.RecycleBin);
            TryDelete(AppPaths.Students);
            TryDelete(AppPaths.Exams);
            TryDelete(AppPaths.Seats);
            TryDelete(AppPaths.Events);
            TryDelete(AppPaths.HomeworkTemplates);
            TryDelete(AppPaths.Polls);

            TryDeleteDirectory(AppPaths.NotesDir);
            TryDeleteDirectory(AppPaths.ReportsDir);

            JsonHelper.ClearCache();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private void BtnEditRightTimeSlots_Click(object sender, RoutedEventArgs e)
        {
            var win = new TimeSlotEditorWindow(true)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        private void BtnEditSubjectMap_Click(object sender, RoutedEventArgs e)
        {
            var win = new SubjectMapWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        private void BtnOpenBiosGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new BiosWakeGuideWindow
                {
                    Owner = Window.GetWindow(this)
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开引导失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnGeneratePowerScripts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(AppPaths.ScriptsDir))
                    Directory.CreateDirectory(AppPaths.ScriptsDir);

                string onTime = (txtPowerOnTime.Text ?? "07:00").Trim();
                string offTime = (txtPowerOffTime.Text ?? "22:00").Trim();

                string onBat =
                    "@echo off\r\n" +
                    "REM CMW 自动开机脚本（由 CMW 生成）\r\n" +
                    "REM 建议开机时间：" + onTime + "\r\n" +
                    "REM 用法：导入任务计划程序，触发时间设为 " + onTime + "，操作选本脚本\r\n" +
                    "echo CMW 开机脚本（触发时间 " + onTime + "）\r\n" +
                    "pause\r\n";

                string offBat =
                    "@echo off\r\n" +
                    "REM CMW 自动关机脚本（由 CMW 生成）\r\n" +
                    "REM 建议关机时间：" + offTime + "\r\n" +
                    "shutdown /s /t 60 /c \"CMW 定时关机（60 秒后）\"\r\n" +
                    "echo 已计划 60 秒后关机\r\n";

                File.WriteAllText(AppPaths.PowerOnScript, onBat, new System.Text.UTF8Encoding(false));
                File.WriteAllText(AppPaths.PowerOffScript, offBat, new System.Text.UTF8Encoding(false));

                MessageBox.Show(
                    "✅ 脚本已生成：\n\n" +
                    AppPaths.PowerOnScript + "\n" +
                    AppPaths.PowerOffScript + "\n\n" +
                    "使用方式：\n" +
                    "1. 打开「任务计划程序」\n" +
                    "2. 创建基本任务 → 触发时间设为 " + onTime + " / " + offTime + "\n" +
                    "3. 操作选「启动程序」→ 选对应的 .bat\n\n" +
                    "注：Windows 默认不支持定时开机，开机脚本仅作为占位。",
                    "脚本已生成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成脚本失败：" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnOpenScriptsDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(AppPaths.ScriptsDir))
                    Directory.CreateDirectory(AppPaths.ScriptsDir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.ScriptsDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败：" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string? PromptPassword(string prompt, string title)
        {
            var owner = Window.GetWindow(this);

            var win = new Window
            {
                Title = title,
                Width = 460,
                Height = 200,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
                Owner = owner,
                Topmost = true,
                ShowActivated = true,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EBF0"))
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"))
            };
            titleBar.MouseLeftButtonDown += (s, ee) =>
            {
                if (ee.ButtonState == MouseButtonState.Pressed) win.DragMove();
            };
            titleBar.Child = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var content = new Grid { Margin = new Thickness(16) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = prompt,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040")),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lbl, 0);
            content.Children.Add(lbl);

            var pwd = new PasswordBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 12),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8C8D8")),
                BorderThickness = new Thickness(1),
                Background = Brushes.White
            };
            Grid.SetRow(pwd, 1);
            content.Children.Add(pwd);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Style = Application.Current?.TryFindResource("PclButton") as Style
            };
            var btnOk = new Button
            {
                Content = "验证",
                Width = 80,
                Height = 32,
                Style = Application.Current?.TryFindResource("PclButtonPrimary") as Style
            };

            string? result = null;
            btnCancel.Click += (s, ee) => { win.DialogResult = false; win.Close(); };
            btnOk.Click += (s, ee) =>
            {
                result = pwd.Password;
                win.DialogResult = true;
                win.Close();
            };

            pwd.KeyDown += (s, ee) =>
            {
                if (ee.Key == Key.Enter)
                {
                    result = pwd.Password;
                    win.DialogResult = true;
                    win.Close();
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 2);
            content.Children.Add(btnPanel);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            win.Content = root;
            win.Loaded += (s, ee) =>
            {
                pwd.Focus();
                win.Activate();
            };

            return win.ShowDialog() == true ? result : null;
        }

        private static string ReadFileText(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        private static void WriteFileText(string path, string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
                File.WriteAllText(path, content);
        }
    }
}