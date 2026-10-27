---
name: usb-tree
description: 按物理 USB 上游连接、PnP 父设备、Container ID 和 USB 音频身份自动树状整理 Windows COM 串口、Linux ttyUSB/ttyACM 串口及音频端点；提供 USB 设备与声卡两栏 GUI、按栏复制/导出、声卡列表、usbTree-cli 终端输出，以及 Windows 11 智能应用控制下的脚本启动器和可选代码签名；当用户需要确认串口/声卡是否属于同一块板卡、查看 CH344 A-D、CH348 A-H 接口映射、查看板卡对应声卡、复制或导出串口和声卡信息、运行 usbTree 工具或重建跨平台版本时使用。
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
- 需要按 CH344 的 `SERIAL-A..D` 或 CH348 的 `Ch A..H` 顺序显示 COM 号。
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

如果目标电脑开启了 Windows 11 智能应用控制（SAC），未签名的 `usbTree.exe` 可能在启动前被拦截。不要为了本工具关闭 SAC；改用 `scripts\usbTree.vbs`（推荐，无控制台窗口）或 `scripts\usbTree.cmd` 启动，详见下方“Windows 11 智能应用控制”。

窗口分为两栏：

- 左栏是 USB 设备信息：上半部分为板卡树，下半部分显示选中串口/设备组/板卡/音频端点的详细信息。
- 右栏是声卡信息：上半部分为全部活动音频端点列表，下半部分显示选中声卡的详细信息。
- USB 板卡树中的“音频端点”分支只显示能与该板卡关联的端点，每行按“方向、通道数、DeviceKey、显示名称”显示；独立声卡只出现在右栏。
- 声卡列表包含 USB 声卡和独立声卡，每行显示方向、状态、通道数、显示名称、所属板卡和 DeviceKey。
- 工具默认每 2 秒刷新，也支持手动刷新。点击左栏或右栏会切换复制范围，工具栏会显示“复制范围: USB 设备”或“复制范围: 声卡列表”。

#### 复制与导出

工具栏：

- `复制选中`：复制当前点击栏中的选中项。USB 栏选中板卡或设备功能时，复制该节点及子节点的树状结构（含父板卡标题）；选中串口时复制详细 PnP 信息；声卡栏复制选中声卡的完整信息。
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
      Capture  8ch  VID_8765&PID_5678:9_2A847557_0_0000  麦克风 (ListenAI Audio)
      Render   2ch  VID_8765&PID_5678:9_2A847557_0_0000  扬声器 (ListenAI Audio)
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

#### Windows 11 智能应用控制（SAC）

开启 SAC 的 Windows 11 会把未签名的 `usbTree.exe`/`usbTree-cli.exe` 当作不受信任应用拦截，程序可能连启动阶段都到不了，而且通常不能只放行单个文件。

不要关闭 Smart App Control：它影响整台电脑的应用检查，关闭后可能需要重置或重装系统才能重新开启，也不应为了一个只读工具降低整机安全级别。

优先使用随仓库提供的免 exe 启动器：

- `scripts\usbTree.vbs`：双击启动 GUI，无控制台窗口，推荐。
- `scripts\usbTree.cmd`：双击启动 GUI；在终端带参数运行时会转成命令行模式，例如 `usbTree.cmd --audio`。
- `scripts\usbTree-cli.cmd --audio`、`--all`、`--format csv --out audio.csv`：命令行模式。
- `scripts\usbTree.ps1` / `scripts\usbTree-cli.ps1`：直接调用 PowerShell 脚本的入口。

这些入口调用系统自带、已签名的 64 位 Windows PowerShell 5.1，加载同目录的 `usbTree.dll`，不会启动未签名的 exe。需要 `usbTree.dll`、`usbTree.ps1`、`usbTree-cli.ps1` 和启动脚本保持在同一目录。不要用 PowerShell 7（`pwsh`）的 MTA 线程直接跑 GUI；`.cmd`/`.vbs` 已固定使用 Windows PowerShell 5.1（STA）。

如果文件来自下载的 zip，先解除文件锁定：

