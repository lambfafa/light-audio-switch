# 轻音切换 · Light Audio Switch

一个轻量的 Windows 托盘工具：游戏运行时使用耳机，所有游戏退出后自动切回日常音箱。

A lightweight Windows tray utility that switches the default audio output between your gaming headset and everyday speakers.

## 下载与使用

[下载 Windows x64 便携版](https://github.com/lambfafa/light-audio-switch/raw/refs/heads/main/downloads/LightAudioSwitch-win-x64.zip)

1. 解压到当前用户可写的文件夹，运行 `LightAudioSwitch.exe`。
2. 首次运行，选择“游戏 → 耳机”和“日常 → 音箱”，点击“保存设置”。
3. 已预置《三角洲行动》的游戏进程名。其他游戏可从运行中的程序或 EXE 文件添加。
4. 关闭设置窗口后仍会留在托盘运行；双击托盘图标重新打开设置。

需要蓝牙音箱时，请先通过 Windows 完成配对与连接。本程序管理已经可用的音频输出设备。

## 功能

- 任意已添加的游戏运行时切到耳机；全部退出后默认等待 3 秒切回音箱。
- WeGame、Steam 常驻不会触发；游戏运行期间切桌面、切浏览器仍保持耳机。
- 托盘可锁定耳机、锁定音箱，或恢复自动切换。
- 目标设备离线时保留当前输出；连接恢复后重新判断。
- 可选登录自启动、切换默认通信输出；保留麦克风和设备音量。
- 单实例、本地配置、日志轮换；没有网络请求、驱动或游戏进程注入。

## 轻量实现

C#、WinForms、Windows Core Audio 与系统自带 .NET Framework；无第三方运行时依赖，无需安装 SDK 即可构建。

原型在 Windows 10 22H2 上的参考测量：主程序约 46 KiB；设置窗口关闭时工作集约 32 MiB。15 秒短时采样中，20 逻辑核电脑的整机 CPU 占用约 0.02%。这些是单台电脑的参考值，实际占用随系统与设备变化。

## 本地构建

要求 Windows x64 与 .NET Framework 4.x，已在 Windows 10 22H2 验证；Windows 11 尚未实机验证。

```powershell
.\build.ps1
.\dist\LightAudioSwitch.exe
```

构建脚本使用 Windows 的 `Microsoft.NET\Framework64\v4.0.30319\csc.exe`，不下载包、不安装软件。

## 测试

默认测试检查路由策略、延迟、手动锁定、进程识别和配置保存，不改变音频输出：

```powershell
.\test.ps1
```

可选硬件集成测试会临时切换声音输出并恢复原来的三个默认输出角色。请先退出正在运行的轻音切换，以免实例互相干扰。提供能唯一匹配的两个已连接设备名称：

```powershell
.\test.ps1 -Hardware -HeadsetName "My Headset" -SpeakerName "My Speaker"
```

测试使用独立的受控进程，不会启动真实游戏。实际游戏或浏览器正在播放的音频是否立即迁移，仍取决于应用行为。

## 兼容性与限制

- 更改系统默认输出不等于强制搬运所有应用的音频流。希望跟随的应用应使用“默认”输出。
- 部分游戏启动时固定设备，可在游戏内指定耳机，或启动前先锁定耳机。
- Windows 中给应用单独指定的设备可能优先于系统默认设备。
- 当前版本采用“游戏运行优先”，不是“前台窗口优先”；游戏与浏览器同时运行时都可能走耳机。
- 仅提供程序名规则时按完整 EXE 名匹配；有路径时尽量校验，受保护进程不允许路径查询时退回程序名匹配。
- 默认设备设置使用未公开的 `IPolicyConfig` COM 接口，Windows 更新后需要重新验证。
- 设备重新配对或更换驱动后，设备 ID 可能改变，需要重新选择。

## 数据与项目结构

设备 ID、选择和游戏路径仅保存在程序旁的 `config.json`；日志为 `运行日志.txt`，这些文件均在 `.gitignore` 中排除。仓库与下载包不包含任何个人设备 ID、账号凭据或本机游戏安装路径。

```text
src/          应用源代码、图标与运行清单
tests/        策略测试与可选硬件集成测试
docs/         使用说明与验证范围
downloads/    可直接解压运行的便携版本
build.ps1     本地构建
test.ps1      本地验证
```

## 许可

[MIT License](LICENSE)。欢迎提交 Issue 和 Pull Request。
