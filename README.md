# MouseForge

Windows 上的**鼠标脚本录制 + 可视化编辑 + 循环播放**小工具。

用鼠标点几下就能录下一段操作，然后在表格里调整坐标、按键、等待时间，设置循环次数和间隔后播放。适合重复点击、固定流程、简单的游戏/页面操作。

## 下载即用

1. 下载或克隆本仓库。
2. 双击 `MouseForge.exe` 或 `Start-MouseForge.cmd`。
3. 第一次启动会自动创建 `projects\默认项目.json`。

不需要安装 .NET SDK，Windows 10/11 自带 .NET Framework 4.x 即可运行。

## Windows SmartScreen 提示

`MouseForge.exe` 没有购买受信任的代码签名证书，所以第一次运行时 Windows 可能提示：

> Windows 已保护你的电脑
> Microsoft Defender SmartScreen 阻止了无法识别的应用启动。

这是 **“未识别的应用”** 提示，不是杀毒软件报毒。处理方式：

1. 点 **“更多信息”**。
2. 点 **“仍要运行”**。

如果文件被标记为来自网络，也可以右键 exe → 属性 → 勾选 **“解除锁定”** → 确定，或运行：

```powershell
Unblock-File .\MouseForge.exe
```

请不要为了这个提示关闭整个系统的 SmartScreen。

**安全说明**

- 程序不联网，不上传任何数据。
- 只读写程序目录下的 `projects` 文件夹。
- 完整源码在 `MouseForge.cs` 和 `MainForm.cs`，可以自己用 `build-MouseForge.cmd` 重新编译。
- 发布包附带 `SHA256.txt`，可用于校验 exe 是否完整。

## 功能

- **项目管理**：新建、保存、删除不同项目，每个项目有独立的鼠标步骤和循环配置。
- **鼠标录制**：按 `F9` 开始，记录左键/右键/中键点击；不记录鼠标移动轨迹；两次点击之间的间隔自动生成等待步骤。
- **可视化编辑**：
  - 添加点击、移动、等待步骤。
  - 修改坐标、按键、移动耗时、按住时长、等待时间。
  - 复制、删除、上移、下移。
  - 按住右 Alt + 左键拾取屏幕坐标。
- **循环播放**：
  - 循环次数：`0` 表示无限循环。
  - 循环间隔、开始等待。
  - `F10` 播放/停止，`F11` 暂停/继续。
  - 播放结束后尽量还原鼠标位置。

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `F9` | 开始/停止录制 |
| `F10` | 开始/停止播放 |
| `F11` | 暂停/继续播放 |
| `Esc` | 取消坐标拾取 |

## 快速上手

1. 双击 `Start-MouseForge.cmd`。
2. 点 **● 录制 (F9)**。
3. 在目标窗口里点击几次。
4. 再按 `F9` 停止，点 **保存**。
5. 在右侧调整步骤，在左侧设置循环次数/间隔。
6. 点 **▶ 播放 (F10)**。

> 录制时只记录“点击”，不会记录鼠标移动轨迹。步骤里的“移动耗时”可以让播放时平滑移动到目标点。

## 坐标与 DPI

程序是 DPI 感知的，使用**物理像素坐标**。在 200% 缩放的屏幕上，坐标数值是逻辑像素的两倍。

录制、拾取、播放使用同一套物理坐标，不需要手动换算。它和很多旧鼠标脚本使用的坐标系一致。

## 项目文件

项目保存在 `projects\<项目名>.json`，例如：

```json
{
  "name": "我的项目",
  "loopCount": 0,
  "loopIntervalSeconds": 0,
  "startDelaySeconds": 3,
  "restoreCursorAfterClick": true,
  "defaultHoldMs": 40,
  "steps": [
    { "type": "click", "x": 500, "y": 300, "button": "Left", "moveMs": 0, "holdMs": 40, "seconds": 1 },
    { "type": "wait", "x": 0, "y": 0, "button": "Left", "moveMs": 0, "holdMs": 40, "seconds": 2 }
  ]
}
```

## 从源码构建

源码是纯 C# WinForms，不需要 Visual Studio。

双击 `build-MouseForge.cmd`，它会调用 Windows 自带的 `csc.exe` 编译出 `MouseForge.exe`。

手动编译命令：

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe /codepage:65001 ^
  /out:MouseForge.exe ^
  /r:System.dll /r:System.Core.dll ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:System.Web.Extensions.dll ^
  MouseForge.cs MainForm.cs
```

## 已知限制

- v1 只支持鼠标动作和等待，不支持键盘动作。
- 播放按屏幕坐标点击，**不会自动查找或激活窗口**；目标窗口需要保持可见/在前台。
- 如果目标程序以管理员权限运行，Windows 会拦截普通权限程序发出的模拟点击和全局热键。请右键以管理员身份运行 MouseForge。
- 不支持条件、随机、每步独立循环等高级逻辑。
- 高 DPI 下程序会按系统缩放放大界面；如果系统缩放特别大，窗口可能接近全屏。

## License

MIT。详见 `LICENSE`。
