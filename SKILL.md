---
name: usb-tree
description: 按物理 USB 上游连接、PnP 父设备和 Container ID 自动树状整理 Windows COM 串口及 Linux ttyUSB/ttyACM 串口；当用户需要确认多个 USB 串口是否来自同一块板卡、查看 CH344 A/B/C/D 接口映射、运行 usbTree 工具或生成/重建跨平台版本时使用。
---

# usbTree USB 串口树状工具

## 概述

这个 Skill 用于运行和维护 `usbTree` 工具。工具把“同一块板卡通过 USB Hub 暴露出的多个 USB 设备”整理成以下层级：

```text
物理 USB 板卡
├─ 设备功能
│  ├─ A  COMx / ttyUSBx
│  ├─ B  COMx / ttyUSBx
│  ├─ C  COMx / ttyUSBx
│  └─ D  COMx / ttyUSBx
└─ 另一项设备功能
   └─ COMx / ttyACMx
```

它只做设备发现和可视化，不修改 Windows PnP、COM 编号、驱动、注册表、USB 描述符或 Linux 网络配置。

## 触发场景

出现以下需求时使用本 Skill：

- 查看 USB 串口，并按板卡而不是只按 COM 编号排列。
- 判断 CH344、ListenAI VCOM 或其他多个串口是否来自同一根 USB 连接/同一块板卡。
- 需要按 CH344 的 `SERIAL-A`、`SERIAL-B`、`SERIAL-C`、`SERIAL-D` 顺序显示 COM 号。
- 在 Windows 运行 `usbTree.exe`，或在 Linux 查看 `ttyUSB*`、`ttyACM*`。
- 修改工具的分组、接口排序、名称、图标或重新编译跨平台执行文件。

## 工作流程

### Windows

可执行工具位于本 Skill 的 `scripts` 目录。直接启动：

```powershell
$skillDir = 'E:\aswxStation\tmpskill\102002usbTre'
& "$skillDir\scripts\usbTree.exe"
```

左侧显示“物理 USB 板卡 → 设备功能 → 串口”，右侧显示实例 ID、父设备、Container ID、硬件 ID 和位置路径。工具默认每 2 秒刷新，也支持手动刷新。

CH344 端口按接口名称排序。例如当前设备的映射为：

```text
A  COM13
B  COM15
C  COM18
D  COM12
```

ListenAI VCOM 显示为单独的设备功能，例如 `COM14`。如果电脑上存在多个相同名称的板卡，工具会自动添加 `#1`、`#2` 编号。

### Linux

Linux 版本使用 Python 标准库读取 `/sys/class/tty` 和 USB sysfs，不需要 `pyudev`：

```bash
cd /path/to/102002usbTre/scripts
chmod +x usbTree-linux usbTree-linux.py
./usbTree-linux
```

只输出一次：

```bash
./usbTree-linux --once
```

它枚举 `ttyUSB*` 和 `ttyACM*`，根据 USB 上游路径归组，并按照设备接口顺序给 CH344 端口标记 A、B、C、D。Linux 目录中提供的是可直接执行的 Python 脚本和启动器，不是预编译 ELF 二进制。

### 重新构建 Windows 版本

Windows 主机自带 .NET Framework 编译器时，可以在 PowerShell 中执行：

```powershell
$skillDir = 'E:\aswxStation\tmpskill\102002usbTre'
Set-Location "$skillDir\scripts"
& .\make-icon.ps1
& .\build.ps1
```

构建结果为 `scripts\usbTree.exe`，源码来自 `references\UsbSerialTreeViewer.cs`，图标来自 `assets\usbTree.ico`。

## 分组规则

Windows 端优先使用以下信息：

1. 物理板卡层：串口位置路径去掉最后的设备接口节点后形成的共享 USB 上游路径。
2. 设备功能层：PnP `Container ID`，缺失时使用父设备实例，再缺失时使用串口实例 ID。
3. 串口层：FriendlyName 中的 `SERIAL-A/B/C/D`；没有字母时按 COM 号排序。

因此，工具可以把一个 USB Hub 后面的 CH344 和 ListenAI VCOM 可视化放在同一块板卡下，但不会伪造一个 Windows PnP 父设备。设备管理器仍会按 USB 设备描述符、设备地址和驱动关系分别枚举它们。

## 资源

- `scripts/usbTree.exe`：Windows 只读 WinForms 工具。
- `scripts/usbTree-linux`：Linux 启动器。
- `scripts/usbTree-linux.py`：Linux 标准库实现。
- `scripts/build.ps1`：Windows 构建脚本。
- `scripts/make-icon.ps1`：生成 USB 树状图标。
- `references/UsbSerialTreeViewer.cs`：Windows 源码。
- `assets/usbTree.ico`：Windows EXE 图标。

## 限制与安全边界

- 工具只读取设备枚举信息，不改变 COM 号、驱动、设备管理器树或 USB 固件。
- 共享上游路径表示 USB 拓扑关系，不能单独证明两个设备一定属于同一产品外壳；必要时还要结合 VID/PID、序列号、父设备和实际硬件确认。
- Linux 版本需要目标主机有 Python 3，并且当前用户能读取 `/sys/class/tty`；串口打开权限仍由 Linux 设备组和 udev 规则决定。
- 不要在 Skill 中保存 API 密钥、串口密码或其他凭据。
