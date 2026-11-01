using System.Runtime.InteropServices;

namespace Treemap.Core.Monitoring.Fps;

/// <summary>Finds the child processes of a process, using a snapshot of the system's process list.</summary>
public static class ProcessTree
{
    private const uint SnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>All processes started, directly or not, by <paramref name="rootProcessId"/> (the root is not included).</summary>
    public static IReadOnlyList<int> Descendants(int rootProcessId) =>
        Descendants(rootProcessId, ReadParentMap());

    /// <summary>Same search over an explicit child-to-parent map; separate so the traversal is testable.</summary>
    public static IReadOnlyList<int> Descendants(int rootProcessId, IReadOnlyDictionary<int, int> parentOf)
    {
        var childrenOf = new Dictionary<int, List<int>>();
        foreach (var (child, parent) in parentOf)
        {
            if (!childrenOf.TryGetValue(parent, out var list))
            {
                childrenOf[parent] = list = [];
            }

            list.Add(child);
        }

        var result = new List<int>();
        var seen = new HashSet<int> { rootProcessId };
        var queue = new Queue<int>();
        queue.Enqueue(rootProcessId);

        // The seen-set also protects against a recycled process id making the "tree" loop back on itself.
        while (queue.Count > 0)
        {
            if (!childrenOf.TryGetValue(queue.Dequeue(), out var children))
            {
                continue;
            }

            foreach (var child in children.Where(seen.Add))
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private static Dictionary<int, int> ReadParentMap()
    {
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle || snapshot == IntPtr.Zero)
        {
            return map;
        }

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>(), ExeFile = string.Empty };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
            {
                map[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            }
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }

        return map;
    }
}
