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
        private AppConfig _currentConfig;
        private bool _isLoading;
        private StackPanel? _currentPanel;

        private bool _blurNeedsRegen = false;

        public SettingsPage()
        {
            InitializeComponent();
            _currentConfig = new AppConfig();
            Loaded += SettingsPage_Loaded;

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

            // ★ 28.0.0：通知条宽度 / 高度
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

            sliderLeftScheduleWidth.ValueChanged += (s, e) =>
            {
                tbLeftScheduleWidth.Text = $"当前：{sliderLeftScheduleWidth.Value:F0} px";
            };
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                LoadConfig();
                LoadFontList();
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

        private void BindUiFromConfig()
        {
            txtClassName.Text = _currentConfig.ClassName;
            sliderBlur.Value = _currentConfig.BlurRadius;
            tbBlurValue.Text = $"当前值：{_currentConfig.BlurRadius:F0}";

            toggleBlurEnabled.IsChecked = _currentConfig.BackgroundBlurEnabled;

            foreach (ComboBoxItem item in cbFontFamily.Items)
            {
                if (item.Content?.ToString() == _currentConfig.FontFamily)
                {
                    cbFontFamily.SelectedItem = item;
                    break;
                }
            }
            if (cbFontFamily.SelectedItem == null && cbFontFamily.Items.Count > 0)
                cbFontFamily.SelectedIndex = 0;

            UpdateBackgroundPathDisplay();

            txtExamName.Text = _currentConfig.ExamName ?? "";
            if (!string.IsNullOrEmpty(_currentConfig.ExamDate)
                && DateTime.TryParse(_currentConfig.ExamDate, out var dt))
            {
                dpExamDate.SelectedDate = dt;
            }
            else
            {
                dpExamDate.SelectedDate = null;
            }

            toggleDutyAutoRotate.IsChecked = _currentConfig.DutyAutoRotateEnabled;

            toggleIslandDraggable.IsChecked = _currentConfig.IslandDraggable;

            // ★ 28.0.0：通知条宽度 / 高度
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

            // ★ 28.0.0：上下课提醒
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

            // ★ 28.0.0：值日 / 待办提醒
            toggleDutyRemind.IsChecked = _currentConfig.DutyRemindEnabled;
            txtDutyRemindTime.Text = _currentConfig.DutyRemindTime ?? "07:00";
            toggleTodoCustomRemind.IsChecked = _currentConfig.TodoCustomRemindEnabled;
            txtTodoCustomRemindTime.Text = _currentConfig.TodoCustomRemindTime ?? "08:00";

            // 右侧窗口
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
                case "Schedule": cbRightContentMode.SelectedIndex = 0; break;
                case "Homework": cbRightContentMode.SelectedIndex = 1; break;
                case "Duty": cbRightContentMode.SelectedIndex = 2; break;
                case "Todo": cbRightContentMode.SelectedIndex = 3; break;
                case "Auto":
                default: cbRightContentMode.SelectedIndex = 4; break;
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

            // ★ 28.0.0：教学辅助
            toggleBoardAutoSchedule.IsChecked = _currentConfig.BoardAutoScheduleEnabled;
            txtBoardStartTime.Text = _currentConfig.BoardAutoStartTime ?? "07:30";
            txtBoardEndTime.Text = _currentConfig.BoardAutoEndTime ?? "18:00";

            toggleLeftSchedule.IsChecked = _currentConfig.LeftScheduleEnabled;

            double lsw = _currentConfig.LeftScheduleWidth;
            if (lsw < 60) lsw = 90;
            if (lsw > 200) lsw = 200;
            sliderLeftScheduleWidth.Value = lsw;
            tbLeftScheduleWidth.Text = $"当前：{lsw:F0} px";

            // ★ 28.0.0：启动设置
            toggleAutoStart.IsChecked = _currentConfig.AutoStart;

            togglePowerSchedule.IsChecked = _currentConfig.PowerScheduleEnabled;
            txtPowerOnTime.Text = _currentConfig.PowerOnTime ?? "07:00";
            txtPowerOffTime.Text = _currentConfig.PowerOffTime ?? "22:00";

            // 密码框清空
            pwdCurrent.Password = "";
            pwdNew.Password = "";
            pwdConfirm.Password = "";
            tbPwdTip.Text = "";
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

        private void ReadUiToConfig()
        {
            _currentConfig.ClassName = txtClassName.Text.Trim();
            _currentConfig.BlurRadius = (int)sliderBlur.Value;
            if (cbFontFamily.SelectedItem is ComboBoxItem fontItem)
            {
                _currentConfig.FontFamily = fontItem.Content?.ToString() ?? "Microsoft YaHei UI";
            }

            _currentConfig.BackgroundBlurEnabled = toggleBlurEnabled.IsChecked == true;

            _currentConfig.ExamName = (txtExamName.Text ?? "").Trim();
            _currentConfig.ExamDate = dpExamDate.SelectedDate.HasValue
                ? dpExamDate.SelectedDate.Value.ToString("yyyy-MM-dd")
                : "";

            _currentConfig.DutyAutoRotateEnabled = toggleDutyAutoRotate.IsChecked == true;

            _currentConfig.IslandDraggable = toggleIslandDraggable.IsChecked == true;

            // ★ 28.0.0：通知条宽度 / 高度
            _currentConfig.NotifyBarWidthPercent = sliderNotifyWidth.Value;
            _currentConfig.NotifyBarHeightPercent = sliderNotifyHeight.Value;

            string sizeTag = (cbIslandSize.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Medium";
            _currentConfig.IslandCardSize = sizeTag;

            _currentConfig.IslandCapsuleTextMode =
                (cbCapsuleTextMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CMW";

            _currentConfig.NotifyClickOutsideToClose = cbNotifyMode.SelectedIndex == 1;

            _currentConfig.IslandFontScale = sliderIslandFontScale.Value;
            _currentConfig.IslandAutoCollapseSeconds = (int)sliderIslandAutoCollapse.Value;

            _currentConfig.SilentStart = (txtSilentStart.Text ?? "").Trim();
            _currentConfig.SilentEnd = (txtSilentEnd.Text ?? "").Trim();

            // ★ 上下课提醒
            _currentConfig.ClassStartRemindEnabled = toggleClassStartRemind.IsChecked == true;
            _currentConfig.ClassEndRemindEnabled = toggleClassEndRemind.IsChecked == true;
            _currentConfig.ClassRemindStyle =
                (cbClassRemindStyle.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Popup";
            _currentConfig.MiniRemindDurationSeconds = (int)sliderMiniRemindDuration.Value;

            // ★ 值日 / 待办提醒
            _currentConfig.DutyRemindEnabled = toggleDutyRemind.IsChecked == true;
            _currentConfig.DutyRemindTime = (txtDutyRemindTime.Text ?? "").Trim();
            _currentConfig.TodoCustomRemindEnabled = toggleTodoCustomRemind.IsChecked == true;
            _currentConfig.TodoCustomRemindTime = (txtTodoCustomRemindTime.Text ?? "").Trim();

            // 右侧窗口
            _currentConfig.FloatWindowEnabled = toggleRightEnabled.IsChecked == true;
            _currentConfig.RightPanelAutoHideOnClassStart = toggleRightAutoHide.IsChecked == true;
            _currentConfig.RightPanelAutoShowOnClassEnd = toggleRightAutoShow.IsChecked == true;

            bool ownSlotsNow = toggleRightOwnTimeSlots.IsChecked == true;
            if (ownSlotsNow && !_currentConfig.RightPanelUseOwnTimeSlots)
            {
                // 刚开启 → 复制当前时段作为初始
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

            // ★ 教学辅助
            _currentConfig.BoardAutoScheduleEnabled = toggleBoardAutoSchedule.IsChecked == true;
            _currentConfig.BoardAutoStartTime = (txtBoardStartTime.Text ?? "").Trim();
            _currentConfig.BoardAutoEndTime = (txtBoardEndTime.Text ?? "").Trim();

            _currentConfig.LeftScheduleEnabled = toggleLeftSchedule.IsChecked == true;
            _currentConfig.LeftScheduleWidth = sliderLeftScheduleWidth.Value;

            // ★ 启动设置
            _currentConfig.AutoStart = toggleAutoStart.IsChecked == true;

            _currentConfig.PowerScheduleEnabled = togglePowerSchedule.IsChecked == true;
            _currentConfig.PowerOnTime = (txtPowerOnTime.Text ?? "").Trim();
            _currentConfig.PowerOffTime = (txtPowerOffTime.Text ?? "").Trim();
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            int oldRadius = _currentConfig.BlurRadius;

            ReadUiToConfig();
            SaveConfigToFile();

            try
            {
                AutoStartHelper.SyncWithConfig(_currentConfig.AutoStart);
            }
            catch { }

            bool needRegen = _blurNeedsRegen || (oldRadius != _currentConfig.BlurRadius);
            if (needRegen && _currentConfig.BackgroundBlurEnabled)
            {
                tbTip.Text = "⏳ 正在生成模糊背景...";
                Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

                HomePage.GenerateBlurredBackground();

                _blurNeedsRegen = false;
            }

            // ★ 28.0.0：启动 / 关闭左侧课表
            try
            {
                if (_currentConfig.LeftScheduleEnabled)
                    App.EnsureLeftSchedule();
                else
                    App.CloseLeftSchedule();
            }
            catch { }

            App.NotifySettingsChanged();

            if (_currentConfig.AutoStart)
                tbTip.Text = "✅ 设置已保存，开机自启动已开启";
            else
                tbTip.Text = "✅ 设置已保存并立即生效";
        }

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

                if (_currentConfig.BackgroundBlurEnabled)
                {
                    HomePage.GenerateBlurredBackground();
                }

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

        // ═══════════════════════════════════════
        // 修改密码
        // ═══════════════════════════════════════

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

        // ═══════════════════════════════════════
        // 清空所有数据
        // ═══════════════════════════════════════

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

        // ═══════════════════════════════════════
        // ★ 28.0.0：编辑右侧独立时段表
        // ═══════════════════════════════════════

        private void BtnEditRightTimeSlots_Click(object sender, RoutedEventArgs e)
        {
            var win = new TimeSlotEditorWindow(true)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        // ═══════════════════════════════════════
        // ★ 28.0.0：编辑课程简称
        // ═══════════════════════════════════════

        private void BtnEditSubjectMap_Click(object sender, RoutedEventArgs e)
        {
            var win = new SubjectMapWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        // ═══════════════════════════════════════
        // ★ 28.0.0：生成开机 / 关机脚本
        // ═══════════════════════════════════════

        private void BtnGeneratePowerScripts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(AppPaths.ScriptsDir))
                    Directory.CreateDirectory(AppPaths.ScriptsDir);

                string onTime = (txtPowerOnTime.Text ?? "07:00").Trim();
                string offTime = (txtPowerOffTime.Text ?? "22:00").Trim();

                // 开机脚本（生成任务计划程序可用的 .bat）
                string onBat =
                    "@echo off\r\n" +
                    "REM CMW 自动开机脚本（由 CMW 生成）\r\n" +
                    "REM 建议开机时间：" + onTime + "\r\n" +
                    "REM 用法：导入任务计划程序，触发时间设为 " + onTime + "，操作选本脚本\r\n" +
                    "REM 如果支持 Wake-on-LAN，可在此处插入网络唤醒命令\r\n" +
                    "REM 示例：wolcmd <MAC> <IP> <SUBNET> <PORT>\r\n" +
                    "echo CMW 开机脚本（触发时间 " + onTime + "）\r\n" +
                    "pause\r\n";

                string offBat =
                    "@echo off\r\n" +
                    "REM CMW 自动关机脚本（由 CMW 生成）\r\n" +
                    "REM 建议关机时间：" + offTime + "\r\n" +
                    "REM 用法：导入任务计划程序，触发时间设为 " + offTime + "，操作选本脚本\r\n" +
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
                    "注：Windows 默认不支持定时开机，开机脚本仅作为占位，实际开机需要主板 BIOS 支持 RTC Wake 或 Wake-on-LAN。",
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

        // ═══════════════════════════════════════
        // 密码输入弹窗
        // ═══════════════════════════════════════

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