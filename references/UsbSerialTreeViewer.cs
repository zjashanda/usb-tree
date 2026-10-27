using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class NativeMethods
{
    internal const uint DIGCF_PRESENT = 0x00000002;
    internal const uint DIGCF_ALLCLASSES = 0x00000004;
    internal const uint DIGCF_CLASS = 0x00000020;

    internal const uint SPDRP_DEVICEDESC = 0x00000000;
    internal const uint SPDRP_HARDWAREID = 0x00000001;
    internal const uint SPDRP_MFG = 0x0000000B;
    internal const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    internal const uint SPDRP_CLASS = 0x00000009;

    internal const uint DEVPROP_TYPE_GUID = 0x0000000D;
    internal const uint DEVPROP_TYPEMOD_LIST = 0x00001000;

    internal static readonly Guid PortsClassGuid = new Guid("4D36E978-E325-11CE-BFC1-08002BE10318");
    internal static readonly Guid AudioEndpointClassGuid = new Guid("C166523C-FE0C-4A94-A586-F1A80CFBBF3E");
    internal static readonly DevPropKey DeviceParent = new DevPropKey(
        new Guid("4340A6C5-93FA-4706-972C-7B648008A5A7"), 8);
    internal static readonly DevPropKey DeviceContainerId = new DevPropKey(
        new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);
    internal static readonly DevPropKey DeviceLocationPaths = new DevPropKey(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 37);
    internal static readonly DevPropKey BusReportedDeviceDesc = new DevPropKey(
        new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 4);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DevPropKey
    {
        internal Guid FmtId;
        internal uint Pid;

        internal DevPropKey(Guid fmtId, uint pid)
        {
            FmtId = fmtId;
            Pid = pid;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDevInfoData
    {
        internal int CbSize;
        internal Guid ClassGuid;
        internal uint DevInst;
        internal IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid, string enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "SetupDiGetDeviceInstanceIdW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData,
        StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "SetupDiGetDeviceRegistryPropertyW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData, uint property,
        out uint propertyRegDataType, IntPtr propertyBuffer, uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "SetupDiGetDevicePropertyW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceProperty(
        IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData, ref DevPropKey propertyKey,
        out uint propertyType, IntPtr propertyBuffer, uint propertyBufferSize,
        out uint requiredSize, uint flags);
}

internal sealed class DeviceRecord
{
    public string InstanceId;
    public string FriendlyName;
    public string Description;
    public string Manufacturer;
    public string ClassName;
}

internal sealed class PortInfo
{
    public string ComName;
    public string FriendlyName;
    public string InstanceId;
    public string Parent;
    public string ParentName;
    public string ContainerId;
    public string LocationPaths;
    public string HardwareIds;
    public string BusDescription;
    public string Manufacturer;
    public string DeviceDescription;
}

internal sealed class AudioPnpInfo
{
    public string EndpointGuid;
    public string FriendlyName;
    public string ContainerId;
    public string Parent;
    public string LocationPaths;
}

internal sealed class AudioEndpointInfo
{
    public string Direction;
    public string State;
    public string DeviceKey;
    public int Channels;
    public string FriendlyName;
    public string EndpointId;
    public string DeviceName;
    public string EndpointName;
    public string InterfacePath;
    public string ContainerId;
    public string Parent;
    public string LocationPaths;
    public bool IsUsb;
    public string VidPid;
    public string UsbToken;
    public string BoardTitle;

    public string ChannelText
    {
        get { return Channels > 0 ? Channels.ToString() : "?"; }
    }

    public string ChannelLabel
    {
        get { return Channels > 0 ? Channels + "ch" : "?ch"; }
    }
}

internal sealed class PortGroup
{
    public string Key;
    public string Parent;
    public string ParentName;
    public string ContainerId;
    public string BusDescription;
    public string DeviceLabel;
    public List<PortInfo> Ports = new List<PortInfo>();

    public string Title
    {
        get
        {
            string name = string.IsNullOrEmpty(DeviceLabel) ? "USB 设备" : DeviceLabel;
            return string.Format("{0}  ({1} 个串口)", name, Ports.Count);
        }
    }
}

internal sealed class BoardGroup
{
    public string Key;
    public string Title;
    public List<PortGroup> Devices = new List<PortGroup>();
    public List<AudioEndpointInfo> Audio = new List<AudioEndpointInfo>();
}

internal static class UsbSerialReader
{
    private static readonly Regex ComRegex = new Regex(
        @"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<PortInfo> ReadPorts()
    {
        Dictionary<string, DeviceRecord> allDevices = ReadAllDevices();
        List<PortInfo> result = new List<PortInfo>();
        Guid portsClassGuid = NativeMethods.PortsClassGuid;
        IntPtr infoSet = NativeMethods.SetupDiGetClassDevs(
            ref portsClassGuid, null, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_CLASS);
        if (infoSet == IntPtr.Zero || infoSet == new IntPtr(-1))
        {
            return result;
        }

        try
        {
            uint index = 0;
            while (true)
            {
                NativeMethods.SpDevInfoData data = new NativeMethods.SpDevInfoData();
                data.CbSize = Marshal.SizeOf(typeof(NativeMethods.SpDevInfoData));
                if (!NativeMethods.SetupDiEnumDeviceInfo(infoSet, index++, ref data))
                {
                    break;
                }

                string instanceId = GetInstanceId(infoSet, ref data);
                string friendly = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_FRIENDLYNAME);
                string description = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_DEVICEDESC);
                string manufacturer = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_MFG);
                string hardwareIds = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_HARDWAREID);
                string display = string.IsNullOrEmpty(friendly) ? description : friendly;
                Match match = ComRegex.Match(display ?? string.Empty);
                if (!match.Success)
                {
                    match = Regex.Match(instanceId ?? string.Empty, @"(COM\d+)", RegexOptions.IgnoreCase);
                }
                if (!match.Success)
                {
                    continue;
                }

                string parent = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceParent);
                string containerId = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceContainerId);
                string locationPaths = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceLocationPaths);
                string busDescription = GetDeviceProperty(infoSet, ref data, NativeMethods.BusReportedDeviceDesc);
                DeviceRecord parentRecord;
                string parentName = string.Empty;
                if (!string.IsNullOrEmpty(parent) && allDevices.TryGetValue(parent, out parentRecord))
                {
                    parentName = string.IsNullOrEmpty(parentRecord.FriendlyName)
                        ? parentRecord.Description : parentRecord.FriendlyName;
                }

                result.Add(new PortInfo
                {
                    ComName = match.Groups[1].Value.ToUpperInvariant(),
                    FriendlyName = display,
                    InstanceId = instanceId,
                    Parent = parent,
                    ParentName = parentName,
                    ContainerId = containerId,
                    LocationPaths = locationPaths,
                    HardwareIds = hardwareIds,
                    BusDescription = busDescription,
                    Manufacturer = manufacturer,
                    DeviceDescription = description
                });
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(infoSet);
        }

        result.Sort(delegate(PortInfo left, PortInfo right)
        {
            int numberLeft = PortNumber(left.ComName);
            int numberRight = PortNumber(right.ComName);
            return numberLeft.CompareTo(numberRight);
        });
        return result;
    }

    private static int PortNumber(string comName)
    {
        int number;
        return int.TryParse(comName.Substring(3), out number) ? number : int.MaxValue;
    }

    private static Dictionary<string, DeviceRecord> ReadAllDevices()
    {
        Dictionary<string, DeviceRecord> devices =
            new Dictionary<string, DeviceRecord>(StringComparer.OrdinalIgnoreCase);
        IntPtr infoSet = NativeMethods.SetupDiGetClassDevs(
            IntPtr.Zero, null, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_ALLCLASSES);
        if (infoSet == IntPtr.Zero || infoSet == new IntPtr(-1))
        {
            return devices;
        }

        try
        {
            uint index = 0;
            while (true)
            {
                NativeMethods.SpDevInfoData data = new NativeMethods.SpDevInfoData();
                data.CbSize = Marshal.SizeOf(typeof(NativeMethods.SpDevInfoData));
                if (!NativeMethods.SetupDiEnumDeviceInfo(infoSet, index++, ref data))
                {
                    break;
                }
                string instanceId = GetInstanceId(infoSet, ref data);
                if (string.IsNullOrEmpty(instanceId))
                {
                    continue;
                }
                devices[instanceId] = new DeviceRecord
                {
                    InstanceId = instanceId,
                    FriendlyName = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_FRIENDLYNAME),
                    Description = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_DEVICEDESC),
                    Manufacturer = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_MFG),
                    ClassName = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_CLASS)
                };
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(infoSet);
        }
        return devices;
    }

    private static string GetInstanceId(IntPtr infoSet, ref NativeMethods.SpDevInfoData data)
    {
        int required;
        StringBuilder value = new StringBuilder(512);
        if (!NativeMethods.SetupDiGetDeviceInstanceId(infoSet, ref data, value, value.Capacity, out required))
        {
            return string.Empty;
        }
        return value.ToString();
    }

    private static string GetRegistryProperty(
        IntPtr infoSet, ref NativeMethods.SpDevInfoData data, uint property)
    {
        uint type;
        uint required;
        NativeMethods.SetupDiGetDeviceRegistryProperty(
            infoSet, ref data, property, out type, IntPtr.Zero, 0, out required);
        if (required == 0)
        {
            return string.Empty;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            if (!NativeMethods.SetupDiGetDeviceRegistryProperty(
                infoSet, ref data, property, out type, buffer, required, out required))
            {
                return string.Empty;
            }
            byte[] bytes = new byte[required];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            string value = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            if (type == 7)
            {
                string[] parts = value.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join("; ", parts);
            }
            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetDeviceProperty(
        IntPtr infoSet, ref NativeMethods.SpDevInfoData data,
        NativeMethods.DevPropKey propertyKey)
    {
        uint type;
        uint required;
        NativeMethods.SetupDiGetDeviceProperty(
            infoSet, ref data, ref propertyKey, out type, IntPtr.Zero, 0, out required, 0);
        if (required == 0)
        {
            return string.Empty;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            if (!NativeMethods.SetupDiGetDeviceProperty(
                infoSet, ref data, ref propertyKey, out type, buffer, required, out required, 0))
            {
                return string.Empty;
            }
            byte[] bytes = new byte[required];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            uint baseType = type & 0x0FFF;
            if (baseType == NativeMethods.DEVPROP_TYPE_GUID && bytes.Length >= 16)
            {
                return new Guid(bytes).ToString("B").ToUpperInvariant();
            }

            string value = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            if ((type & NativeMethods.DEVPROP_TYPEMOD_LIST) != 0)
            {
                string[] parts = value.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(Environment.NewLine, parts);
            }
            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static Dictionary<string, AudioPnpInfo> ReadAudioPnp()
    {
        Dictionary<string, AudioPnpInfo> result =
            new Dictionary<string, AudioPnpInfo>(StringComparer.OrdinalIgnoreCase);
        Guid classGuid = NativeMethods.AudioEndpointClassGuid;
        IntPtr infoSet = NativeMethods.SetupDiGetClassDevs(
            ref classGuid, null, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_CLASS);
        if (infoSet == IntPtr.Zero || infoSet == new IntPtr(-1))
        {
            return result;
        }

        try
        {
            uint index = 0;
            while (true)
            {
                NativeMethods.SpDevInfoData data = new NativeMethods.SpDevInfoData();
                data.CbSize = Marshal.SizeOf(typeof(NativeMethods.SpDevInfoData));
                if (!NativeMethods.SetupDiEnumDeviceInfo(infoSet, index++, ref data))
                {
                    break;
                }

                string instanceId = GetInstanceId(infoSet, ref data);
                Match match = Regex.Match(instanceId ?? string.Empty, @"\{([0-9A-Fa-f-]{36})\}$");
                if (!match.Success)
                {
                    continue;
                }

                string endpointGuid = "{" + match.Groups[1].Value.ToUpperInvariant() + "}";
                result[endpointGuid] = new AudioPnpInfo
                {
                    EndpointGuid = endpointGuid,
                    FriendlyName = GetRegistryProperty(infoSet, ref data, NativeMethods.SPDRP_FRIENDLYNAME),
                    ContainerId = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceContainerId),
                    Parent = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceParent),
                    LocationPaths = GetDeviceProperty(infoSet, ref data, NativeMethods.DeviceLocationPaths)
                };
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(infoSet);
        }
        return result;
    }
}

internal static class PortGrouper
{
    public static List<BoardGroup> Build(List<PortInfo> ports)
    {
        Dictionary<string, PortGroup> groups = new Dictionary<string, PortGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (PortInfo port in ports)
        {
            string key = GroupKey(port);
            PortGroup group;
            if (!groups.TryGetValue(key, out group))
            {
                group = new PortGroup
                {
                    Key = key,
                    Parent = port.Parent,
                    ParentName = port.ParentName,
                    ContainerId = port.ContainerId,
                    BusDescription = port.BusDescription,
                    DeviceLabel = DeviceTitle(port)
                };
                groups.Add(key, group);
            }
            group.Ports.Add(port);
        }

        Dictionary<string, List<PortGroup>> buckets = new Dictionary<string, List<PortGroup>>(StringComparer.OrdinalIgnoreCase);
        foreach (PortGroup group in groups.Values)
        {
            string physicalKey = PhysicalKey(group.Ports[0]);
            List<PortGroup> bucket;
            if (!buckets.TryGetValue(physicalKey, out bucket))
            {
                bucket = new List<PortGroup>();
                buckets.Add(physicalKey, bucket);
            }
            bucket.Add(group);
        }

        List<BoardGroup> boards = new List<BoardGroup>();
        foreach (KeyValuePair<string, List<PortGroup>> pair in buckets)
        {
            if (TryCreateListenAiBoard(pair.Key, pair.Value, boards))
            {
                continue;
            }
            foreach (PortGroup group in pair.Value)
            {
                BoardGroup board = new BoardGroup { Key = "G:" + group.Key };
                board.Devices.Add(group);
                boards.Add(board);
            }
        }

        boards.Sort(delegate(BoardGroup left, BoardGroup right)
        {
            int compare = BoardTitle(left).CompareTo(BoardTitle(right));
            if (compare != 0)
            {
                return compare;
            }
            return string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);
        });
        AssignBoardTitles(boards);

        foreach (BoardGroup board in boards)
        {
            board.Devices.Sort(delegate(PortGroup left, PortGroup right)
            {
                int compare = left.Title.CompareTo(right.Title);
                if (compare != 0)
                {
                    return compare;
                }
                return string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);
            });
            foreach (PortGroup group in board.Devices)
            {
                group.Ports.Sort(delegate(PortInfo left, PortInfo right)
                {
                    int compare = InterfaceOrder(left).CompareTo(InterfaceOrder(right));
                    if (compare != 0)
                    {
                        return compare;
                    }
                    compare = ComNumber(left.ComName).CompareTo(ComNumber(right.ComName));
                    if (compare != 0)
                    {
                        return compare;
                    }
                    return string.Compare(left.InstanceId, right.InstanceId, StringComparison.OrdinalIgnoreCase);
                });
            }
        }
        return boards;
    }

    private static bool TryCreateListenAiBoard(
        string physicalKey, List<PortGroup> groups, List<BoardGroup> boards)
    {
        if (groups.Count != 2)
        {
            return false;
        }

        PortGroup listenAi = null;
        PortGroup ch344 = null;
        foreach (PortGroup group in groups)
        {
            if (listenAi == null && IsListenAi(group))
            {
                listenAi = group;
            }
            else if (ch344 == null && IsCh344(group))
            {
                ch344 = group;
            }
        }
        if (listenAi == null || ch344 == null)
        {
            return false;
        }

        BoardGroup board = new BoardGroup { Key = "B:" + physicalKey };
        board.Devices.Add(listenAi);
        board.Devices.Add(ch344);
        boards.Add(board);
        return true;
    }

    public static List<BoardGroup> Build(List<PortInfo> ports, List<AudioEndpointInfo> audio)
    {
        List<BoardGroup> boards = Build(ports);
        AudioEndpointMapper.Attach(boards, audio ?? new List<AudioEndpointInfo>());
        return boards;
    }

    public static int CountBoards(List<PortInfo> ports)
    {
        return Build(ports).Count;
    }

    private static void AssignBoardTitles(List<BoardGroup> boards)
    {
        Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (BoardGroup board in boards)
        {
            string baseTitle = BoardTitle(board);
            int count;
            totals.TryGetValue(baseTitle, out count);
            totals[baseTitle] = count + 1;
        }

        Dictionary<string, int> indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (BoardGroup board in boards)
        {
            string baseTitle = BoardTitle(board);
            int index;
            indexes.TryGetValue(baseTitle, out index);
            index++;
            indexes[baseTitle] = index;
            board.Title = totals[baseTitle] > 1
                ? string.Format("{0} #{1}", baseTitle, index)
                : baseTitle;
        }
    }

    internal static string GroupKey(PortInfo port)
    {
        if (!string.IsNullOrEmpty(port.Parent))
        {
            return "P:" + port.Parent;
        }
        if (!string.IsNullOrEmpty(port.ContainerId))
        {
            return "C:" + port.ContainerId;
        }
        return "I:" + port.InstanceId;
    }

    internal static string PhysicalKey(PortInfo port)
    {
        string paths = port.LocationPaths ?? string.Empty;
        if (string.IsNullOrEmpty(paths))
        {
            return GroupKey(port);
        }

        string[] lines = paths.Split(new[] { '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] parts = line.Trim().Split('#');
            int count = parts.Length;
            if (count > 0 && parts[count - 1].StartsWith("USBMI(", StringComparison.OrdinalIgnoreCase))
            {
                count--;
            }
            if (count > 0 && parts[count - 1].StartsWith("USB(", StringComparison.OrdinalIgnoreCase))
            {
                count--;
            }
            if (count > 0)
            {
                return string.Join("#", parts, 0, count);
            }
        }
        return GroupKey(port);
    }

    private static string DeviceTitle(PortInfo port)
    {
        string all = (port.FriendlyName ?? string.Empty) + " " + (port.BusDescription ?? string.Empty);
        if (all.IndexOf("CH344", StringComparison.OrdinalIgnoreCase) >= 0 ||
            all.IndexOf("Quad_Serial", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "CH344 Quad_Serial";
        }
        if (all.IndexOf("ListenAI", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "ListenAI VCOM";
        }
        if (all.IndexOf("USB To Multi Serial", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "USB To Multi Serial";
        }
        if (!string.IsNullOrEmpty(port.BusDescription))
        {
            return port.BusDescription;
        }
        return string.IsNullOrEmpty(port.ParentName) ? "USB 设备" : port.ParentName;
    }

    private static bool IsListenAi(PortGroup group)
    {
        if ((group.DeviceLabel ?? string.Empty).IndexOf("ListenAI", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        foreach (PortInfo port in group.Ports)
        {
            if ((port.InstanceId ?? string.Empty).IndexOf("VID_8765&PID_5678", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsCh344(PortGroup group)
    {
        if ((group.DeviceLabel ?? string.Empty).IndexOf("CH344", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        foreach (PortInfo port in group.Ports)
        {
            string ids = (port.InstanceId ?? string.Empty) + " " + (port.HardwareIds ?? string.Empty);
            if (ids.IndexOf("VID_1A86&PID_55D5", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static string BoardTitle(BoardGroup board)
    {
        foreach (PortGroup device in board.Devices)
        {
            if (IsListenAi(device))
            {
                return "ListenAI 板卡";
            }
        }
        return "USB 板卡";
    }

    internal static string InterfaceLabel(PortInfo port)
    {
        string name = port.FriendlyName ?? string.Empty;
        Match match = Regex.Match(name, @"SERIAL-([A-Z])", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(name, @"\bCh\s*([A-Z])\b", RegexOptions.IgnoreCase);
        }
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static int InterfaceOrder(PortInfo port)
    {
        string label = InterfaceLabel(port);
        if (label.Length == 1 && label[0] >= 'A' && label[0] <= 'Z')
        {
            return label[0] - 'A';
        }
        return 1000 + ComNumber(port.ComName);
    }

    internal static string PortTitle(PortInfo port)
    {
        string label = InterfaceLabel(port);
        return string.IsNullOrEmpty(label) ? port.ComName : label + "  " + port.ComName;
    }

    private static int ComNumber(string comName)
    {
        int number;
        return int.TryParse(comName.Substring(3), out number) ? number : int.MaxValue;
    }
}

internal static class AudioEndpointReader
{
    private const string MmDevicesRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio";
    private const string NameProperty = "{b3f8fa53-0004-438e-9003-51a46e139bfc},6";
    private const string EndpointNameProperty = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
    private const string InterfaceProperty = "{b3f8fa53-0004-438e-9003-51a46e139bfc},2";
    private const string ContainerProperty = "{8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c},2";
    private const string WaveFormatProperty = "{f19f064d-082c-4e27-bc73-6882a1bb8e4c},0";

    public static List<AudioEndpointInfo> ReadEndpoints(bool includeDisabled)
    {
        Dictionary<string, AudioPnpInfo> pnp = UsbSerialReader.ReadAudioPnp();
        List<AudioEndpointInfo> result = new List<AudioEndpointInfo>();
        AddDirection(result, pnp, "Render", includeDisabled);
        AddDirection(result, pnp, "Capture", includeDisabled);
        result.Sort(delegate(AudioEndpointInfo left, AudioEndpointInfo right)
        {
            int compare = string.Compare(left.Direction, right.Direction, StringComparison.OrdinalIgnoreCase);
            if (compare != 0)
            {
                return compare;
            }
            compare = string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.OrdinalIgnoreCase);
            if (compare != 0)
            {
                return compare;
            }
            return string.Compare(left.DeviceKey, right.DeviceKey, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private static void AddDirection(
        List<AudioEndpointInfo> result,
        Dictionary<string, AudioPnpInfo> pnp,
        string direction,
        bool includeDisabled)
    {
        using (RegistryKey root = Registry.LocalMachine.OpenSubKey(MmDevicesRoot + "\\" + direction))
        {
            if (root == null)
            {
                return;
            }

            foreach (string child in root.GetSubKeyNames())
            {
                using (RegistryKey key = root.OpenSubKey(child))
                {
                    if (key == null)
                    {
                        continue;
                    }

                    int state = ReadInt(key, "DeviceState", 0);
                    if (!includeDisabled && (state & 0x0F) != 1)
                    {
                        continue;
                    }

                    using (RegistryKey props = key.OpenSubKey("Properties"))
                    {
                        if (props == null)
                        {
                            continue;
                        }

                        string deviceName = ReadString(props, NameProperty);
                        string endpointName = ReadString(props, EndpointNameProperty);
                        string interfacePath = ReadString(props, InterfaceProperty);
                        string containerId = ReadGuid(props, ContainerProperty);
                        string endpointId = NormalizeGuid(child);
                        AudioPnpInfo pnpInfo;
                        if (pnp.TryGetValue(endpointId, out pnpInfo))
                        {
                            if (string.IsNullOrEmpty(containerId))
                            {
                                containerId = pnpInfo.ContainerId;
                            }
                        }

                        string friendlyName = pnpInfo == null ? string.Empty : pnpInfo.FriendlyName;
                        if (string.IsNullOrEmpty(friendlyName))
                        {
                            if (string.IsNullOrEmpty(endpointName))
                            {
                                friendlyName = string.IsNullOrEmpty(deviceName) ? endpointId : deviceName;
                            }
                            else
                            {
                                friendlyName = string.IsNullOrEmpty(deviceName)
                                    ? endpointName
                                    : endpointName + " (" + deviceName + ")";
                            }
                        }

                        string vidPid;
                        string token;
                        bool isUsb = ParseUsbIdentity(interfacePath, out vidPid, out token);
                        result.Add(new AudioEndpointInfo
                        {
                            Direction = direction,
                            State = StateLabel(state),
                            DeviceKey = BuildDeviceKey(direction, endpointId, vidPid, token),
                            Channels = ReadChannels(props, WaveFormatProperty),
                            FriendlyName = friendlyName,
                            EndpointId = endpointId,
                            DeviceName = deviceName,
                            EndpointName = endpointName,
                            InterfacePath = interfacePath,
                            ContainerId = containerId,
                            Parent = pnpInfo == null ? string.Empty : pnpInfo.Parent,
                            LocationPaths = pnpInfo == null ? string.Empty : pnpInfo.LocationPaths,
                            IsUsb = isUsb,
                            VidPid = vidPid,
                            UsbToken = token,
                            BoardTitle = string.Empty
                        });
                    }
                }
            }
        }
    }

    private static string ReadString(RegistryKey key, string name)
    {
        object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value as string ?? string.Empty;
    }

    private static int ReadInt(RegistryKey key, string name, int fallback)
    {
        object value = key.GetValue(name, null);
        if (value is int)
        {
            return (int)value;
        }
        if (value is uint)
        {
            return unchecked((int)(uint)value);
        }
        return fallback;
    }

    private static string ReadGuid(RegistryKey key, string name)
    {
        object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        byte[] bytes = value as byte[];
        if (bytes != null)
        {
            int offset = bytes.Length >= 24 ? 8 : 0;
            if (bytes.Length < offset + 16)
            {
                return string.Empty;
            }
            byte[] guidBytes = new byte[16];
            Array.Copy(bytes, offset, guidBytes, 0, 16);
            return new Guid(guidBytes).ToString("B").ToUpperInvariant();
        }

        string text = value as string;
        return string.IsNullOrEmpty(text) ? string.Empty : NormalizeGuid(text);
    }

    private static int ReadChannels(RegistryKey key, string name)
    {
        byte[] bytes = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[];
        if (bytes == null || bytes.Length < 12)
        {
            return -1;
        }

        int offset = (bytes.Length >= 8 &&
                      BitConverter.ToUInt32(bytes, 0) == 65 &&
                      BitConverter.ToUInt32(bytes, 4) == 1) ? 8 : 0;
        if (bytes.Length < offset + 4)
        {
            return -1;
        }

        int channels = BitConverter.ToUInt16(bytes, offset + 2);
        return channels > 0 ? channels : -1;
    }

    private static string StateLabel(int state)
    {
        switch (state & 0x0F)
        {
            case 1:
                return "Active";
            case 2:
                return "Disabled";
            case 4:
                return "NotPresent";
            case 8:
                return "Unplugged";
            default:
                return "Unknown";
        }
    }

    private static string NormalizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        value = value.Trim();
        if (!value.StartsWith("{", StringComparison.Ordinal))
        {
            value = "{" + value + "}";
        }
        return value.ToUpperInvariant();
    }

    private static bool ParseUsbIdentity(string interfacePath, out string vidPid, out string token)
    {
        vidPid = string.Empty;
        token = string.Empty;
        if (string.IsNullOrEmpty(interfacePath))
        {
            return false;
        }

        Match match = Regex.Match(interfacePath,
            @"^(?:\{\d+\}\.)?USB\\(?<Head>[^\\]+)\\(?<Tail>.+)$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        string head = match.Groups["Head"].Value;
        string tail = match.Groups["Tail"].Value;
        string candidate = Regex.Replace(head, @"&MI_[0-9A-F]{2}$", "", RegexOptions.IgnoreCase)
            .ToUpperInvariant();
        if (!Regex.IsMatch(candidate, @"^VID_[0-9A-F]{4}&PID_[0-9A-F]{4}$"))
        {
            return false;
        }

        string sanitized = Regex.Replace(tail, "[^A-Za-z0-9]+", "_").Trim('_').ToUpperInvariant();
        Match compact = Regex.Match(sanitized, @"^([A-Z0-9]{4,})_0_([A-Z0-9]{2,})$");
        if (compact.Success)
        {
            sanitized = compact.Groups[1].Value + "_" + compact.Groups[2].Value;
        }
        if (sanitized.Length == 0)
        {
            return false;
        }

        vidPid = candidate;
        token = sanitized;
        return true;
    }

    private static string BuildDeviceKey(string direction, string endpointId, string vidPid, string token)
    {
        if (!string.IsNullOrEmpty(vidPid) && !string.IsNullOrEmpty(token))
        {
            return vidPid + ":" + token;
        }

        string identity = (direction + ":" + endpointId).ToUpperInvariant();
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                hex.Append(value.ToString("X2"));
            }
            string text = hex.ToString();
            return "AUDIO_" + text.Substring(0, Math.Min(20, text.Length));
        }
    }
}

internal static class AudioEndpointMapper
{
    public static void Attach(List<BoardGroup> boards, List<AudioEndpointInfo> endpoints)
    {
        foreach (BoardGroup board in boards)
        {
            board.Audio.Clear();
        }
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            endpoint.BoardTitle = string.Empty;
        }

        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            BoardGroup board = FindBoard(boards, endpoint);
            if (board == null)
            {
                continue;
            }
            board.Audio.Add(endpoint);
            endpoint.BoardTitle = board.Title;
        }

        foreach (BoardGroup board in boards)
        {
            board.Audio.Sort(delegate(AudioEndpointInfo left, AudioEndpointInfo right)
            {
                int compare = string.Compare(left.Direction, right.Direction, StringComparison.OrdinalIgnoreCase);
                if (compare != 0)
                {
                    return compare;
                }
                compare = string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.OrdinalIgnoreCase);
                if (compare != 0)
                {
                    return compare;
                }
                return string.Compare(left.DeviceKey, right.DeviceKey, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    private static BoardGroup FindBoard(List<BoardGroup> boards, AudioEndpointInfo endpoint)
    {
        if (IsUsableContainer(endpoint.ContainerId))
        {
            string container = NormalizeGuid(endpoint.ContainerId);
            foreach (BoardGroup board in boards)
            {
                foreach (PortGroup group in board.Devices)
                {
                    foreach (PortInfo port in group.Ports)
                    {
                        if (IsUsableContainer(port.ContainerId) &&
                            NormalizeGuid(port.ContainerId) == container)
                        {
                            return board;
                        }
                    }
                }
            }
        }

        if (!endpoint.IsUsb)
        {
            return null;
        }

        string token = SignificantToken(endpoint.UsbToken);
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        foreach (BoardGroup board in boards)
        {
            foreach (PortGroup group in board.Devices)
            {
                foreach (PortInfo port in group.Ports)
                {
                    string instanceId = port.InstanceId ?? string.Empty;
                    if (instanceId.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (string.IsNullOrEmpty(endpoint.VidPid) ||
                         instanceId.IndexOf(endpoint.VidPid, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return board;
                    }
                }
            }
        }
        return null;
    }

    private static string SignificantToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return string.Empty;
        }

        string best = string.Empty;
        foreach (string part in token.Split('_'))
        {
            if (part.Length >= 6 && part.Length > best.Length)
            {
                best = part;
            }
        }
        return best;
    }

    private static string NormalizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        value = value.Trim();
        if (!value.StartsWith("{", StringComparison.Ordinal))
        {
            value = "{" + value + "}";
        }
        return value.ToUpperInvariant();
    }

    private static bool IsUsableContainer(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string text = value.Trim().Trim('{', '}').ToUpperInvariant();
        return text != "00000000-0000-0000-0000-000000000000" &&
               text != "00000000-0000-0000-FFFF-FFFFFFFFFFFF";
    }
}

internal static class AudioReport
{
    public static string Build(List<AudioEndpointInfo> endpoints, PortReportFormat format)
    {
        switch (format)
        {
            case PortReportFormat.Plain:
                return BuildPlain(endpoints);
            case PortReportFormat.Markdown:
                return BuildMarkdown(endpoints);
            case PortReportFormat.Csv:
                return BuildCsv(endpoints);
            case PortReportFormat.Detail:
                return BuildDetail(endpoints);
            default:
                return BuildTable(endpoints, true);
        }
    }

    public static string BuildTable(List<AudioEndpointInfo> endpoints, bool includeBoard)
    {
        if (endpoints.Count == 0)
        {
            return "未发现活动音频端点。";
        }

        int directionWidth = 9;
        int stateWidth = 7;
        int keyWidth = 9;
        int friendlyWidth = 12;
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            directionWidth = Math.Max(directionWidth, DisplayWidth(endpoint.Direction));
            stateWidth = Math.Max(stateWidth, DisplayWidth(endpoint.State));
            keyWidth = Math.Max(keyWidth, DisplayWidth(endpoint.DeviceKey));
            friendlyWidth = Math.Max(friendlyWidth, DisplayWidth(endpoint.FriendlyName));
        }

        StringBuilder text = new StringBuilder();
        text.Append(Pad("Direction", directionWidth)).Append("  ")
            .Append(Pad("State", stateWidth)).Append("  ")
            .Append(Pad("DeviceKey", keyWidth)).Append("  ")
            .Append(Pad("Ch", 2)).Append("  ")
            .Append(Pad("FriendlyName", friendlyWidth));
        if (includeBoard)
        {
            text.Append("  Board");
        }
        text.AppendLine();

        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            text.Append(Pad(endpoint.Direction, directionWidth)).Append("  ")
                .Append(Pad(endpoint.State, stateWidth)).Append("  ")
                .Append(Pad(endpoint.DeviceKey, keyWidth)).Append("  ")
                .Append(Pad(endpoint.ChannelText, 2)).Append("  ")
                .Append(Pad(endpoint.FriendlyName, friendlyWidth));
            if (includeBoard)
            {
                text.Append("  ").Append(endpoint.BoardTitle ?? string.Empty);
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    public static string BuildPlain(List<AudioEndpointInfo> endpoints)
    {
        StringBuilder text = new StringBuilder();
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            text.Append(endpoint.Direction).Append('\t')
                .Append(endpoint.DeviceKey).Append('\t')
                .Append(endpoint.ChannelText).Append('\t')
                .Append(endpoint.FriendlyName).Append('\t')
                .Append(endpoint.BoardTitle ?? string.Empty)
                .AppendLine();
        }
        return text.ToString();
    }

    public static string BuildMarkdown(List<AudioEndpointInfo> endpoints)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# 声卡列表");
        text.AppendLine();
        if (endpoints.Count == 0)
        {
            text.AppendLine("未发现活动音频端点。");
            return text.ToString();
        }

        text.AppendLine("| Direction | State | DeviceKey | Ch | FriendlyName | Board |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            text.Append("| ").Append(MarkdownCell(endpoint.Direction))
                .Append(" | ").Append(MarkdownCell(endpoint.State))
                .Append(" | ").Append(MarkdownCell(endpoint.DeviceKey))
                .Append(" | ").Append(endpoint.ChannelText)
                .Append(" | ").Append(MarkdownCell(endpoint.FriendlyName))
                .Append(" | ").Append(MarkdownCell(endpoint.BoardTitle))
                .AppendLine(" |");
        }
        return text.ToString();
    }

    public static string BuildCsv(List<AudioEndpointInfo> endpoints)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("Direction,State,DeviceKey,Channels,FriendlyName,EndpointId,Board");
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            text.Append(CsvCell(endpoint.Direction)).Append(',')
                .Append(CsvCell(endpoint.State)).Append(',')
                .Append(CsvCell(endpoint.DeviceKey)).Append(',')
                .Append(CsvCell(endpoint.ChannelText)).Append(',')
                .Append(CsvCell(endpoint.FriendlyName)).Append(',')
                .Append(CsvCell(endpoint.EndpointId)).Append(',')
                .Append(CsvCell(endpoint.BoardTitle))
                .AppendLine();
        }
        return text.ToString();
    }

    public static string BuildDetail(List<AudioEndpointInfo> endpoints)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine(string.Format("音频端点详细信息（{0} 个端点）", endpoints.Count));
        if (endpoints.Count == 0)
        {
            text.AppendLine("未发现活动音频端点。");
            return text.ToString();
        }

        int index = 1;
        foreach (AudioEndpointInfo endpoint in endpoints)
        {
            text.AppendLine(string.Format("[{0}] {1} / {2}", index, endpoint.Direction, endpoint.FriendlyName));
            text.AppendLine(FormatEndpoint(endpoint));
            index++;
        }
        return text.ToString();
    }

    public static string FormatEndpoint(AudioEndpointInfo endpoint)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("音频端点详细信息");
        text.AppendLine(new string('=', 72));
        text.AppendLine("方向:        " + endpoint.Direction);
        text.AppendLine("状态:        " + endpoint.State);
        text.AppendLine("DeviceKey:   " + endpoint.DeviceKey);
        text.AppendLine("通道数:      " + endpoint.ChannelText);
        text.AppendLine("显示名称:    " + endpoint.FriendlyName);
        text.AppendLine("端点名称:    " + endpoint.EndpointName);
        text.AppendLine("设备名称:    " + endpoint.DeviceName);
        text.AppendLine("端点 ID:     " + endpoint.EndpointId);
        text.AppendLine("Container ID: " + endpoint.ContainerId);
        text.AppendLine("父设备:      " + endpoint.Parent);
        text.AppendLine("接口路径:    " + endpoint.InterfacePath);
        text.AppendLine("位置路径:");
        text.AppendLine(MultiLine(endpoint.LocationPaths));
        text.AppendLine("所属板卡:    " + endpoint.BoardTitle);
        return text.ToString();
    }

    private static string Pad(string value, int width)
    {
        value = value ?? string.Empty;
        int padding = width - DisplayWidth(value);
        return padding > 0 ? value + new string(' ', padding) : value;
    }

    private static int DisplayWidth(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        int width = 0;
        foreach (char ch in value)
        {
            if (ch >= 0x1100 &&
                (ch <= 0x115F ||
                 ch == 0x2329 || ch == 0x232A ||
                 (ch >= 0x2E80 && ch <= 0xA4CF && ch != 0x303F) ||
                 (ch >= 0xAC00 && ch <= 0xD7A3) ||
                 (ch >= 0xF900 && ch <= 0xFAFF) ||
                 (ch >= 0xFE10 && ch <= 0xFE19) ||
                 (ch >= 0xFE30 && ch <= 0xFE6F) ||
                 (ch >= 0xFF00 && ch <= 0xFF60) ||
                 (ch >= 0xFFE0 && ch <= 0xFFE6)))
            {
                width += 2;
            }
            else
            {
                width++;
            }
        }
        return width;
    }

    private static string MarkdownCell(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string CsvCell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        value = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\0', ' ').Trim();
        if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static string MultiLine(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        return value.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\0', '\n');
    }
}

internal enum PortReportFormat
{
    Tree,
    Plain,
    Markdown,
    Csv,
    Detail
}

internal static class PortReport
{
    public static string Build(List<PortInfo> ports, PortReportFormat format)
    {
        return Build(ports, new List<AudioEndpointInfo>(), format);
    }

    public static string Build(
        List<PortInfo> ports, List<AudioEndpointInfo> audio, PortReportFormat format)
    {
        switch (format)
        {
            case PortReportFormat.Plain:
                return BuildPlain(ports);
            case PortReportFormat.Markdown:
                return BuildMarkdown(ports);
            case PortReportFormat.Csv:
                return BuildCsv(ports);
            case PortReportFormat.Detail:
                return BuildDetail(ports, audio);
            default:
                return BuildTree(ports, audio);
        }
    }

    public static string FormatName(PortReportFormat format)
    {
        switch (format)
        {
            case PortReportFormat.Plain:
                return "串口列表";
            case PortReportFormat.Markdown:
                return "Markdown";
            case PortReportFormat.Csv:
                return "CSV";
            case PortReportFormat.Detail:
                return "详细信息";
            default:
                return "树状摘要";
        }
    }

    public static string BuildTree(List<PortInfo> ports)
    {
        return BuildTree(ports, new List<AudioEndpointInfo>());
    }

    public static string BuildTree(List<PortInfo> ports, List<AudioEndpointInfo> audio)
    {
        StringBuilder text = new StringBuilder();
        if (ports.Count == 0)
        {
            text.AppendLine("未发现当前连接的串口设备。");
            return text.ToString();
        }

        List<BoardGroup> boards = PortGrouper.Build(ports, audio);
        if (boards.Count == 1)
        {
            AppendBoard(text, boards[0], string.Empty);
        }
        else
        {
            text.AppendLine(string.Format("物理 USB 板卡（{0} 个）", boards.Count));
            foreach (BoardGroup board in boards)
            {
                AppendBoard(text, board, "  ");
            }
        }
        return text.ToString();
    }

    private static void AppendBoard(StringBuilder text, BoardGroup board, string indent)
    {
        text.Append(indent).AppendLine(board.Title);
        string deviceIndent = indent + "  ";
        foreach (PortGroup device in board.Devices)
        {
            text.Append(deviceIndent).AppendLine(device.Title);
            foreach (PortInfo port in device.Ports)
            {
                text.Append(deviceIndent).Append("  ").AppendLine(PortGrouper.PortTitle(port));
            }
        }

        if (board.Audio.Count > 0)
        {
            text.Append(deviceIndent).AppendLine(string.Format("音频端点  ({0} 个)", board.Audio.Count));
            foreach (AudioEndpointInfo endpoint in board.Audio)
            {
                text.Append(deviceIndent).Append("  ").AppendLine(AudioLine(endpoint));
            }
        }
    }

    public static string BuildBoardTree(BoardGroup board)
    {
        StringBuilder text = new StringBuilder();
        AppendBoard(text, board, string.Empty);
        return text.ToString();
    }

    public static string BuildGroupTree(PortGroup group, string boardTitle)
    {
        StringBuilder text = new StringBuilder();
        string indent = string.Empty;
        if (!string.IsNullOrEmpty(boardTitle))
        {
            text.AppendLine(boardTitle);
            indent = "  ";
        }
        text.Append(indent).AppendLine(group.Title);
        foreach (PortInfo port in group.Ports)
        {
            text.Append(indent).Append("  ").AppendLine(PortGrouper.PortTitle(port));
        }
        return text.ToString();
    }

    internal static string AudioLine(AudioEndpointInfo endpoint)
    {
        return (endpoint.Direction ?? string.Empty).PadRight(7) + "  " +
            endpoint.ChannelLabel + "  " +
            endpoint.DeviceKey + "  " + endpoint.FriendlyName;
    }

    public static string BuildPlain(List<PortInfo> ports)
    {
        StringBuilder text = new StringBuilder();
        foreach (BoardGroup board in PortGrouper.Build(ports))
        {
            foreach (PortGroup group in board.Devices)
            {
                foreach (PortInfo port in group.Ports)
                {
                    string label = PortGrouper.InterfaceLabel(port);
                    text.AppendLine(string.IsNullOrEmpty(label)
                        ? port.ComName
                        : label + "\t" + port.ComName);
                }
            }
        }
        return text.ToString();
    }

    public static string BuildMarkdown(List<PortInfo> ports)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# USB 串口树");
        text.AppendLine();
        text.AppendLine(string.Format("共 {0} 个串口。生成时间：{1}",
            ports.Count, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        text.AppendLine();
        if (ports.Count == 0)
        {
            text.AppendLine("未发现当前连接的串口设备。");
            return text.ToString();
        }

        text.AppendLine("| 板卡 | 设备功能 | 接口 | 串口 | 显示名称 |");
        text.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (BoardGroup board in PortGrouper.Build(ports))
        {
            foreach (PortGroup group in board.Devices)
            {
                foreach (PortInfo port in group.Ports)
                {
                    string label = PortGrouper.InterfaceLabel(port);
                    text.Append("| ").Append(MarkdownCell(board.Title))
                        .Append(" | ").Append(MarkdownCell(group.Title))
                        .Append(" | ").Append(string.IsNullOrEmpty(label) ? "-" : label)
                        .Append(" | ").Append(port.ComName)
                        .Append(" | ").Append(MarkdownCell(port.FriendlyName))
                        .AppendLine(" |");
                }
            }
        }
        return text.ToString();
    }

    public static string BuildCsv(List<PortInfo> ports)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("板卡,设备功能,接口,串口,显示名称,实例ID,父设备,ContainerID,位置路径");
        foreach (BoardGroup board in PortGrouper.Build(ports))
        {
            foreach (PortGroup group in board.Devices)
            {
                foreach (PortInfo port in group.Ports)
                {
                    text.Append(CsvCell(board.Title)).Append(',')
                        .Append(CsvCell(group.Title)).Append(',')
                        .Append(CsvCell(PortGrouper.InterfaceLabel(port))).Append(',')
                        .Append(CsvCell(port.ComName)).Append(',')
                        .Append(CsvCell(port.FriendlyName)).Append(',')
                        .Append(CsvCell(port.InstanceId)).Append(',')
                        .Append(CsvCell(port.Parent)).Append(',')
                        .Append(CsvCell(port.ContainerId)).Append(',')
                        .Append(CsvCell(MultiLine(port.LocationPaths)))
                        .AppendLine();
                }
            }
        }
        return text.ToString();
    }

    public static string BuildDetail(List<PortInfo> ports)
    {
        return BuildDetail(ports, new List<AudioEndpointInfo>());
    }

    public static string BuildDetail(List<PortInfo> ports, List<AudioEndpointInfo> audio)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine(string.Format("USB 串口详细信息（{0} 个串口）", ports.Count));
        text.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        text.AppendLine();
        if (ports.Count == 0)
        {
            text.AppendLine("未发现当前连接的串口设备。");
            return text.ToString();
        }

        int index = 1;
        foreach (BoardGroup board in PortGrouper.Build(ports, audio))
        {
            foreach (PortGroup group in board.Devices)
            {
                foreach (PortInfo port in group.Ports)
                {
                    text.AppendLine(string.Format("[{0}] {1} / {2} / {3}",
                        index, board.Title, group.Title, PortGrouper.PortTitle(port)));
                    text.AppendLine(FormatPort(port));
                    index++;
                }
            }
            foreach (AudioEndpointInfo endpoint in board.Audio)
            {
                text.AppendLine(string.Format("[{0}] {1} / 音频端点 / {2}",
                    index, board.Title, AudioLine(endpoint)));
                text.AppendLine(AudioReport.FormatEndpoint(endpoint));
                index++;
            }
        }
        return text.ToString();
    }

    private static string MarkdownCell(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string CsvCell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        value = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\0', ' ').Trim();
        if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static string MultiLine(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        return value.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\0', '\n');
    }

    public static string FormatBoard(BoardGroup board)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("物理 USB 板卡");
        text.AppendLine(new string('=', 72));
        text.AppendLine("显示名称: " + board.Title);
        text.AppendLine("共享上游位置路径: " + board.Key);
        text.AppendLine();
        text.AppendLine("设备功能:");
        foreach (PortGroup device in board.Devices)
        {
            text.AppendLine("  " + device.Title);
        }
        if (board.Audio.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("音频端点:");
            foreach (AudioEndpointInfo endpoint in board.Audio)
            {
                text.AppendLine("  " + AudioLine(endpoint));
            }
        }
        text.AppendLine();
        text.AppendLine("说明: 该层依据共享的 USB 上游位置路径建立，仅用于本工具可视化，不改变 Windows PnP 枚举。");
        return text.ToString();
    }

    public static string FormatGroup(PortGroup group)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("USB 设备组");
        text.AppendLine(new string('=', 72));
        text.AppendLine("父设备名称: " + group.ParentName);
        text.AppendLine("父设备实例: " + group.Parent);
        text.AppendLine("总线描述:   " + group.BusDescription);
        text.AppendLine("Container ID: " + group.ContainerId);
        text.AppendLine();
        text.AppendLine("包含串口:");
        foreach (PortInfo port in group.Ports)
        {
            text.AppendLine("  " + port.ComName + "  " + port.FriendlyName);
        }
        text.AppendLine();
        text.AppendLine("说明: 这是只读的 PnP 关系展示，不会改变设备管理器中的设备归属。");
        return text.ToString();
    }

    public static string FormatPort(PortInfo port)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("串口详细信息");
        text.AppendLine(new string('=', 72));
        text.AppendLine("串口:        " + port.ComName);
        text.AppendLine("显示名称:    " + port.FriendlyName);
        text.AppendLine("设备描述:    " + port.DeviceDescription);
        text.AppendLine("制造商:      " + port.Manufacturer);
        text.AppendLine("实例 ID:     " + port.InstanceId);
        text.AppendLine("父设备:      " + port.Parent);
        text.AppendLine("父设备名称:  " + port.ParentName);
        text.AppendLine("Container ID: " + port.ContainerId);
        text.AppendLine("总线描述:    " + port.BusDescription);
        text.AppendLine("硬件 ID:     " + port.HardwareIds);
        text.AppendLine("位置路径:");
        text.AppendLine(MultiLine(port.LocationPaths));
        return text.ToString();
    }
}

internal static class ClipboardHelper
{
    public static bool TrySetText(string text, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            error = "内容为空。";
            return false;
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            System.Threading.Thread.Sleep(120);
        }
        return false;
    }
}

internal sealed class MainForm : Form
{
    private readonly TreeView tree;
    private readonly TextBox details;
    private readonly ListView audioList;
    private readonly TextBox audioDetails;
    private readonly Label audioHeader;
    private readonly Label status;
    private readonly Label scopeLabel;
    private readonly CheckBox autoRefresh;
    private readonly ComboBox formatBox;
    private readonly Timer timer;
    private string lastSignature = string.Empty;
    private List<PortInfo> currentPorts = new List<PortInfo>();
    private List<AudioEndpointInfo> currentAudio = new List<AudioEndpointInfo>();
    private int activePane;

    public MainForm()
    {
        try
        {
            string assemblyPath = typeof(MainForm).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                string iconPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(assemblyPath), "..", "assets", "usbTree.ico"));
                if (File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
            }
        }
        catch
        {
        }

        Text = "USB 串口与声卡查看器";
        Width = 1440;
        Height = 780;
        MinimumSize = new Size(1100, 600);
        StartPosition = FormStartPosition.CenterScreen;

        Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 8, 8, 6) };
        Button refresh = new Button { Text = "立即刷新", Width = 80, Dock = DockStyle.Left };
        refresh.Click += delegate { RefreshPorts(true); };
        autoRefresh = new CheckBox { Text = "自动刷新（2秒）", Checked = true, Width = 120, Dock = DockStyle.Left };
        Button copySelected = new Button { Text = "复制选中", Width = 90, Dock = DockStyle.Left };
        copySelected.Click += delegate { CopySelected(); };
        Button copyAll = new Button { Text = "复制全部", Width = 90, Dock = DockStyle.Left };
        copyAll.Click += delegate { CopyAll(); };
        Label formatLabel = new Label
        {
            Text = "复制格式:",
            Width = 66,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };
        formatBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Dock = DockStyle.Left };
        formatBox.Items.AddRange(new object[]
        {
            "摘要", "简单列表", "Markdown", "CSV", "详细信息", "全部（USB+声卡）"
        });
        formatBox.SelectedIndex = 0;
        Button export = new Button { Text = "导出...", Width = 74, Dock = DockStyle.Left };
        export.Click += delegate { ExportReport(); };
        scopeLabel = new Label
        {
            Text = "复制范围: USB 设备",
            Width = 150,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };
        status = new Label { Text = "正在读取...", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        toolbar.Controls.Add(status);
        toolbar.Controls.Add(scopeLabel);
        toolbar.Controls.Add(export);
        toolbar.Controls.Add(formatBox);
        toolbar.Controls.Add(formatLabel);
        toolbar.Controls.Add(copyAll);
        toolbar.Controls.Add(copySelected);
        toolbar.Controls.Add(autoRefresh);
        toolbar.Controls.Add(refresh);

        tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        tree.AfterSelect += TreeAfterSelect;
        tree.ContextMenuStrip = BuildContextMenu();
        tree.Enter += delegate { SetActivePane(0); };
        details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10F),
            BackColor = SystemColors.Window
        };
        details.Enter += delegate { SetActivePane(0); };

        SplitContainer leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            IsSplitterFixed = false
        };
        leftSplit.Panel1.Controls.Add(tree);
        leftSplit.Panel2.Controls.Add(details);

        audioList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        audioList.Columns.Add("方向", 60);
        audioList.Columns.Add("状态", 70);
        audioList.Columns.Add("通道", 45, HorizontalAlignment.Right);
        audioList.Columns.Add("显示名称", 230);
        audioList.Columns.Add("所属板卡", 110);
        audioList.Columns.Add("DeviceKey", 260);
        audioList.SelectedIndexChanged += AudioAfterSelect;
        audioList.ContextMenuStrip = BuildAudioContextMenu();
        audioList.Enter += delegate { SetActivePane(1); };
        audioHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "声卡列表（全部活动端点）",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Padding = new Padding(6, 0, 0, 0)
        };
        Panel audioPanel = new Panel { Dock = DockStyle.Fill };
        audioPanel.Controls.Add(audioList);
        audioPanel.Controls.Add(audioHeader);

        audioDetails = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10F),
            BackColor = SystemColors.Window
        };
        audioDetails.Enter += delegate { SetActivePane(1); };

        SplitContainer rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            IsSplitterFixed = false
        };
        rightSplit.Panel1.Controls.Add(audioPanel);
        rightSplit.Panel2.Controls.Add(audioDetails);

        SplitContainer outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false
        };
        outer.Panel1.Controls.Add(leftSplit);
        outer.Panel2.Controls.Add(rightSplit);
        Controls.Add(outer);
        Controls.Add(toolbar);

        Shown += delegate
        {
            BeginInvoke(new MethodInvoker(delegate
            {
                SetSplitterDistance(outer, 0.62, 500, 360);
                SetSplitterDistance(leftSplit, 0.60, 260, 200);
                SetSplitterDistance(rightSplit, 0.55, 260, 200);
            }));
        };

        KeyPreview = true;
        KeyDown += MainKeyDown;

        timer = new Timer { Interval = 2000 };
        timer.Tick += delegate { if (autoRefresh.Checked) RefreshPorts(false); };
        Load += delegate
        {
            RefreshPorts(true);
            timer.Start();
        };
        FormClosed += delegate { timer.Stop(); };
    }

    private void RefreshPorts(bool force)
    {
        try
        {
            List<PortInfo> ports = UsbSerialReader.ReadPorts();
            List<AudioEndpointInfo> audio;
            string audioError = null;
            try
            {
                audio = AudioEndpointReader.ReadEndpoints(false);
            }
            catch (Exception ex)
            {
                audio = new List<AudioEndpointInfo>();
                audioError = ex.Message;
            }

            string signature = BuildSignature(ports, audio);
            if (!force && string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                return;
            }
            lastSignature = signature;
            currentPorts = ports;
            currentAudio = audio;

            List<BoardGroup> boards = PortGrouper.Build(ports, audio);
            BuildTree(boards);
            BuildAudioList(audio);
            status.Text = string.Format(
                "已刷新：{0} 个串口，{1} 个物理 USB 板卡，{2} 个音频端点。工具只读，不修改系统。",
                ports.Count, boards.Count, audio.Count);
            if (!string.IsNullOrEmpty(audioError))
            {
                status.Text = "音频读取失败：" + audioError;
            }
        }
        catch (Exception ex)
        {
            status.Text = "读取失败：" + ex.Message;
            details.Text = ex.ToString();
        }
    }

    private static string BuildSignature(List<PortInfo> ports, List<AudioEndpointInfo> audio)
    {
        StringBuilder value = new StringBuilder();
        foreach (PortInfo port in ports)
        {
            value.Append(port.InstanceId).Append('|')
                .Append(port.ComName).Append('|')
                .Append(port.Parent).Append('|')
                .Append(port.ParentName).Append('|')
                .Append(port.ContainerId).Append('|')
                .Append(port.FriendlyName).Append('|')
                .Append(port.BusDescription).Append('|')
                .Append(port.DeviceDescription).Append('|')
                .Append(port.Manufacturer).Append('|')
                .Append(port.HardwareIds).Append('|')
                .Append(port.LocationPaths).Append('\n');
        }
        foreach (AudioEndpointInfo endpoint in audio)
        {
            value.Append("A|").Append(endpoint.Direction).Append('|')
                .Append(endpoint.State).Append('|')
                .Append(endpoint.DeviceKey).Append('|')
                .Append(endpoint.Channels).Append('|')
                .Append(endpoint.FriendlyName).Append('|')
                .Append(endpoint.EndpointId).Append('|')
                .Append(endpoint.BoardTitle).Append('\n');
        }
        return value.ToString();
    }

    private void BuildTree(List<BoardGroup> boards)
    {
        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            if (boards.Count == 0)
            {
                TreeNode emptyRoot = new TreeNode("USB 串口设备");
                emptyRoot.Nodes.Add(new TreeNode("未发现当前连接的串口设备。"));
                emptyRoot.Expand();
                tree.Nodes.Add(emptyRoot);
                tree.SelectedNode = emptyRoot;
                return;
            }

            TreeNode root;
            if (boards.Count == 1)
            {
                root = CreateBoardNode(boards[0]);
                tree.Nodes.Add(root);
            }
            else
            {
                root = new TreeNode(string.Format("物理 USB 板卡（{0} 个）", boards.Count));
                tree.Nodes.Add(root);
                foreach (BoardGroup board in boards)
                {
                    root.Nodes.Add(CreateBoardNode(board));
                }
            }

            root.ExpandAll();
            tree.SelectedNode = root;
        }
        finally
        {
            tree.EndUpdate();
        }
    }

    private static TreeNode CreateBoardNode(BoardGroup board)
    {
        TreeNode boardNode = new TreeNode(board.Title) { Tag = board };
        foreach (PortGroup group in board.Devices)
        {
            TreeNode groupNode = new TreeNode(group.Title) { Tag = group };
            foreach (PortInfo port in group.Ports)
            {
                groupNode.Nodes.Add(new TreeNode(PortGrouper.PortTitle(port)) { Tag = port });
            }
            boardNode.Nodes.Add(groupNode);
        }

        if (board.Audio.Count > 0)
        {
            TreeNode audioNode = new TreeNode(string.Format("音频端点  ({0} 个)", board.Audio.Count));
            foreach (AudioEndpointInfo endpoint in board.Audio)
            {
                audioNode.Nodes.Add(new TreeNode(PortReport.AudioLine(endpoint)) { Tag = endpoint });
            }
            boardNode.Nodes.Add(audioNode);
        }
        return boardNode;
    }

    private void BuildAudioList(List<AudioEndpointInfo> audio)
    {
        audioList.BeginUpdate();
        try
        {
            audioList.Items.Clear();
            foreach (AudioEndpointInfo endpoint in audio)
            {
                ListViewItem item = new ListViewItem(endpoint.Direction);
                item.SubItems.Add(endpoint.State);
                item.SubItems.Add(endpoint.ChannelText);
                item.SubItems.Add(endpoint.FriendlyName);
                item.SubItems.Add(endpoint.BoardTitle ?? string.Empty);
                item.SubItems.Add(endpoint.DeviceKey);
                item.Tag = endpoint;
                audioList.Items.Add(item);
            }
        }
        finally
        {
            audioList.EndUpdate();
        }

        audioHeader.Text = string.Format("声卡列表（全部活动端点，{0} 个）", audio.Count);
        if (audioList.Items.Count > 0)
        {
            audioList.Items[0].Selected = true;
        }
        else
        {
            audioDetails.Text = "未发现活动音频端点。";
        }
    }

    private static void SetSplitterDistance(
        SplitContainer split, double ratio, int minFirst, int minSecond)
    {
        int total = split.Orientation == Orientation.Vertical
            ? split.ClientSize.Width
            : split.ClientSize.Height;
        int available = total - split.SplitterWidth;
        int maxFirst = available - minSecond;
        if (maxFirst < minFirst)
        {
            return;
        }

        int value = Math.Max(minFirst, Math.Min((int)(available * ratio), maxFirst));
        try
        {
            split.SplitterDistance = value;
            split.Panel1MinSize = minFirst;
            split.Panel2MinSize = minSecond;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void SetActivePane(int pane)
    {
        activePane = pane;
        scopeLabel.Text = pane == 1 ? "复制范围: 声卡列表" : "复制范围: USB 设备";
    }

    private ContextMenuStrip BuildContextMenu()
    {
        ToolStripMenuItem copySelected = new ToolStripMenuItem("复制选中项");
        copySelected.Click += delegate { CopySelected(); };
        ToolStripMenuItem copyCom = new ToolStripMenuItem("复制串口名");
        copyCom.Click += delegate { CopySelectedCom(); };
        ToolStripMenuItem copyAll = new ToolStripMenuItem("复制全部（当前格式）");
        copyAll.Click += delegate { CopyAll(); };
        ToolStripMenuItem export = new ToolStripMenuItem("导出...");
        export.Click += delegate { ExportReport(); };

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(copySelected);
        menu.Items.Add(copyCom);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(copyAll);
        menu.Items.Add(export);
        menu.Opening += delegate
        {
            SetActivePane(0);
            TreeNode node = tree.SelectedNode;
            copyCom.Enabled = node != null && node.Tag is PortInfo;
        };
        return menu;
    }

    private ContextMenuStrip BuildAudioContextMenu()
    {
        ToolStripMenuItem copyKey = new ToolStripMenuItem("复制 DeviceKey");
        copyKey.Click += delegate { CopySelectedAudioKey(); };
        ToolStripMenuItem copySelected = new ToolStripMenuItem("复制选中声卡");
        copySelected.Click += delegate { CopySelectedAudio(); };
        ToolStripMenuItem copyAll = new ToolStripMenuItem("复制全部声卡");
        copyAll.Click += delegate
        {
            CopyText(AudioReport.BuildTable(currentAudio, true), "全部声卡信息");
        };

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(copyKey);
        menu.Items.Add(copySelected);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(copyAll);
        menu.Opening += delegate
        {
            SetActivePane(1);
            copyKey.Enabled = audioList.SelectedItems.Count > 0;
            copySelected.Enabled = audioList.SelectedItems.Count > 0;
        };
        return menu;
    }

    private void MainKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.C)
        {
            CopyAll();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.C && (tree.ContainsFocus || audioList.ContainsFocus))
        {
            CopySelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private enum ReportChoice
    {
        Summary,
        Plain,
        Markdown,
        Csv,
        Detail,
        All
    }

    private ReportChoice SelectedChoice()
    {
        switch (formatBox.SelectedIndex)
        {
            case 1:
                return ReportChoice.Plain;
            case 2:
                return ReportChoice.Markdown;
            case 3:
                return ReportChoice.Csv;
            case 4:
                return ReportChoice.Detail;
            case 5:
                return ReportChoice.All;
            default:
                return ReportChoice.Summary;
        }
    }

    private static string ChoiceName(ReportChoice choice)
    {
        switch (choice)
        {
            case ReportChoice.Plain:
                return "简单列表";
            case ReportChoice.Markdown:
                return "Markdown";
            case ReportChoice.Csv:
                return "CSV";
            case ReportChoice.Detail:
                return "详细信息";
            case ReportChoice.All:
                return "全部";
            default:
                return "摘要";
        }
    }

    private static PortReportFormat ChoiceToFormat(ReportChoice choice)
    {
        switch (choice)
        {
            case ReportChoice.Plain:
                return PortReportFormat.Plain;
            case ReportChoice.Markdown:
                return PortReportFormat.Markdown;
            case ReportChoice.Csv:
                return PortReportFormat.Csv;
            case ReportChoice.Detail:
                return PortReportFormat.Detail;
            default:
                return PortReportFormat.Tree;
        }
    }

    private string BuildChoiceText(ReportChoice choice, bool audio)
    {
        if (choice == ReportChoice.All)
        {
            return BuildAllText();
        }

        PortReportFormat format = ChoiceToFormat(choice);
        return audio
            ? AudioReport.Build(currentAudio, format)
            : PortReport.Build(currentPorts, currentAudio, format);
    }

    private string BuildAllText()
    {
        string usb = PortReport.BuildTree(currentPorts, currentAudio).TrimEnd();
        string sound = string.Format("声卡列表（{0} 个端点）\r\n{1}",
            currentAudio.Count, AudioReport.BuildTable(currentAudio, true));
        return usb + "\r\n\r\n" + sound.TrimEnd();
    }

    private void CopyAll()
    {
        ReportChoice choice = SelectedChoice();
        bool audio = activePane == 1 && choice != ReportChoice.All;
        string text = BuildChoiceText(choice, audio);
        string scope = choice == ReportChoice.All ? "USB+声卡" : (audio ? "声卡列表" : "USB 设备信息");
        CopyText(text, scope + "（" + ChoiceName(choice) + "）");
    }

    private void CopySelected()
    {
        if (activePane == 1)
        {
            CopySelectedAudio();
            return;
        }

        TreeNode node = tree.SelectedNode;
        string text = null;
        string label = "选中项";
        if (node != null)
        {
            PortInfo port = node.Tag as PortInfo;
            PortGroup group = node.Tag as PortGroup;
            BoardGroup board = node.Tag as BoardGroup;
            AudioEndpointInfo endpoint = node.Tag as AudioEndpointInfo;
            if (port != null)
            {
                text = PortReport.FormatPort(port);
                label = port.ComName;
            }
            else if (group != null)
            {
                BoardGroup parentBoard = node.Parent == null ? null : node.Parent.Tag as BoardGroup;
                text = PortReport.BuildGroupTree(group, parentBoard == null ? null : parentBoard.Title);
                label = group.Title;
            }
            else if (board != null)
            {
                text = PortReport.BuildBoardTree(board);
                label = board.Title;
            }
            else if (endpoint != null)
            {
                text = AudioReport.FormatEndpoint(endpoint);
                label = endpoint.Direction + " " + endpoint.FriendlyName;
            }
        }

        if (text == null)
        {
            ReportChoice choice = SelectedChoice();
            text = BuildChoiceText(choice, false);
            label = "USB 设备信息（" + ChoiceName(choice) + "）";
        }
        CopyText(text, label);
    }

    private void CopySelectedAudio()
    {
        if (audioList.SelectedItems.Count == 0)
        {
            status.Text = "请先在声卡列表中选择一项。";
            return;
        }

        AudioEndpointInfo endpoint = audioList.SelectedItems[0].Tag as AudioEndpointInfo;
        if (endpoint == null)
        {
            return;
        }
        CopyText(AudioReport.FormatEndpoint(endpoint),
            endpoint.Direction + " " + endpoint.FriendlyName);
    }

    private void CopySelectedAudioKey()
    {
        if (audioList.SelectedItems.Count == 0)
        {
            return;
        }

        AudioEndpointInfo endpoint = audioList.SelectedItems[0].Tag as AudioEndpointInfo;
        if (endpoint != null)
        {
            CopyText(endpoint.DeviceKey, endpoint.DeviceKey);
        }
    }

    private void CopySelectedCom()
    {
        TreeNode node = tree.SelectedNode;
        PortInfo port = node == null ? null : node.Tag as PortInfo;
        if (port == null)
        {
            return;
        }
        CopyText(port.ComName, port.ComName);
    }

    private void CopyText(string text, string label)
    {
        if (string.IsNullOrEmpty(text))
        {
            status.Text = "当前没有可复制的信息。";
            return;
        }

        string error;
        if (ClipboardHelper.TrySetText(text, out error))
        {
            status.Text = string.Format("已复制{0}到剪贴板（{1} 个字符）。", label, text.Length);
        }
        else
        {
            status.Text = "复制失败：" + error;
        }
    }

    private void AudioAfterSelect(object sender, EventArgs e)
    {
        if (audioList.SelectedItems.Count == 0)
        {
            return;
        }

        AudioEndpointInfo endpoint = audioList.SelectedItems[0].Tag as AudioEndpointInfo;
        if (endpoint != null)
        {
            audioDetails.Text = AudioReport.FormatEndpoint(endpoint);
        }
    }

    private void ExportReport()
    {
        ReportChoice choice = SelectedChoice();
        bool audio = activePane == 1 && choice != ReportChoice.All;
        string text = BuildChoiceText(choice, audio);
        string extension = "txt";
        int filterIndex = 1;
        if (choice == ReportChoice.Csv)
        {
            extension = "csv";
            filterIndex = 2;
        }
        else if (choice == ReportChoice.Markdown)
        {
            extension = "md";
            filterIndex = 3;
        }

        string prefix = choice == ReportChoice.All
            ? "usbTree-all"
            : (audio ? "usbTree-audio" : "usbTree-usb");
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Title = choice == ReportChoice.All
                ? "导出 USB 与声卡信息"
                : (audio ? "导出声卡信息" : "导出 USB 设备信息");
            dialog.Filter = "文本文件 (*.txt)|*.txt|CSV 文件 (*.csv)|*.csv|Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*";
            dialog.FilterIndex = filterIndex;
            dialog.DefaultExt = extension;
            dialog.FileName = prefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "." + extension;
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, text, new UTF8Encoding(true));
                status.Text = "已导出：" + dialog.FileName;
            }
            catch (Exception ex)
            {
                status.Text = "导出失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void TreeAfterSelect(object sender, TreeViewEventArgs e)
    {
        PortInfo port = e.Node.Tag as PortInfo;
        if (port != null)
        {
            details.Text = PortReport.FormatPort(port);
            return;
        }
        BoardGroup board = e.Node.Tag as BoardGroup;
        if (board != null)
        {
            details.Text = PortReport.FormatBoard(board);
            return;
        }
        PortGroup group = e.Node.Tag as PortGroup;
        if (group != null)
        {
            details.Text = PortReport.FormatGroup(group);
            return;
        }
        AudioEndpointInfo endpoint = e.Node.Tag as AudioEndpointInfo;
        if (endpoint != null)
        {
            details.Text = AudioReport.FormatEndpoint(endpoint);
            return;
        }
        details.Text = "选择左侧的 USB 设备组、串口或音频端点查看详细信息。\r\n\r\n" +
            "分组优先依据 Container ID，其次使用父设备；这与 Windows 设备枚举关系一致。";
    }

}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal static class CliProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        return Run(args);
    }

    internal static int Run(string[] args)
    {
        PrepareOutput();

        PortReportFormat format = PortReportFormat.Tree;
        string outputPath = null;
        bool copy = false;
        bool audioOnly = false;
        bool all = false;
        bool includeDisabled = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string lower = arg.ToLowerInvariant();
            if (lower == "-h" || lower == "--help" || lower == "/?")
            {
                PrintHelp();
                return 0;
            }
            if (lower == "-l" || lower == "--list")
            {
                continue;
            }
            if (lower == "-a" || lower == "--audio")
            {
                audioOnly = true;
                continue;
            }
            if (lower == "--all")
            {
                all = true;
                continue;
            }
            if (lower == "--include-disabled")
            {
                includeDisabled = true;
                continue;
            }
            if (lower == "-f" || lower == "--format")
            {
                if (i + 1 >= args.Length)
                {
                    return UsageError("--format 缺少参数。");
                }
                i++;
                string value = args[i];
                if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    all = true;
                }
                else if (value.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioOnly = true;
                }
                else if (!TryParseFormat(value, out format))
                {
                    return UsageError("未知格式: " + value);
                }
                continue;
            }
            if (lower.StartsWith("--format=", StringComparison.Ordinal))
            {
                string value = arg.Substring(9);
                if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    all = true;
                }
                else if (value.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioOnly = true;
                }
                else if (!TryParseFormat(value, out format))
                {
                    return UsageError("未知格式: " + value);
                }
                continue;
            }
            if (lower == "-o" || lower == "--out")
            {
                if (i + 1 >= args.Length)
                {
                    return UsageError("--out 缺少文件路径。");
                }
                i++;
                outputPath = args[i];
                continue;
            }
            if (lower.StartsWith("--out=", StringComparison.Ordinal))
            {
                outputPath = arg.Substring(6);
                continue;
            }
            if (lower == "-c" || lower == "--copy")
            {
                copy = true;
                continue;
            }
            return UsageError("未知参数: " + arg);
        }

        try
        {
            List<PortInfo> ports = UsbSerialReader.ReadPorts();
            List<AudioEndpointInfo> audio = AudioEndpointReader.ReadEndpoints(includeDisabled);
            PortGrouper.Build(ports, audio);
            string text;
            string label;
            if (all)
            {
                text = BuildAllText(ports, audio);
                label = "USB+声卡信息";
            }
            else if (audioOnly)
            {
                text = AudioReport.Build(audio, format);
                label = "声卡列表（" + PortReport.FormatName(format) + "）";
            }
            else
            {
                text = PortReport.Build(ports, audio, format);
                label = "USB 信息（" + PortReport.FormatName(format) + "）";
            }

            Console.WriteLine(text.TrimEnd('\r', '\n'));

            if (copy)
            {
                string error;
                if (ClipboardHelper.TrySetText(text, out error))
                {
                    WriteStatus("已复制" + label + "到剪贴板。");
                }
                else
                {
                    WriteStatus("复制失败：" + error);
                    return 1;
                }
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, text, new UTF8Encoding(true));
                WriteStatus("已写入: " + Path.GetFullPath(outputPath));
            }
            return 0;
        }
        catch (Exception ex)
        {
            WriteStatus("运行失败: " + ex.Message);
            return 1;
        }
    }

    private static string BuildAllText(List<PortInfo> ports, List<AudioEndpointInfo> audio)
    {
        string usb = PortReport.BuildTree(ports, audio).TrimEnd();
        string sound = string.Format("声卡列表（{0} 个端点）\r\n{1}",
            audio.Count, AudioReport.BuildTable(audio, true)).TrimEnd();
        return usb + "\r\n\r\n" + sound;
    }

    private static void PrepareOutput()
    {
        UTF8Encoding encoding = new UTF8Encoding(false);
        try
        {
            Console.OutputEncoding = encoding;
        }
        catch
        {
        }

        try
        {
            Stream stdout = Console.OpenStandardOutput();
            Stream stderr = Console.OpenStandardError();
            Console.SetOut(new StreamWriter(stdout, encoding) { AutoFlush = true });
            Console.SetError(new StreamWriter(stderr, encoding) { AutoFlush = true });
        }
        catch
        {
        }
    }

    private static bool TryParseFormat(string value, out PortReportFormat format)
    {
        switch (value.ToLowerInvariant())
        {
            case "tree":
            case "text":
                format = PortReportFormat.Tree;
                return true;
            case "plain":
            case "list":
                format = PortReportFormat.Plain;
                return true;
            case "md":
            case "markdown":
                format = PortReportFormat.Markdown;
                return true;
            case "csv":
                format = PortReportFormat.Csv;
                return true;
            case "detail":
            case "full":
                format = PortReportFormat.Detail;
                return true;
            default:
                format = PortReportFormat.Tree;
                return false;
        }
    }

    private static int UsageError(string message)
    {
        WriteStatus("错误: " + message);
        PrintHelp();
        return 2;
    }

    private static void WriteStatus(string message)
    {
        Console.Error.WriteLine(message);
    }

    private static void PrintHelp()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("usbTree USB 串口与声卡信息工具（只读）");
        text.AppendLine();
        text.AppendLine("用法:");
        text.AppendLine("  usbTree-cli.exe [选项]");
        text.AppendLine();
        text.AppendLine("选项:");
        text.AppendLine("  -a, --audio            只输出声卡列表");
        text.AppendLine("      --all              输出 USB 设备树 + 声卡列表");
        text.AppendLine("      --include-disabled 声卡列表包含非活动端点");
        text.AppendLine("  -f, --format <格式>    tree|plain|md|csv|detail|audio|all，默认 tree");
        text.AppendLine("  -o, --out <文件>       同时写入 UTF-8 文件");
        text.AppendLine("  -c, --copy             同时复制到剪贴板");
        text.AppendLine("  -l, --list             输出到终端（默认行为）");
        text.AppendLine("  -h, --help             显示本帮助");
        text.AppendLine();
        text.AppendLine("格式说明:");
        text.AppendLine("  tree    与 usbTree.exe 左侧树一致的板卡/设备/接口结构");
        text.AppendLine("  plain   每行一个串口，带 A/B/C/D 接口字母");
        text.AppendLine("  md      Markdown 表格");
        text.AppendLine("  csv     CSV 表格，可用 Excel 打开");
        text.AppendLine("  detail  每个串口和音频端点的完整信息");
        text.AppendLine("  audio   等同 --audio，输出声卡表");
        text.AppendLine("  all     等同 --all，输出 USB 树 + 声卡表");
        text.AppendLine();
        text.AppendLine("示例:");
        text.AppendLine("  usbTree-cli.exe");
        text.AppendLine("  usbTree-cli.exe --audio");
        text.AppendLine("  usbTree-cli.exe --audio --format csv --out audio.csv");
        text.AppendLine("  usbTree-cli.exe --all --out device.txt");
        text.AppendLine("  usbTree-cli.exe --format plain");
        text.AppendLine("  usbTree-cli.exe --format tree --copy");

        Console.WriteLine(text.ToString());
    }
}

public static class UsbTreeLauncher
{
    [STAThread]
    public static void RunGui()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    [STAThread]
    public static int RunCli(string[] args)
    {
        return CliProgram.Run(args ?? new string[0]);
    }
}
