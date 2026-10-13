﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CMW
{
    public partial class TodoPage : UserControl
    {
        private List<TodoItem> _todoList;
        private StackPanel? _currentPanel;

        public TodoPage()
        {
            InitializeComponent();
            _todoList = new List<TodoItem>();
            Loaded += TodoPage_Loaded;
        }

        private void TodoPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTodoFromJson();
            RefreshTodoList();
            RefreshTodayTodo();
            _currentPanel = PanelAdd;
        }

        // ═══════════════════════════════════════
        // 二级选项卡切换
        // ═══════════════════════════════════════

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            StackPanel? target = rb.Name switch
            {
                "TabAdd" => PanelAdd,
                "TabAll" => PanelAll,
                "TabToday" => PanelToday,
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

        // ═══════════════════════════════════════
        // 新增待办
        // ═══════════════════════════════════════

        // ★ 26.6-4：回车快速添加
        private void TxtTodoContent_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddTodo_Click(sender, e);
        }

        private void BtnAddTodo_Click(object sender, RoutedEventArgs e)
        {
            string content = txtTodoContent.Text.Trim();
            string remark = txtRemark.Text.Trim();
            DateTime? deadline = dpTodoDeadline.SelectedDate;

            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("请填写待办内容");
                return;
            }

            _todoList.Add(new TodoItem
            {
                Id = Guid.NewGuid().ToString(),
                Content = content,
                Remark = remark,
                Deadline = deadline,
                IsCompleted = false
            });

            txtTodoContent.Text = string.Empty;
            txtRemark.Text = string.Empty;
            dpTodoDeadline.SelectedDate = null;

            SaveTodoToJson();
            RefreshTodoList();
            RefreshTodayTodo();

            TabAll.IsChecked = true;
        }

        // ═══════════════════════════════════════
        // 删除 / 勾选
        // ═══════════════════════════════════════

        private void DeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TodoItem item)
            {
                RecycleBinService.Add(
                    RecycleBinService.TypeTodo,
                    item.Content,
                    item);

                _todoList.Remove(item);
                SaveTodoToJson();
                RefreshTodoList();
                RefreshTodayTodo();
            }
        }

        private void Todo_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is TodoItem item)
            {
                item.IsCompleted = true;
                SaveTodoToJson();
                RefreshTodayTodo();
            }
        }

        private void Todo_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is TodoItem item)
            {
                item.IsCompleted = false;
                SaveTodoToJson();
                RefreshTodayTodo();
            }
        }

        private void BtnClearCompletedTodo_Click(object sender, RoutedEventArgs e)
        {
            int removeCount = _todoList.RemoveAll(x => x.IsCompleted);
            if (removeCount > 0)
            {
                SaveTodoToJson();
                RefreshTodoList();
                RefreshTodayTodo();
            }
            else MessageBox.Show("没有已完成待办");
        }

        // ═══════════════════════════════════════
        // 刷新列表
        // ═══════════════════════════════════════

        private void RefreshTodoList()
        {
            icTodoList.ItemsSource = null;
            icTodoList.ItemsSource = _todoList;

            tbAllEmpty.Visibility = _todoList.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshTodayTodo()
        {
            var todayList = GetTodayTodo();

            icTodayTodoList.ItemsSource = null;
            icTodayTodoList.ItemsSource = todayList;

            tbTodayEmpty.Visibility = todayList.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ═══════════════════════════════════════
        // 数据接口
        // ═══════════════════════════════════════

        public List<TodoItem> GetTodayTodo()
        {
            DateTime today = DateTime.Now.Date;
            return _todoList
                .Where(x => x.Deadline.HasValue
                    && x.Deadline.Value.Date == today
                    && !x.IsCompleted)
                .ToList();
        }

        private void LoadTodoFromJson()
        {
            _todoList = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                        ?? new List<TodoItem>();
        }

        private void SaveTodoToJson()
        {
            try { JsonHelper.Save(AppPaths.Todo, _todoList); }
            catch { MessageBox.Show("待办保存失败。"); }
        }

        public static List<TodoSummaryItem> GetTodayTodoForSummary()
        {
            var allData = JsonHelper.Load<List<TodoItem>>(AppPaths.Todo)
                          ?? new List<TodoItem>();
            DateTime today = DateTime.Now.Date;
            return allData
                .Where(x => x.Deadline.HasValue
                         && x.Deadline.Value.Date == today
                         && !x.IsCompleted)
                .Select(x => new TodoSummaryItem
                {
                    TodoContent = x.Content,
                    Remark = x.Remark,
                    DeadlineText = x.DeadlineText,
                    IsCompleted = x.IsCompleted
                })
                .ToList();
        }
    }
}