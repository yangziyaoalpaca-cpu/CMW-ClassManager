# CMW 班级管理工具

> 武汉外国语学校高中部班级日常管理工具
>
> 当前版本：**33.0.0**
>
> 界面风格：PCL2 启动器风 + ClassIsland 深色主题（可切换）

---

## 项目简介

**CMW**（Class Management Widget）是一款基于 **WPF + .NET 6**
开发的 Windows 桌面班级管理软件，覆盖高中班级日常管理的各类场景：
课表、作业、待办、值日、名单、考试、座位表、日历、统计、电子班牌……

界面采用 PCL2 启动器风格：深蓝顶栏 + 浅灰背景 + 白色卡片，
扁平化、圆角、假阴影，可运行时切换 ClassIsland 深色主题。

专为**班级触摸白板**优化：大字、触摸友好、开机自启、常驻桌面。

---

## 功能一览

### 十三大功能页面

| 页面 | 说明 |
|---|---|
| **首页** | 数据概览卡片（今日作业 / 待办 / 值日 / 下节课）+ 考试倒计时 |
| **课表** | 每周固定课表 + 单节临时换课 + 天级换课 + CSV 导入 |
| **待办** | 新增 / 全部 / 今日 三视图，回车快速添加 |
| **值日** | 值日组管理 + 每日自动轮换 |
| **作业** | 布置 / 追踪 / 催收 / 点名 / 从模板添加 / 编辑 |
| **汇总** | 今日课表 + 值日 + 作业 + 待办 一屏总览 |
| **名单** | 名单管理 + 随机点名 + 班级投票 |
| **考试** | 成绩录入 / 列表 / 趋势图 / CSV 导出 |
| **周报** | 每周自动生成 Markdown 报告 |
| **统计** | 作业趋势 / 待办趋势 / 值日统计图表 |
| **座位表** | 可视化排座，多方案切换 |
| **日历** | 月视图 + 活动管理 |
| **设置** | 8 个分类标签页，全部可配置 |

### 桌面常驻组件

- **灵动岛** — 屏幕顶部黑色胶囊
  - 单击展开卡片：菜单 / 作业 / 待办 / 课表 / 笔记 / 计时
  - 右键或上下课可延伸为**横条**
- **横条** — 从胶囊延伸出来
  - 6 组内容自动轮播：日期 / 课表 / 状态 / 作业 / 值日 / 待办
  - 顺序可自定义，超宽自动往返滚动
  - 上下课自动延伸 / 缩回
  - **横条常驻**：开启后上课时横条也不收回
- **右侧常驻窗口** — 贴在屏幕右侧显示作业 / 值日 / 待办
- **电子班牌** — 全屏深色展示当天信息，适合班级信息屏
- **课堂倒计时** — 课堂限时活动工具
- **课上截图笔记** — 截图 + 文字备注保存

### 缩回目标

关闭主窗口时可选择缩回到哪种形态：

- **缩回为胶囊**（默认）：黑色小胶囊
- **缩回为横条**：直接展示横条

### 主题与外观

- **双主题**：PCL2（浅色）/ ClassIsland（深色），运行时切换
- **全局 R 角**：设置里一个滑块控制所有卡片 / 按钮 / 输入框圆角
- **假阴影**：无性能开销的右下 1px 描线阴影
- **主页背景**：图片 / 4 种渐变预设（深蓝斜向、晨雾浅蓝、
  暗夜紫灰、墨绿山水）
- **全局字体**：可切换字体，首页数据卡字体自动缩放

### 通知与提醒

- 上下课提醒（可分开设置 / 4 种样式）
- 上下课表现方式（变色 → 通知 / 仅通知 / 仅变色 + 横条 / 都不做）
- 待办提醒（多时间点）
- 值日提醒（多时间点）
- 作业催收（滚动显示未交名单）
- 每日 08:00 自动汇总

### 自动开关机

- **关机**：生成 `power_off.bat`，可导入 Windows 任务计划程序
- **开机**：Windows 关机后软件无法唤醒，提供 **BIOS RTC 定时唤醒**
  设置引导，含主流品牌进入 BIOS 按键对照

---

## 技术栈

- **框架**：.NET 6.0（net6.0-windows）
- **语言**：C# 10.0
- **UI**：WPF
- **JSON**：Newtonsoft.Json 13.0.3
- **SDK**：6.0.428（`global.json` 严格锁定）

---

## 环境要求

### 支持的系统

- Windows 10 1607+
- Windows 11
- Windows Server 2016+

### 不支持

- Windows 7 / 8 / 8.1
- 极精简版 Windows

### 开发环境

- .NET SDK 6.0.428（`global.json` 已锁定）
- 命令行 `dotnet`（不需要 Visual Studio）

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

    # 自包含多文件 + R2R（约 250MB，启动更快）
    dotnet publish -c Release -r win-x64 --self-contained true \
      -p:PublishReadyToRun=true

    # 框架依赖版本（约 5MB，需要目标机装 .NET 6 运行时）
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
    ├── ReleasableWindow.cs                 辅助窗口基类
    ├── HomePage ~ SettingsPage             13 个功能页（各 xaml + cs）
    ├── RollCallWindow ~ BiosWakeGuideWindow 13 个弹窗（各 xaml + cs）
    ├── LessonCell.xaml / .cs               课程单元格控件
    │
    ├── Models/
    │   └── Models.cs                       所有数据类 + 工具类
    ├── Services/                           10 个服务类
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
    ├── CMW.csproj                          项目文件
    └── README.md                           本文件

---

## 版本历史

| 版本 | 亮点 |
|---|---|
| **8.0-P** | 灵动岛原型 |
| **8.0** | 课表模块 |
| **8.3.1** | 适配 net6.0 |
| **A1.x** | Bug 修复 + 触屏兼容 + 开机自启动 |
| **26.x** | 全功能优化 + PCL2 主题 + 数据安全 |
| **27.x** | 座位表 / 日历 / 截图笔记 / 投票 / 班牌 / 倒计时 |
| **28.0.0** | 天级换课 + 通知条宽高 + 大量优化 |
| **30.0.0** | 正式冻结版本 |
| **31.0.0** | 字体修复 + 双主题 + 灵动岛横条重构 |
| **32.0.0** | 横条动画重构 + 组排序 + 多条倒计时 + 渐变背景 + 图标化通知 + BIOS 开机引导 + 辅助窗口基类 + 崩溃日志分类 |
| **33.0.0** | PCL2 假阴影 + 全局 R 角 + 侧栏胶囊文字 + 切换即生效 + 版本历程 + 侧栏滚动兜底 + 横条常驻 + 缩回目标可选胶囊/横条 |

---

## 许可证

本项目采用 **MIT License**，详见 [LICENSE](LICENSE) 文件。

Copyright © 2026 alpaca

---

## 作者

- **设计 / 开发**：alpaca
- **单位**：武汉外国语学校高中部

---

## 致谢

本项目开发过程中得到了 AI 辅助，在此表示感谢。