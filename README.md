# CMW 班级管理工具

武汉外国语学校高中部班级日常管理工具
当前版本：**32.0.0**

---

## 项目简介

CMW（Class Management Widget）是一款基于
WPF + .NET 6 开发的 Windows 桌面班级管理软件，
覆盖高中班级日常管理的各类场景：课表、作业、
待办、值日、名单、考试、座位表、日历、统计、
电子班牌等。

界面采用 PCL2 启动器风格：深蓝顶栏 + 浅灰背景 +
白色卡片，扁平化，圆角处理。支持运行时切换
ClassIsland 深色主题。

---

## 主要功能

### 十三大功能页面

  首页      数据概览卡（今日作业/待办/值日/下节课）
            + 考试倒计时（可添加多条）
  课表      每周固定课表 + 单节临时换课 + 天级换课
            + CSV 导入
  待办      新增 / 全部 / 今日 三视图，回车快速添加
  值日      值日组管理 + 每日自动轮换
  作业      布置 / 追踪 / 催收 / 点名 / 从模板添加
            / 编辑
  汇总      今日课表 + 值日 + 作业 + 待办 一屏总览
  名单      名单管理 + 随机点名 + 班级投票
  考试      成绩录入 / 列表 / 趋势图
  周报      每周自动生成 Markdown 报告
  统计      作业趋势 / 待办趋势 / 值日统计图表
  座位表    可视化排座，多方案切换
  日历      月视图 + 活动管理
  设置      8 个分类标签页，全部可配置

### 桌面常驻组件

  · 灵动岛 — 屏幕顶部黑色胶囊，单击展开卡片
    （作业 / 待办 / 课表 / 笔记 / 计时），
    右键或上下课可延伸为横条
  · 横条 — 从胶囊延伸出来，6 组内容自动轮播
    （日期 / 课表 / 状态 / 作业 / 值日 / 待办），
    顺序可自定义，超宽自动往返滚动
  · 右侧常驻窗口 — 贴在屏幕右侧，显示作业 /
    值日 / 待办
  · 电子班牌 — 全屏深色展示当天信息，
    适合班级信息屏
  · 课堂倒计时 — 课堂限时活动工具
  · 课上截图笔记 — 截图 + 文字备注保存

### 主题与外观

  · 双主题：PCL2（浅色）/ ClassIsland（深色）
    运行时切换，图标颜色随主题
  · 主页背景：图片 / 4 种渐变预设（深蓝斜向、
    晨雾浅蓝、暗夜紫灰、墨绿山水）
  · 全局字体切换 + 首页数据卡字体自动缩放
  · 全局 R 角体系（Small / Medium / Large / XLarge）

### 通知与提醒

  · 上下课提醒（可分开设置 / 4 种样式）
  · 上下课表现方式（变色 → 通知 / 仅通知 /
    仅变色 + 横条 / 都不做）
  · 待办提醒（多个时间点）
  · 值日提醒（多个时间点）
  · 作业催收（滚动显示未交名单）
  · 每日汇总（08:00）

### 自动开关机

  · 关机：生成 power_off.bat，可导入
    Windows 任务计划程序定时关机
  · 开机：软件无法直接开机（Windows 关机后
    不运行任何程序），提供 BIOS RTC 定时唤醒
    设置引导，含主流品牌进入 BIOS 按键对照

---

## 技术栈

  · 框架：.NET 6.0（net6.0-windows）
  · 语言：C# 10.0
  · UI：WPF
  · JSON：Newtonsoft.Json 13.0.3
  · SDK：6.0.428（global.json 严格锁定）

---

## 环境要求

支持的系统
  · Windows 10 1607+
  · Windows 11
  · Windows Server 2016+

不支持的系统
  · Windows 7 / 8 / 8.1
  · 极精简版 Windows

开发环境
  · .NET SDK 6.0.428（global.json 已锁定）
  · 命令行 dotnet（不需要 Visual Studio）

---

## 编译与运行

  # 克隆仓库
  git clone https://github.com/yangziyaoalpaca-cpu/CMW-ClassManager.git
  cd CMW-ClassManager

  # 编译
  dotnet build

  # 运行
  dotnet run

### 发布

  # 自包含单文件版本（约 150MB）
  dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

  # 框架依赖版本（约 5MB，需要目标机安装
  # .NET 6 运行时）
  dotnet publish -c Release -r win-x64 --self-contained false

