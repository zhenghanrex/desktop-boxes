using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

internal static class Native {
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string name, string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder name, int size);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool SetWindowCompositionAttribute(IntPtr h, ref AttributeData data);
    [StructLayout(LayoutKind.Sequential)] struct Accent { public int State, Flags, Color, Animation; }
    [StructLayout(LayoutKind.Sequential)] struct AttributeData { public int Attribute; public IntPtr Data; public UIntPtr Size; }

    public static bool SetBlur(IntPtr h, bool enabled, Color tint, int alpha) {
        var accent = new Accent { State = enabled ? 3 : 0, Color = (alpha << 24) | (tint.B << 16) | (tint.G << 8) | tint.R };
        int size = Marshal.SizeOf(typeof(Accent));
        IntPtr p = Marshal.AllocHGlobal(size);
        try {
            Marshal.StructureToPtr(accent, p, false);
            var data = new AttributeData { Attribute = 19, Data = p, Size = (UIntPtr)(uint)size };
            return SetWindowCompositionAttribute(h, ref data);
        } catch (EntryPointNotFoundException) { return false; }
        finally { Marshal.FreeHGlobal(p); }
    }

    public static IntPtr Desktop() {
        uint explorer; GetWindowThreadProcessId(GetShellWindow(), out explorer);
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr unused) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != explorer) return true;
            var name = new StringBuilder(64);
            GetClassName(h, name, 64);
            if (name.ToString() != "Progman" && name.ToString() != "WorkerW") return true;
            var view = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view == IntPtr.Zero || FindWindowEx(view, IntPtr.Zero,"SysListView32",null) == IntPtr.Zero) return true;
            found = view; return false;
        }, IntPtr.Zero);
        return found;
    }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct ShellInfo {
        public IntPtr Icon; public int Index; public uint Attr;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string Display;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string Type;
    }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, out ShellInfo info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static Bitmap TypeIcon(string path, bool folder, bool small) {
        ShellInfo info;
        SHGetFileInfo(path, folder ? 16u : 128u, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x110u | (small ? 1u : 0u));
        if (info.Icon == IntPtr.Zero) return SystemIcons.Application.ToBitmap();
        try { return Icon.FromHandle(info.Icon).ToBitmap(); }
        finally { DestroyIcon(info.Icon); }
    }
}