```powershell
Get-ChildItem -LiteralPath '<skill目录>\scripts' -Recurse | Unblock-File
```

如果必须分发 exe，需要受信任的代码签名证书。SAC 还会结合应用信誉判断，新证书也可能被拦截，EV 代码签名证书最稳妥：

```powershell
Set-Location '<skill目录>\scripts'
.\build.ps1 -CertificateThumbprint <证书指纹> `
  -SignToolPath 'C:\Program Files (x86)\Windows Kits\10\bin\<版本>\x64\signtool.exe'
```

`build.ps1` 会同时签名 `usbTree.exe`、`usbTree-cli.exe` 和 `usbTree.dll`。证书默认按当前用户“个人/我的”证书存储查找；没有可信证书时请使用脚本启动器，不要用自签名证书冒充受信任发布者。若单位策略同时拦截 PowerShell 脚本，只能申请组织策略例外或使用已签名的 exe。

CH344 端口按接口名称排序。例如当前设备的映射为：

```text
A  COM13
B  COM15
C  COM18
D  COM12
```

ListenAI VCOM 显示为单独的设备功能，例如 `COM14`。如果电脑上存在多个相同名称的板卡，工具会自动添加 `#1`、`#2` 编号。

多串口子设备（例如 WCH USB2MCOM/CH348，设备管理器显示 `Ch A` 到 `Ch H`）没有 LocationPaths，但共享同一个 PnP Parent。工具会按 Parent 把它们合并成一个设备功能，并按 `Ch A..Ch H` 显示 `A` 到 `H`，不会再把每个 COM 当成独立板卡。8 口板会显示为类似：

```text
USB 板卡
  USB To Multi Serial  (8 个串口)
    A  COM3
    B  COM8
    C  COM4
    D  COM7
    E  COM5
    F  COM10
    G  COM9
    H  COM6
```

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

- 默认树与 Windows 一致：两空格缩进、板卡/设备功能/串口、CH344 的 A/B/C/D、`音频端点` 分支；音频行依次显示方向、通道数、DeviceKey 和名称。
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

构建结果为 GUI 版 `scripts\usbTree.exe`、命令行版 `scripts\usbTree-cli.exe` 和供脚本启动器加载的 `scripts\usbTree.dll`，源码来自 `references\UsbSerialTreeViewer.cs`，图标来自 `assets\usbTree.ico`。重新构建前需要关闭正在运行的程序，否则 Windows 会锁定 exe/dll 文件。

默认构建不签名。若在开启 Smart App Control 的电脑上必须直接运行 exe，请使用 `-CertificateThumbprint` 指定受信任代码签名证书，`build.ps1` 会签名 exe 和 dll；没有证书时使用 `usbTree.vbs`/`usbTree.cmd`/`usbTree-cli.cmd`。

## 分组规则

Windows 端优先使用以下信息：

1. USB 设备层（硬分组）：优先使用 PnP `Parent`（父 USB 设备实例），没有 Parent 时才用 `Container ID`，再退回实例 ID。同一 Parent 的 COM 口视为同一个 USB 设备，例如 WCH USB2MCOM/CH348 的 8 个口、CH344 的 4 个口。
2. 板卡层（软分组）：只对已知的 ListenAI CH344 + VCOM 组合，在同一共享上游路径下合并为 `ListenAI 板卡`；其他不同的 USB 设备即使接在同一个 Hub 下也保持独立板卡，避免把两块独立板卡误合并。
3. 串口层：FriendlyName 中的 `SERIAL-A..Z` 或 `Ch A..Z`；没有字母时按 COM 号排序。板卡、设备功能、端口都有稳定的次级排序键，避免刷新后 `#1/#2` 互换。
4. 音频层：声卡列表读取 MMDevices 的 Render/Capture 端点；与板卡关联时先比较 `Container ID`，再用 USB Key 尾标识/VID-PID 匹配串口实例。匹配不到或使用占位 Container ID 的声卡只保留在右侧声卡列表。

