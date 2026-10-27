#!/usr/bin/env python3
"""USB serial and audio endpoint viewer for Linux. Uses only the standard library."""

import argparse
import glob
import hashlib
import os
import re
import shutil
import subprocess
import sys
import time
import unicodedata
from collections import Counter, defaultdict


def read_text(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return handle.read().strip()
    except (OSError, IOError):
        return ""


def read_lines(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return handle.readlines()
    except (OSError, IOError):
        return []


def find_usb_device(device_path):
    current = os.path.realpath(device_path)
    while current and current != os.path.dirname(current):
        if os.path.exists(os.path.join(current, "idVendor")) and os.path.exists(
            os.path.join(current, "idProduct")
        ):
            return current
        current = os.path.dirname(current)
    return ""


def interface_number(device_path):
    current = os.path.realpath(device_path)
    while current and current != os.path.dirname(current):
        match = re.search(r":\d+\.(\d+)$", os.path.basename(current))
        if match:
            return int(match.group(1))
        current = os.path.dirname(current)
    return 999


def upstream_key(usb_path):
    name = os.path.basename(usb_path)
    if "." in name:
        return re.sub(r"\.\d+$", "", name)
    return os.path.basename(os.path.dirname(usb_path)) or name


def device_label(product, manufacturer, vendor, product_id):
    text = "%s %s" % (product, manufacturer)
    if "CH344" in text.upper() or "QUAD_SERIAL" in text.upper():
        return "CH344 Quad_Serial"
    if "LISTENAI" in text.upper():
        return "ListenAI VCOM"
    return product or "%s:%s" % (vendor, product_id)


def sanitize_token(value):
    token = re.sub(r"[^A-Za-z0-9]+", "_", value or "").strip("_").upper()
    match = re.match(r"^([A-Z0-9]{4,})_0_([A-Z0-9]{2,})$", token)
    if match:
        token = "%s_%s" % (match.group(1), match.group(2))
    return token


def usb_token(usb_path, serial):
    token = sanitize_token(serial)
    if token:
        return token
    token = sanitize_token(os.path.basename(usb_path))
    return token or "USB"


def collect_ports():
    paths = sorted(set(glob.glob("/sys/class/tty/ttyUSB*") + glob.glob("/sys/class/tty/ttyACM*")))
    ports = []
    for tty_path in paths:
        name = os.path.basename(tty_path)
        device_path = os.path.realpath(os.path.join(tty_path, "device"))
        usb_path = find_usb_device(device_path)
        if not usb_path:
            continue
        vendor = read_text(os.path.join(usb_path, "idVendor"))
        product_id = read_text(os.path.join(usb_path, "idProduct"))
        product = read_text(os.path.join(usb_path, "product"))
        manufacturer = read_text(os.path.join(usb_path, "manufacturer"))
        serial = read_text(os.path.join(usb_path, "serial"))
        ports.append(
            {
                "name": name,
                "device_path": device_path,
                "usb_path": usb_path,
                "usb_name": os.path.basename(usb_path),
                "board_key": upstream_key(usb_path),
                "device_key": usb_path,
                "vendor": vendor,
                "product_id": product_id,
                "product": product,
                "manufacturer": manufacturer,
                "serial": serial,
                "interface": interface_number(device_path),
                "label": device_label(product, manufacturer, vendor, product_id),
                "interface_label": "",
            }
        )
    return ports


def is_multi_serial(label):
    upper = label.upper()
    return any(token in upper for token in (
        "CH344", "CH348", "QUAD_SERIAL", "MULTI SERIAL"
    ))


def device_title(ports):
    label = ports[0]["label"]
    if "USB TO MULTI SERIAL" in label.upper():
        label = "USB To Multi Serial"
    return "%s  (%d 个串口)" % (label, len(ports))


def board_base_title(devices):
    labels = [ports[0]["label"] for ports in devices.values()]
    if any("LISTENAI" in label.upper() for label in labels):
        return "ListenAI 板卡"
    return "USB 板卡（共享上游连接）" if len(devices) > 1 else "USB 板卡"


def build_boards(ports):
    boards_map = {}
    for port in ports:
        board = boards_map.get(port["board_key"])
        if board is None:
            board = {
                "key": port["board_key"],
                "title": "",
                "devices": {},
                "audio": [],
            }
            boards_map[port["board_key"]] = board
        board["devices"].setdefault(port["device_key"], []).append(port)

    boards = list(boards_map.values())
    for board in boards:
        board["base_title"] = board_base_title(board["devices"])
        for device_ports in board["devices"].values():
            device_ports.sort(key=lambda item: (item["interface"], item["name"], item["device_path"]))
            if is_multi_serial(device_ports[0]["label"]):
                for index, port in enumerate(device_ports):
                    port["interface_label"] = chr(ord("A") + index)

    boards.sort(key=lambda item: (item["base_title"], item["key"]))
    totals = Counter(board["base_title"] for board in boards)
    indexes = defaultdict(int)
    for board in boards:
        indexes[board["base_title"]] += 1
        base = board["base_title"]
        board["title"] = (
            "%s #%d" % (base, indexes[base]) if totals[base] > 1 else base
        )
    return boards


def stream_channels(card, section):
    best = 0
    for path in glob.glob("/proc/asound/card%s/stream*" % card):
        current = None
        for line in read_lines(path):
            stripped = line.strip()
            if line and not line[0].isspace() and stripped.endswith(":"):
                if stripped in ("Playback:", "Capture:"):
                    current = stripped[:-1]
                else:
                    current = None
                continue
            if current == section and stripped.startswith("Channels:"):
                try:
                    best = max(best, int(stripped.split(":", 1)[1].strip()))
                except (IndexError, ValueError):
                    pass
    return best


def collect_audio():
    cards = []
    card_paths = sorted(
        glob.glob("/sys/class/sound/card[0-9]*"),
        key=lambda value: int(re.search(r"(\d+)$", os.path.basename(value)).group(1)),
    )
    for path in card_paths:
        match = re.search(r"(\d+)$", os.path.basename(path))
        if not match:
            continue
        device_link = os.path.join(path, "device")
        device_path = os.path.realpath(device_link) if os.path.exists(device_link) else ""
        cards.append((match.group(1), path, device_path))

    if not cards:
        for control in sorted(glob.glob("/dev/snd/controlC*")):
            match = re.search(r"(\d+)$", os.path.basename(control))
            if match:
                cards.append((match.group(1), "", ""))

    audio = []
    for card, card_path, device_path in cards:
        usb_path = find_usb_device(device_path) if device_path else ""
        vendor = read_text(os.path.join(usb_path, "idVendor")) if usb_path else ""
        product_id = read_text(os.path.join(usb_path, "idProduct")) if usb_path else ""
        product = read_text(os.path.join(usb_path, "product")) if usb_path else ""
        manufacturer = read_text(os.path.join(usb_path, "manufacturer")) if usb_path else ""
        serial = read_text(os.path.join(usb_path, "serial")) if usb_path else ""
        is_usb = bool(usb_path and vendor and product_id)
        vid_pid = (
            "VID_%s&PID_%s" % (vendor.upper(), product_id.upper()) if is_usb else ""
        )
        token = usb_token(usb_path, serial) if is_usb else ""
        board_key = upstream_key(usb_path) if is_usb else ""
        endpoint_id = "card%s" % card
        name = (
            read_text("/proc/asound/card%s/id" % card)
            or read_text(os.path.join(card_path, "id"))
            or endpoint_id
        )
        playback = stream_channels(card, "Playback")
        capture = stream_channels(card, "Capture")

        def make_item(direction, channels):
            if is_usb and vid_pid and token:
                device_key = "%s:%s" % (vid_pid, token)
            else:
                identity = (direction + ":" + endpoint_id).upper()
                digest = hashlib.sha256(identity.encode("utf-8")).hexdigest().upper()
                device_key = "AUDIO_" + digest[:20]
            return {
                "direction": direction,
                "state": "Active",
                "device_key": device_key,
                "channels": channels,
                "friendly_name": name,
                "endpoint_id": endpoint_id,
                "device_name": product or name,
                "endpoint_name": name,
                "interface_path": device_path or usb_path,
                "container_id": "",
                "parent": usb_path,
                "location_paths": usb_path,
                "is_usb": is_usb,
                "vid_pid": vid_pid,
                "usb_token": token,
                "board_key": board_key,
                "board_title": "",
            }

        if playback:
            audio.append(make_item("Render", playback))
        if capture:
            audio.append(make_item("Capture", capture))
        if not playback and not capture:
            audio.append(make_item("Unknown", 0))

    audio.sort(
        key=lambda item: (
            item["direction"].lower(),
            item["friendly_name"].lower(),
            item["device_key"].lower(),
        )
    )
    return audio


def attach_audio(boards, audio):
    by_key = {board["key"]: board for board in boards}
    for item in audio:
        board = by_key.get(item["board_key"])
        if board is None:
            continue
        board["audio"].append(item)
        item["board_title"] = board["title"]
    for board in boards:
        board["audio"].sort(
            key=lambda item: (
                item["direction"].lower(),
                item["friendly_name"].lower(),
                item["device_key"].lower(),
            )
        )


def port_title(port):
    label = port["interface_label"]
    return "%s  %s" % (label, port["name"]) if label else port["name"]


def channel_label(channels):
    return "%dch" % channels if channels else "?ch"


def channel_text(channels):
    return str(channels) if channels else "?"


def audio_line(item):
    return "%s  %s  %s  %s" % (
        item["direction"].ljust(7),
        channel_label(item["channels"]),
        item["device_key"],
        item["friendly_name"],
    )


def sorted_device_items(board):
    return sorted(
        board["devices"].items(),
        key=lambda pair: (pair[1][0]["label"], pair[0]),
    )


def render_tree(boards):
    if not boards:
        return "未发现当前连接的串口设备。"

    lines = []
    indent = "  " if len(boards) > 1 else ""
    if len(boards) > 1:
        lines.append("物理 USB 板卡（%d 个）" % len(boards))
    for board in boards:
        lines.append(indent + board["title"])
        device_indent = indent + "  "
        for _device_key, device_ports in sorted_device_items(board):
            lines.append(device_indent + device_title(device_ports))
            port_indent = device_indent + "  "
            for port in device_ports:
                lines.append(port_indent + port_title(port))
        if board["audio"]:
            lines.append(
                device_indent + "音频端点  (%d 个)" % len(board["audio"])
            )
            for item in board["audio"]:
                lines.append(device_indent + "  " + audio_line(item))
    return "\n".join(lines)


def render_plain(boards):
    lines = []
    for board in boards:
        for _device_key, device_ports in sorted_device_items(board):
            for port in device_ports:
                label = port["interface_label"]
                lines.append(
                    "%s\t%s" % (label, port["name"]) if label else port["name"]
                )
    return "\n".join(lines)


def count_ports(boards):
    return sum(
        len(device_ports)
        for board in boards
        for device_ports in board["devices"].values()
    )


def render_markdown(boards):
    lines = ["# USB 串口树", ""]
    lines.append(
        "共 %d 个串口。生成时间：%s" % (count_ports(boards), time.strftime("%Y-%m-%d %H:%M:%S"))
    )
    lines.append("")
    if not boards:
        lines.append("未发现当前连接的串口设备。")
        return "\n".join(lines)
    lines.append("| 板卡 | 设备功能 | 接口 | 串口 | 显示名称 |")
    lines.append("| --- | --- | --- | --- | --- |")
    for board in boards:
        for _device_key, device_ports in sorted_device_items(board):
            title = device_title(device_ports)
            for port in device_ports:
                lines.append(
                    "| %s | %s | %s | %s | %s |"
                    % (
                        markdown_cell(board["title"]),
                        markdown_cell(title),
                        port["interface_label"] or "-",
                        port["name"],
                        markdown_cell(port["product"] or port["name"]),
                    )
                )
    return "\n".join(lines)


def render_csv(boards):
    lines = ["板卡,设备功能,接口,串口,显示名称,实例ID,父设备,ContainerID,位置路径"]
    for board in boards:
        for _device_key, device_ports in sorted_device_items(board):
            title = device_title(device_ports)
            for port in device_ports:
                lines.append(
                    ",".join(
                        csv_cell(value)
                        for value in (
                            board["title"],
                            title,
                            port["interface_label"],
                            port["name"],
                            port["product"] or port["name"],
                            port["device_path"],
                            port["usb_path"],
                            "",
                            port["usb_path"],
                        )
                    )
                )
    return "\n".join(lines)


def render_detail(boards):
    lines = ["USB 串口详细信息（%d 个串口）" % count_ports(boards)]
    lines.append("生成时间: %s" % time.strftime("%Y-%m-%d %H:%M:%S"))
    lines.append("")
    if not boards:
        lines.append("未发现当前连接的串口设备。")
        return "\n".join(lines)

    index = 1
    for board in boards:
        for _device_key, device_ports in sorted_device_items(board):
            title = device_title(device_ports)
            for port in device_ports:
                lines.append(
                    "[%d] %s / %s / %s"
                    % (index, board["title"], title, port_title(port))
                )
                lines.extend(port_detail(port))
                index += 1
        for item in board["audio"]:
            lines.append(
                "[%d] %s / 音频端点 / %s"
                % (index, board["title"], audio_line(item))
            )
            lines.extend(audio_detail(item))
            index += 1
    return "\n".join(lines)


def port_detail(port):
    lines = [
        "串口详细信息",
        "=" * 72,
        "串口:        %s" % port["name"],
        "显示名称:    %s" % (port["product"] or port["name"]),
        "设备描述:    %s" % (port["product"] or port["name"]),
        "制造商:      %s" % port["manufacturer"],
        "实例 ID:     %s" % port["device_path"],
        "父设备:      %s" % port["usb_path"],
        "父设备名称:  %s" % (port["product"] or port["usb_name"]),
        "Container ID: ",
        "总线描述:    USB",
        "硬件 ID:     %s:%s" % (port["vendor"], port["product_id"]),
        "位置路径:",
        port["usb_path"],
    ]
    return lines


def display_width(value):
    width = 0
    for char in value:
        width += 2 if unicodedata.east_asian_width(char) in ("W", "F") else 1
    return width


def pad(value, width):
    value = value or ""
    return value + " " * max(0, width - display_width(value))


def render_audio_table(audio, include_board=True):
    if not audio:
        return "未发现活动音频端点。"
    direction_width = 9
    state_width = 7
    key_width = 9
    friendly_width = 12
    for item in audio:
        direction_width = max(direction_width, display_width(item["direction"]))
        state_width = max(state_width, display_width(item["state"]))
        key_width = max(key_width, display_width(item["device_key"]))
        friendly_width = max(friendly_width, display_width(item["friendly_name"]))

    lines = [
        "%s  %s  %s  %s  %s%s"
        % (
            pad("Direction", direction_width),
            pad("State", state_width),
            pad("DeviceKey", key_width),
            pad("Ch", 2),
            pad("FriendlyName", friendly_width),
            "  Board" if include_board else "",
        )
    ]
    for item in audio:
        line = "%s  %s  %s  %s  %s" % (
            pad(item["direction"], direction_width),
            pad(item["state"], state_width),
            pad(item["device_key"], key_width),
            pad(channel_text(item["channels"]), 2),
            pad(item["friendly_name"], friendly_width),
        )
        if include_board:
            line += "  " + item["board_title"]
        lines.append(line)
    return "\n".join(lines)


def render_audio(audio, fmt):
    if fmt == "plain":
        return "\n".join(
            "%s\t%s\t%s\t%s\t%s"
            % (
                item["direction"],
                item["device_key"],
                channel_text(item["channels"]),
                item["friendly_name"],
                item["board_title"],
            )
            for item in audio
        )
    if fmt == "md":
        return render_audio_markdown(audio)
    if fmt == "csv":
        return render_audio_csv(audio)
    if fmt == "detail":
        return render_audio_detail(audio)
    return render_audio_table(audio)


def render_audio_markdown(audio):
    lines = ["# 声卡列表", ""]
    if not audio:
        lines.append("未发现活动音频端点。")
        return "\n".join(lines)
    lines.append("| Direction | State | DeviceKey | Ch | FriendlyName | Board |")
    lines.append("| --- | --- | --- | --- | --- | --- |")
    for item in audio:
        lines.append(
            "| %s | %s | %s | %s | %s | %s |"
            % (
                markdown_cell(item["direction"]),
                markdown_cell(item["state"]),
                markdown_cell(item["device_key"]),
                channel_text(item["channels"]),
                markdown_cell(item["friendly_name"]),
                markdown_cell(item["board_title"]),
            )
        )
    return "\n".join(lines)


def render_audio_csv(audio):
    lines = ["Direction,State,DeviceKey,Channels,FriendlyName,EndpointId,Board"]
    for item in audio:
        lines.append(
            ",".join(
                csv_cell(value)
                for value in (
                    item["direction"],
                    item["state"],
                    item["device_key"],
                    channel_text(item["channels"]),
                    item["friendly_name"],
                    item["endpoint_id"],
                    item["board_title"],
                )
            )
        )
    return "\n".join(lines)


def render_audio_detail(audio):
    lines = ["音频端点详细信息（%d 个端点）" % len(audio)]
    if not audio:
        lines.append("未发现活动音频端点。")
        return "\n".join(lines)
    for index, item in enumerate(audio, 1):
        lines.append(
            "[%d] %s / %s" % (index, item["direction"], item["friendly_name"])
        )
        lines.extend(audio_detail(item))
    return "\n".join(lines)


def audio_detail(item):
    return [
        "音频端点详细信息",
        "=" * 72,
        "方向:        %s" % item["direction"],
        "状态:        %s" % item["state"],
        "DeviceKey:   %s" % item["device_key"],
        "通道数:      %s" % channel_text(item["channels"]),
        "显示名称:    %s" % item["friendly_name"],
        "端点名称:    %s" % item["endpoint_name"],
        "设备名称:    %s" % item["device_name"],
        "端点 ID:     %s" % item["endpoint_id"],
        "Container ID: %s" % item["container_id"],
        "父设备:      %s" % item["parent"],
        "接口路径:    %s" % item["interface_path"],
        "位置路径:",
        item["location_paths"],
        "所属板卡:    %s" % item["board_title"],
    ]


def markdown_cell(value):
    if value is None:
        return ""
    return value.replace("|", "\\|").replace("\r", " ").replace("\n", " ").strip()


def csv_cell(value):
    if value is None:
        return ""
    text = str(value).replace("\r\n", " ").replace("\r", " ").replace("\n", " ").replace("\x00", " ").strip()
    if "," in text or '"' in text:
        return '"' + text.replace('"', '""') + '"'
    return text


def build_text(boards, audio, audio_only, all_mode, fmt):
    if all_mode:
        usb = render_tree(boards)
        sound = "声卡列表（%d 个端点）\n%s" % (
            len(audio),
            render_audio_table(audio),
        )
        return usb.rstrip() + "\n\n" + sound.rstrip()
    if audio_only:
        return render_audio(audio, fmt)
    if fmt == "plain":
        return render_plain(boards)
    if fmt == "md":
        return render_markdown(boards)
    if fmt == "csv":
        return render_csv(boards)
    if fmt == "detail":
        return render_detail(boards)
    return render_tree(boards)


def copy_to_clipboard(text):
    candidates = []
    if shutil.which("wl-copy"):
        candidates.append(["wl-copy"])
    if shutil.which("xclip"):
        candidates.append(["xclip", "-selection", "clipboard"])
    if shutil.which("xsel"):
        candidates.append(["xsel", "--clipboard", "--input"])
    if shutil.which("pbcopy"):
        candidates.append(["pbcopy"])
    for command in candidates:
        try:
            result = subprocess.run(
                command,
                input=text.encode("utf-8"),
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=5,
            )
            if result.returncode == 0:
                return True
        except (OSError, subprocess.SubprocessError):
            continue
    return False


def write_text_file(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text.rstrip() + "\n")


def print_help():
    print(
        """usbTree Linux USB 串口与声卡信息工具（只读）

用法:
  usbTree-linux [选项]

选项:
  -a, --audio             只输出声卡列表
      --all               输出 USB 设备树 + 声卡列表
  -f, --format <格式>     tree|plain|md|csv|detail|audio|all，默认 tree
  -o, --out <文件>        同时写入 UTF-8 文件
  -c, --copy              同时复制到剪贴板（需要 wl-copy/xclip/xsel）
      --include-disabled  兼容参数；ALSA 没有 Windows 的活动/禁用状态
      --once              只输出一次
      --watch             持续刷新（默认无参数时）
      --interval <秒>     刷新间隔，默认 2 秒
      --no-clear          刷新时不清屏
  -l, --list              输出到终端（默认行为）
  -h, --help              显示本帮助

格式说明:
  tree    与 Windows 版一致的板卡/设备/接口树，USB 音频合并到板卡
  plain   每行一个串口，带 A/B/C/D 接口字母
  md      Markdown 表格
  csv     CSV 表格
  detail  每个串口和音频端点的完整信息
  audio   等同 --audio，输出声卡表
  all     等同 --all，输出 USB 树 + 声卡表

示例:
  usbTree-linux --once
  usbTree-linux --format plain
  usbTree-linux --audio
  usbTree-linux --audio --format csv --out audio.csv
  usbTree-linux --all --out device.txt
  usbTree-linux --watch --interval 5
"""
    )


def main():
    parser = argparse.ArgumentParser(add_help=False, description="按 USB 板卡关系显示 Linux 串口和声卡")
    parser.add_argument("-a", "--audio", action="store_true", help="只输出声卡列表")
    parser.add_argument("--all", action="store_true", help="输出 USB 树和声卡列表")
    parser.add_argument("-f", "--format", choices=["tree", "plain", "md", "csv", "detail", "audio", "all"], default=None, help="输出格式")
    parser.add_argument("-o", "--out", help="同时写入 UTF-8 文件")
    parser.add_argument("-c", "--copy", action="store_true", help="同时复制到剪贴板")
    parser.add_argument("--include-disabled", action="store_true", help="兼容参数")
    parser.add_argument("--once", action="store_true", help="只输出一次")
    parser.add_argument("--watch", action="store_true", help="持续刷新")
    parser.add_argument("--interval", type=float, default=2.0, help="刷新间隔，默认 2 秒")
    parser.add_argument("--no-clear", action="store_true", help="刷新时不清屏")
    parser.add_argument("-l", "--list", action="store_true", help="输出到终端")
    parser.add_argument("-h", "--help", action="store_true", help="显示帮助")
    args = parser.parse_args()

    if args.help:
        print_help()
        return 0

    fmt = args.format or "tree"
    if fmt == "audio":
        args.audio = True
        fmt = "tree"
    elif fmt == "all":
        args.all = True
        fmt = "tree"
    if args.all:
        args.audio = False

    new_mode = (
        args.audio
        or args.all
        or args.out
        or args.copy
        or args.list
        or args.format is not None
    )
    one_shot = args.once or new_mode
    if args.watch:
        one_shot = False
    interval = max(0.2, args.interval)

    while True:
        try:
            ports = collect_ports()
            boards = build_boards(ports)
            audio = collect_audio()
            attach_audio(boards, audio)
            text = build_text(boards, audio, args.audio, args.all, fmt)

            if not one_shot and not args.no_clear and sys.stdout.isatty():
                sys.stdout.write("\033[2J\033[H")
            sys.stdout.write(text.rstrip() + "\n")
            sys.stdout.flush()

            if args.out:
                try:
                    write_text_file(args.out, text)
                    print("已写入: %s" % os.path.abspath(args.out), file=sys.stderr)
                except OSError as exc:
                    print("写入失败: %s" % exc, file=sys.stderr)
                    return 1

            if args.copy:
                if copy_to_clipboard(text):
                    print("已复制到剪贴板。", file=sys.stderr)
                else:
                    print("复制失败：未找到 wl-copy、xclip、xsel 或 pbcopy。", file=sys.stderr)
                    return 1

            if one_shot:
                return 0
            time.sleep(interval)
        except KeyboardInterrupt:
            return 0
        except Exception as exc:
            print("运行失败: %s" % exc, file=sys.stderr)
            return 1


if __name__ == "__main__":
    sys.exit(main())
