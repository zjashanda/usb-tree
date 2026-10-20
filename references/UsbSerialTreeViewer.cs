using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

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
    internal static readonly DevPropKey DeviceParent = new DevPropKey(
        new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), 8);
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
}

internal sealed class MainForm : Form
{
    private readonly TreeView tree;
    private readonly TextBox details;
    private readonly Label status;
    private readonly CheckBox autoRefresh;
    private readonly Timer timer;
    private string lastSignature = string.Empty;
    private List<PortInfo> currentPorts = new List<PortInfo>();

    public MainForm()
    {
        Text = "USB 串口树状查看器";
        Width = 1180;
        Height = 720;
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;

        Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 8, 8, 6) };
        Button refresh = new Button { Text = "立即刷新", Width = 90, Dock = DockStyle.Left };
        refresh.Click += delegate { RefreshPorts(true); };
        autoRefresh = new CheckBox { Text = "自动刷新（2秒）", Checked = true, Width = 130, Dock = DockStyle.Left };
        status = new Label { Text = "正在读取...", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        toolbar.Controls.Add(status);
        toolbar.Controls.Add(autoRefresh);
        toolbar.Controls.Add(refresh);

        SplitContainer split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 590,
            IsSplitterFixed = false
        };
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
        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(details);
        Controls.Add(split);
        Controls.Add(toolbar);

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
            string signature = BuildSignature(ports);
            if (!force && string.Equals(signature, lastSignature, StringComparison.Ordinal))
            {
                return;
            }
            lastSignature = signature;
            currentPorts = ports;
            BuildTree(ports);
            status.Text = string.Format("已刷新：{0} 个串口，{1} 个物理 USB 板卡。工具只读，不修改系统。",
                ports.Count, CountBoards(ports));
        }
        catch (Exception ex)
        {
            status.Text = "读取失败：" + ex.Message;
            details.Text = ex.ToString();
        }
    }

    private static int CountBoards(List<PortInfo> ports)
    {
        Dictionary<string, bool> groups = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (PortInfo port in ports)
        {
            groups[PhysicalKey(port)] = true;
        }
        return groups.Count;
    }

    private static string BuildSignature(List<PortInfo> ports)
    {
        StringBuilder value = new StringBuilder();
        foreach (PortInfo port in ports)
        {
            value.Append(port.InstanceId).Append('|')
                .Append(port.ComName).Append('|')
                .Append(port.Parent).Append('|')
                .Append(port.ContainerId).Append('|')
                .Append(port.FriendlyName).Append('\n');
        }
        return value.ToString();
    }

    private void BuildTree(List<PortInfo> ports)
    {
        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
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

            Dictionary<string, BoardGroup> boards = new Dictionary<string, BoardGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (PortGroup group in groups.Values)
            {
                string boardKey = PhysicalKey(group.Ports[0]);
                BoardGroup board;
                if (!boards.TryGetValue(boardKey, out board))
                {
                    board = new BoardGroup { Key = boardKey };
                    boards.Add(boardKey, board);
                }
                board.Devices.Add(group);
            }

            if (ports.Count == 0)
            {
                TreeNode emptyRoot = new TreeNode("USB 串口设备");
                emptyRoot.Nodes.Add(new TreeNode("未发现当前连接的串口设备。"));
                emptyRoot.Expand();
                tree.Nodes.Add(emptyRoot);
                tree.SelectedNode = emptyRoot;
                return;
            }

            List<BoardGroup> orderedBoards = new List<BoardGroup>(boards.Values);
            orderedBoards.Sort(delegate(BoardGroup left, BoardGroup right)
            {
                return BoardTitle(left).CompareTo(BoardTitle(right));
            });
            AssignBoardTitles(orderedBoards);

            TreeNode root;
            if (orderedBoards.Count == 1)
            {
                BoardGroup board = orderedBoards[0];
                root = CreateBoardNode(board);
                tree.Nodes.Add(root);
            }
            else
            {
                root = new TreeNode(string.Format("物理 USB 板卡（{0} 个）", orderedBoards.Count));
                tree.Nodes.Add(root);
                foreach (BoardGroup board in orderedBoards)
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
        List<PortGroup> devices = new List<PortGroup>(board.Devices);
        devices.Sort(delegate(PortGroup left, PortGroup right)
        {
            return left.Title.CompareTo(right.Title);
        });
        foreach (PortGroup group in devices)
        {
            TreeNode groupNode = new TreeNode(group.Title) { Tag = group };
            group.Ports.Sort(delegate(PortInfo left, PortInfo right)
            {
                return InterfaceOrder(left).CompareTo(InterfaceOrder(right));
            });
            foreach (PortInfo port in group.Ports)
            {
                groupNode.Nodes.Add(new TreeNode(PortTitle(port)) { Tag = port });
            }
            boardNode.Nodes.Add(groupNode);
        }
        return boardNode;
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

    private static string GroupKey(PortInfo port)
    {
        if (!string.IsNullOrEmpty(port.ContainerId))
        {
            return "C:" + port.ContainerId;
        }
        if (!string.IsNullOrEmpty(port.Parent))
        {
            return "P:" + port.Parent;
        }
        return "I:" + port.InstanceId;
    }

    private static string PhysicalKey(PortInfo port)
    {
        string paths = port.LocationPaths ?? string.Empty;
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

    private static string BoardTitle(BoardGroup board)
    {
        foreach (PortGroup device in board.Devices)
        {
            if (IsListenAi(device))
            {
                return "ListenAI 板卡";
            }
        }
        return board.Devices.Count == 1 ? "USB 板卡" : "USB 板卡（共享上游连接）";
    }

    private static string InterfaceLabel(PortInfo port)
    {
        Match match = Regex.Match(port.FriendlyName ?? string.Empty, @"SERIAL-([A-Z])", RegexOptions.IgnoreCase);
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

    private static string PortTitle(PortInfo port)
    {
        string label = InterfaceLabel(port);
        return string.IsNullOrEmpty(label) ? port.ComName : label + "  " + port.ComName;
    }

    private static int ComNumber(string comName)
    {
        int number;
        return int.TryParse(comName.Substring(3), out number) ? number : int.MaxValue;
    }

    private void TreeAfterSelect(object sender, TreeViewEventArgs e)
    {
        PortInfo port = e.Node.Tag as PortInfo;
        if (port != null)
        {
            details.Text = FormatPort(port);
            return;
        }
        BoardGroup board = e.Node.Tag as BoardGroup;
        if (board != null)
        {
            details.Text = FormatBoard(board);
            return;
        }
        PortGroup group = e.Node.Tag as PortGroup;
        if (group != null)
        {
            details.Text = FormatGroup(group);
            return;
        }
        details.Text = "选择左侧的 USB 设备组或串口查看详细 PnP 信息。\r\n\r\n" +
            "分组优先依据 Container ID，其次使用父设备；这与 Windows 设备枚举关系一致。";
    }

    private static string FormatBoard(BoardGroup board)
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
        text.AppendLine();
        text.AppendLine("说明: 该层依据共享的 USB 上游位置路径建立，仅用于本工具可视化，不改变 Windows PnP 枚举。");
        return text.ToString();
    }

    private static string FormatGroup(PortGroup group)
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

    private static string FormatPort(PortInfo port)
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
        text.AppendLine(port.LocationPaths);
        return text.ToString();
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
