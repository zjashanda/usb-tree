---
name: usb-tree
description: 按物理 USB 上游连接、PnP 父设备、Container ID 和 USB 音频身份自动树状整理 Windows COM 串口、Linux ttyUSB/ttyACM 串口及音频端点；提供 USB 设备与声卡两栏 GUI、按栏复制/导出、声卡列表和 usbTree-cli 终端输出；当用户需要确认串口/声卡是否属于同一块板卡、查看 CH344 A/B/C/D 接口映射、查看板卡对应声卡、复制或导出串口和声卡信息、运行 usbTree 工具或重建跨平台版本时使用。
---

# usbTree USB 串口与声卡树状工具

## 概述

这个 Skill 用于运行和维护 `usbTree` 工具。工具把“同一块板卡通过 USB Hub 暴露出的多个 USB 功能”整理成以下层级：

```text
物理 USB 板卡
├─ 串口设备功能
│  ├─ A  COMx / ttyUSBx
│  ├─ B  COMx / ttyUSBx
│  ├─ C  COMx / ttyUSBx
│  └─ D  COMx / ttyUSBx
├─ 另一项串口设备功能
│  └─ COMx / ttyACMx
└─ 音频端点
   ├─ Render   2ch  扬声器
   └─ Capture  8ch  麦克风
```

GUI 固定为两栏：左侧是 USB 设备树（串口 + 归属该板卡的音频端点），右侧是声卡列表（全部活动音频端点，包括独立声卡和板卡声卡）。点击哪一栏，复制/导出就作用于哪一栏。工具只读，不修改 Windows PnP、COM 编号、驱动、注册表、USB 描述符或 Linux 音频配置。

## 触发场景

出现以下需求时使用本 Skill：

- 查看 USB 串口和声卡，并按板卡而不是只按 COM 编号/设备名排列。
- 判断 CH344、ListenAI VCOM、USB 音频端点是否来自同一根 USB 连接/同一块板卡。
- 需要按 CH344 的 `SERIAL-A`、`SERIAL-B`、`SERIAL-C`、`SERIAL-D` 顺序显示 COM 号。
- 需要查看某块板卡对应的 Render/Capture 声卡，或列出电脑上全部活动音频端点。
- 不想手抄串口/声卡信息，需要在 GUI 复制当前栏的选中项或全部信息，或导出 TXT、CSV、Markdown。
- 需要在终端直接查看与 GUI 一致的串口和声卡信息，例如写脚本、记录测试环境。
- 在 Windows 运行 `usbTree.exe`，或在 Linux 查看 `ttyUSB*`、`ttyACM*` 和 ALSA 声卡。
- 修改工具的分组、接口排序、名称、图标或重新编译跨平台执行文件。

## 工作流程

### Windows

可执行工具位于本 Skill 的 `scripts` 目录。直接启动：

```powershell
$skillDir = 'E:\aswxStation\tmpskill\102002usbTre'
& "$skillDir\scripts\usbTree.exe"
```

窗口分为两栏：

- 左栏是 USB 设备信息：上半部分为板卡树，下半部分显示选中串口/设备组/板卡/音频端点的详细信息。
- 右栏是声卡信息：上半部分为全部活动音频端点列表，下半部分显示选中声卡的详细信息。
- USB 板卡树中的“音频端点”分支只显示能与该板卡关联的端点；独立声卡只出现在右栏。
- 声卡列表包含 USB 声卡和独立声卡，每行显示方向、状态、DeviceKey、通道数、显示名称和所属板卡。
- 工具默认每 2 秒刷新，也支持手动刷新。点击左栏或右栏会切换复制范围，工具栏会显示“复制范围: USB 设备”或“复制范围: 声卡列表”。

#### 复制与导出

工具栏：

- `复制选中`：复制当前点击栏中的选中项。USB 栏复制树节点详情；声卡栏复制该声卡的完整信息。
- `复制全部`：按“复制格式”复制当前点击栏的全部信息。
- `复制格式`：`摘要`、`简单列表`、`Markdown`、`CSV`、`详细信息`、`全部（USB+声卡）`。
  - USB 栏的 `摘要` 是板卡树；声卡栏的 `摘要` 是声卡表。
  - `全部（USB+声卡）` 始终输出 USB 树 + 声卡列表，不受当前栏影响。
- `导出...`：把当前栏当前格式保存为 UTF-8 文件；CSV 可用 Excel 打开。
- 右键菜单：USB 栏可复制串口名/选中项；声卡栏可复制 DeviceKey/选中声卡/全部声卡。
- `Ctrl+C` 复制当前栏选中项，`Ctrl+Shift+C` 复制当前栏全部信息。