---

## 项目结构

  CMW/
  ├── App.xaml / App.xaml.cs              应用入口、全局样式、事件
  ├── Theme.PCL2.xaml                     浅色主题资源
  ├── Theme.ClassIsland.xaml              深色主题资源
  ├── MainWindow.xaml / .cs               主窗口
  ├── IslandWindow.xaml / .cs             灵动岛（含横条）
  ├── FloatWindow.xaml / .cs              右侧常驻窗口
  ├── ReleasableWindow.cs                 辅助窗口基类（统一释放）
  ├── HomePage.xaml / .cs                 首页
  ├── SchedulePage.xaml / .cs             课表页
  ├── TodoPage.xaml / .cs                 待办页
  ├── DutyPage.xaml / .cs                 值日页
  ├── HomeworkPage.xaml / .cs             作业页
  ├── SummaryPage.xaml / .cs              汇总页
  ├── StudentsPage.xaml / .cs             名单页
  ├── ExamPage.xaml / .cs                 考试页
  ├── LogsPage.xaml / .cs                 周报页
  ├── StatisticsPage.xaml / .cs           统计页
  ├── SeatPage.xaml / .cs                 座位表页
  ├── CalendarPage.xaml / .cs             日历页
  ├── SettingsPage.xaml / .cs             设置页
  │
  ├── RollCallWindow.xaml / .cs           点名窗口
  ├── RecycleBinWindow.xaml / .cs         回收站
  ├── SeatPickerWindow.xaml / .cs         座位选人
  ├── DayEventWindow.xaml / .cs           日历日弹窗
  ├── HomeworkTemplateWindow.xaml / .cs   作业模板
  ├── ScreenshotOverlayWindow.xaml / .cs  截图遮罩
  ├── NoteAddWindow.xaml / .cs            新增笔记
  ├── NotesViewerWindow.xaml / .cs        笔记查看
  ├── ClassBoardWindow.xaml / .cs         电子班牌
  ├── ClassTimerWindow.xaml / .cs         课堂倒计时
  ├── TimeSlotEditorWindow.xaml / .cs     时段编辑
  ├── SubjectMapWindow.xaml / .cs         课程简称编辑
  ├── BiosWakeGuideWindow.xaml / .cs      BIOS 定时唤醒引导
  ├── LessonCell.xaml / .cs               课程单元格控件
  │
  ├── Models/
  │   └── Models.cs                       所有数据类 + 工具类
  ├── Services/
  │   ├── DataMigrationService.cs         数据迁移
  │   ├── CrashLogger.cs                  崩溃日志
  │   ├── BackupService.cs                每日自动备份
  │   ├── RecycleBinService.cs            回收站
  │   ├── CsvImportService.cs             CSV 导入
  │   ├── WeeklyReportService.cs          周报
  │   ├── ExportService.cs                CSV 导出
  │   ├── ScreenshotService.cs            截图
  │   ├── NoteService.cs                  笔记读写
  │   └── DutyRotateService.cs            值日轮换
  ├── Assets/
  │   ├── app.ico                         图标
  │   ├── Background.jpg                  默认背景
  │   └── logo.png                        校徽
  │
  ├── global.json                         SDK 版本锁定
  └── CMW.csproj                          项目文件

---

## 版本历史

  8.0-P     灵动岛原型
  8.0       课表模块
  8.3.1     适配 net6.0
  A1.x      Bug 修复 + 触屏兼容
  26.x      全功能优化 + PCL2 主题
  27.x      座位表 / 日历 / 截图笔记 / 投票 / 班牌
  28.0.0    天级换课 + 通知条宽高 + 大量优化
  30.0.0    正式冻结版本
  31.0.0    字体修复 + 双主题 + 灵动岛横条重构
  32.0.0    横条过渡动画 + 组排序 + 多条倒计时 +
            渐变背景 + 图标化通知 + BIOS 开机引导 +
            辅助窗口基类 + 崩溃日志分类

---

## 许可证

本项目采用 MIT License，详见 LICENSE 文件。

Copyright © 2026 alpaca

---

## 作者

  · 设计 / 开发：alpaca
  · 单位：武汉外国语学校高中部

---

## 致谢

本项目开发过程中得到了 AI 辅助，在此表示感谢。