Linux 端按 USB 上游路径归组，ALSA 声卡没有 PnP Container ID，独立声卡只出现在声卡列表。Linux 的 DeviceKey 使用 `VID_xxxx&PID_xxxx:尾标识`，EndpointId 使用 `cardN`，与 Windows 的端点 GUID 不要求相同；Linux 尚未做与 Windows 完全相同的已知板卡合并规则，真机验证前不要把 Linux 的板卡层当作强保证。

因此，同一 USB 设备的多个串口会被可靠合并；已知 ListenAI 板卡保留 CH344 + VCOM 的板卡视图；其他多 USB 设备的产品会显示为多个独立设备，宁可拆开也不误合并。USB 设备层来自 Windows PnP 关系，板卡层是明确的产品规则，不伪造 PnP 父设备。

## 资源

- `scripts/usbTree.exe`：Windows 只读两栏 WinForms 工具，显示 USB 树和声卡列表，支持按栏复制和导出。
- `scripts/usbTree-cli.exe`：Windows 命令行输出 USB/声卡信息，支持复制和导出。
- `scripts/usbTree.dll`：同一套 GUI/CLI 的托管程序集，供免 exe 启动器加载。
- `scripts/usbTree.ps1`、`scripts/usbTree.cmd`、`scripts/usbTree.vbs`：GUI 启动器；`.vbs` 无控制台窗口。
- `scripts/usbTree-cli.ps1`、`scripts/usbTree-cli.cmd`：命令行启动器。
- `scripts/usbTree-linux`：Linux 启动器。
- `scripts/usbTree-linux.py`：Linux 标准库实现，支持串口 + ALSA 声卡和与 Windows 一致的参数。
- `scripts/build.ps1`：Windows 构建脚本，可选代码签名。
- `scripts/make-icon.ps1`：生成 USB 树状图标。
- `references/UsbSerialTreeViewer.cs`：Windows 源码。
- `assets/usbTree.ico`：Windows EXE 图标。

## 限制与安全边界

- 工具只读取设备枚举信息，不改变 COM 号、驱动、设备管理器树、USB 固件或 ALSA 配置。
- Windows 11 智能应用控制可能拦截未签名的 exe。不要为本工具关闭 SAC；使用 `usbTree.vbs`/`usbTree.cmd`/`usbTree-cli.cmd`，或使用可信代码签名证书重新构建。若组织策略同时拦截脚本，需要申请策略例外或使用已签名 exe。
- 免 exe 启动器依赖 64 位 Windows PowerShell 5.1（系统自带）和 `usbTree.dll`；不要把 dll、ps1 和启动脚本分开复制。文件来自下载压缩包时先 `Unblock-File`。
- Windows 默认只列出活动音频端点；排查历史设备时使用 `--include-disabled`。禁用/未连接端点可能很多。
- 剪贴板复制需要与工具处于同一个 Windows 用户会话；远程桌面、服务会话或无交互桌面可能无法访问剪贴板，此时改用 `--out` 导出文件。Linux 的 `--copy` 还要求安装 `wl-copy`、`xclip`、`xsel` 或 `pbcopy`。
- 终端显示中文需要终端代码页支持 UTF-8；Windows Terminal、PowerShell 7 通常直接支持，旧版控制台可先执行 `chcp 65001`。
- 板卡层只对已知 ListenAI CH344+VCOM 组合做合并；其他 USB 设备即使共享上游 Hub 也保持独立。共享上游路径表示 USB 拓扑关系，不能单独证明两个设备属于同一产品外壳；必要时还要结合 VID/PID、序列号、父设备和实际硬件确认。
- 音频端点与板卡的关联是启发式的：Container ID 最可靠，USB Key/VID-PID 是兜底。多块同型号板卡同时连接时，应以板卡声卡列表中的 DeviceKey 和所属板卡为准再复核。
- Linux 版本需要目标主机有 Python 3，并且当前用户能读取 `/sys/class/tty` 和 `/proc/asound`；串口/声卡权限仍由 Linux 设备组和 udev/ALSA 规则决定。Linux 通道数解析是尽力而为，未实测硬件时不要宣称已验证。
- 不要在 Skill 中保存 API 密钥、串口密码或其他凭据。