#### 命令行输出

`scripts\usbTree-cli.exe` 不打开窗口，默认输出与 GUI 左侧树相同的结构（包含 USB 板卡的音频端点）：

```powershell
$skillDir = 'E:\aswxStation\tmpskill\102002usbTre'
& "$skillDir\scripts\usbTree-cli.exe"
& "$skillDir\scripts\usbTree-cli.exe" --audio
& "$skillDir\scripts\usbTree-cli.exe" --all
```

输出示例：

```text
物理 USB 板卡（2 个）
  ListenAI 板卡 #1
    CH344 Quad_Serial  (4 个串口)
      A  COM13
      B  COM15
      C  COM18
      D  COM12
    ListenAI VCOM  (1 个串口)
      COM14
    音频端点  (2 个)
      Capture  8ch  麦克风 (ListenAI Audio)
      Render   2ch  扬声器 (ListenAI Audio)
```

常用参数：

```powershell
& "$skillDir\scripts\usbTree-cli.exe" --format plain
& "$skillDir\scripts\usbTree-cli.exe" --format csv --out ports.csv
& "$skillDir\scripts\usbTree-cli.exe" --audio
& "$skillDir\scripts\usbTree-cli.exe" --audio --format csv --out audio.csv
& "$skillDir\scripts\usbTree-cli.exe" --all --out device.txt
& "$skillDir\scripts\usbTree-cli.exe" --format detail --copy
& "$skillDir\scripts\usbTree-cli.exe" --help
```

- `-a, --audio`：只输出声卡列表，包含全部活动音频端点及其所属板卡。
- `--all`：输出 USB 树 + 声卡列表。
- `--include-disabled`：声卡列表包含禁用、未连接和不存在的端点，便于排查历史设备。
- `-f, --format <tree|plain|md|csv|detail|audio|all>`：默认 `tree`；USB 模式与 GUI 树一致，`--audio` 模式下 `tree/plain` 输出表格或 TSV，`md/csv/detail` 输出对应声卡格式；`audio`/`all` 是 `--audio`/`--all` 的别名。
- `-o, --out <文件>`：同时写入带 BOM 的 UTF-8 文件，适合保存测试现场结果。
- `-c, --copy`：同时复制到剪贴板。
- `-l, --list`：显式要求在终端输出，默认行为；`-h, --help` 查看帮助。

PowerShell 5.1 通过管道读取中文输出时可能按本地代码页解码。需要稳定文件结果时优先使用 `--out`，或先在终端执行 `chcp 65001`。

CH344 端口按接口名称排序。例如当前设备的映射为：

```text
A  COM13
B  COM15
C  COM18
D  COM12
```

ListenAI VCOM 显示为单独的设备功能，例如 `COM14`。如果电脑上存在多个相同名称的板卡，工具会自动添加 `#1`、`#2` 编号。

音频端点优先按 PnP `Container ID` 关联到对应板卡；缺失时用 USB 设备 Key 中的尾标识与串口实例匹配。`{00000000-0000-0000-FFFF-FFFFFFFFFFFF}` 这类占位 Container ID 会被忽略，避免把主板/Realtek 声卡误挂到串口板卡。

### Linux

Linux 版本使用 Python 标准库读取 `/sys/class/tty`、USB sysfs 和 ALSA，不需要 `pyudev`：

```bash
cd /path/to/102002usbTre/scripts
chmod +x usbTree-linux usbTree-linux.py
./usbTree-linux --once
```

无参数时默认持续刷新（兼容旧用法）；`--once` 只输出一次。带 `--format`、`--audio`、`--all`、`--out` 或 `--copy` 时自动按一次输出处理，`--watch` 可强制持续刷新：

```bash
./usbTree-linux --format plain
./usbTree-linux --audio
./usbTree-linux --audio --format csv --out audio.csv
./usbTree-linux --all --out device.txt
./usbTree-linux --watch --interval 5
```

Linux 与 Windows 使用相同的参数和输出列：

- 默认树与 Windows 一致：两空格缩进、板卡/设备功能/串口、CH344 的 A/B/C/D、`音频端点` 分支。
- USB 声卡按 USB 上游路径合并到对应板卡；独立 ALSA 声卡只出现在声卡列表。
- 声卡列表包含 Direction、State、DeviceKey、Channels、FriendlyName 和所属板卡；通道数从 `/proc/asound/card*/stream*` 尽力解析，未知时显示 `?`。
- `--copy` 需要系统存在 `wl-copy`、`xclip`、`xsel` 或 `pbcopy`，否则只输出到终端并返回失败。
- Linux 没有 Windows 的活动/禁用状态，`--include-disabled` 仅为参数兼容。

