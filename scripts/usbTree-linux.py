#!/usr/bin/env python3
"""USB serial board tree viewer for Linux. Uses only the Python standard library."""

import argparse
import glob
import os
import re
import sys
import time
from collections import defaultdict


def read_text(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return handle.read().strip()
    except (OSError, IOError):
        return ""


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
            }
        )
    return ports


def port_title(port, index, is_quad):
    if is_quad:
        return "%s  %s" % (chr(ord("A") + index), port["name"])
    return port["name"]


def board_title(devices):
    if any(device["label"] == "ListenAI VCOM" for device in devices.values()):
        return "ListenAI 板卡"
    return "USB 板卡（共享上游连接）" if len(devices) > 1 else "USB 板卡"


def render(ports):
    boards = defaultdict(lambda: defaultdict(list))
    for port in ports:
        boards[port["board_key"]][port["device_key"]].append(port)
    if not boards:
        return "USB 串口设备\n└─ 未发现当前连接的 USB 串口。"

    board_items = sorted(boards.items(), key=lambda item: board_title(item[1]))
    lines = []
    for board_index, (_board_key, devices) in enumerate(board_items):
        if len(board_items) > 1:
            lines.append("物理 USB 板卡：%s" % board_title(devices))
        else:
            lines.append(board_title(devices))
        device_items = sorted(devices.values(), key=lambda value: value[0]["label"])
        for device_index, device_ports in enumerate(device_items):
            label = device_ports[0]["label"]
            device_last = device_index == len(device_items) - 1
            device_branch = "└─" if device_last else "├─"
            lines.append("%s %s" % (device_branch, label))
            device_ports.sort(key=lambda port: (port["interface"], port["name"]))
            is_quad = "CH344" in label or "QUAD_SERIAL" in label.upper()
            indent = "   " if device_last else "│  "
            for port_index, port in enumerate(device_ports):
                port_branch = "└─" if port_index == len(device_ports) - 1 else "├─"
                lines.append("%s%s %s" % (indent, port_branch, port_title(port, port_index, is_quad)))
        if board_index != len(board_items) - 1:
            lines.append("")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="按 USB 板卡关系显示 Linux 串口")
    parser.add_argument("--once", action="store_true", help="只显示一次")
    parser.add_argument("--interval", type=float, default=2.0, help="自动刷新间隔，默认 2 秒")
    parser.add_argument("--no-clear", action="store_true", help="刷新时不清屏")
    args = parser.parse_args()
    try:
        while True:
            output = render(collect_ports())
            if not args.once and not args.no_clear and sys.stdout.isatty():
                sys.stdout.write("\033[2J\033[H")
            print(output)
            sys.stdout.flush()
            if args.once:
                return 0
            time.sleep(max(0.2, args.interval))
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    sys.exit(main())