Linux 目录中提供的是可直接执行的 Python 脚本和启动器，不是预编译 ELF 二进制。

### 重新构建 Windows 版本

Windows 主机自带 .NET Framework 编译器时，可以在 PowerShell 中执行：

```powershell
$skillDir = 'E:\aswxStation\tmpskill\102002usbTre'
Set-Location "$skillDir\scripts"
& .\make-icon.ps1
& .\build.ps1
```

构建结果为 GUI 版 `scripts\usbTree.exe` 和命令行版 `scripts\usbTree-cli.exe`，源码来自 `references\UsbSerialTreeViewer.cs`，图标来自 `assets\usbTree.ico`。重新构建前需要关闭正在运行的两个程序，否则 Windows 会锁定 exe 文件。

## 分组规则

Windows 端优先使用以下信息：

1. 物理板卡层：串口位置路径去掉最后的设备接口节点后形成的共享 USB 上游路径。
2. 设备功能层：PnP `Container ID`，缺失时使用父设备实例，再缺失时使用串口实例 ID。
3. 串口层：FriendlyName 中的 `SERIAL-A/B/C/D`；没有字母时按 COM 号排序。
4. 音频层：声卡列表读取 MMDevices 的 Render/Capture 端点；与板卡关联时先比较 `Container ID`，再用 USB Key 尾标识/VID-PID 匹配串口实例。匹配不到或使用占位 Container ID 的声卡只保留在右侧声卡列表。

Linux 端按 USB 上游路径把串口和 USB 声卡归到同一板卡；ALSA 声卡没有 PnP Container ID，独立声卡只出现在声卡列表。Linux 的 DeviceKey 使用 `VID_xxxx&PID_xxxx:尾标识`，EndpointId 使用 `cardN`，与 Windows 的端点 GUID 不要求相同。

因此，工具可以把一个 USB Hub 后面的 CH344、ListenAI VCOM 和音频端点可视化放在同一块板卡下，但不会伪造一个 Windows PnP 父设备。设备管理器仍会按 USB 设备描述符、设备地址和驱动关系分别枚举它们。

## 资源

- `scripts/usbTree.exe`：Windows 只读两栏 WinForms 工具，显示 USB 树和声卡列表，支持按栏复制和导出。
- `scripts/usbTree-cli.exe`：Windows 命令行输出 USB/声卡信息，支持复制和导出。
- `scripts/usbTree-linux`：Linux 启动器。
- `scripts/usbTree-linux.py`：Linux 标准库实现，支持串口 + ALSA 声卡和与 Windows 一致的参数。
- `scripts/build.ps1`：Windows 构建脚本。
- `scripts/make-icon.ps1`：生成 USB 树状图标。
- `references/UsbSerialTreeViewer.cs`：Windows 源码。
- `assets/usbTree.ico`：Windows EXE 图标。

## 限制与安全边界

- 工具只读取设备枚举信息，不改变 COM 号、驱动、设备管理器树、USB 固件或 ALSA 配置。
- Windows 默认只列出活动音频端点；排查历史设备时使用 `--include-disabled`。禁用/未连接端点可能很多。
- 剪贴板复制需要与工具处于同一个 Windows 用户会话；远程桌面、服务会话或无交互桌面可能无法访问剪贴板，此时改用 `--out` 导出文件。Linux 的 `--copy` 还要求安装 `wl-copy`、`xclip`、`xsel` 或 `pbcopy`。
- 终端显示中文需要终端代码页支持 UTF-8；Windows Terminal、PowerShell 7 通常直接支持，旧版控制台可先执行 `chcp 65001`。
- 共享上游路径表示 USB 拓扑关系，不能单独证明两个设备一定属于同一产品外壳；必要时还要结合 VID/PID、序列号、父设备和实际硬件确认。
- 音频端点与板卡的关联是启发式的：Container ID 最可靠，USB Key/VID-PID 是兜底。多块同型号板卡同时连接时，应以板卡声卡列表中的 DeviceKey 和所属板卡为准再复核。
- Linux 版本需要目标主机有 Python 3，并且当前用户能读取 `/sys/class/tty` 和 `/proc/asound`；串口/声卡权限仍由 Linux 设备组和 udev/ALSA 规则决定。Linux 通道数解析是尽力而为，未实测硬件时不要宣称已验证。
- 不要在 Skill 中保存 API 密钥、串口密码或其他凭据。
