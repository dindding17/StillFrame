using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace StillFrame;

internal sealed record GameWindow(IntPtr Handle, string Process, string Title)
{
    public override string ToString() => $"{Title}  ·  {Process}";
}

internal static class Native
{
    internal const int WM_HOTKEY = 0x312;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Gamepad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LX, LY, RX, RY; }
    [StructLayout(LayoutKind.Sequential)] private struct ControllerState { public uint Packet; public Gamepad Gamepad; }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out ControllerState state);

    internal static void MakeOverlay(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, style | 0x20 | 0x80 | 0x8000000); // click-through, tool window, no activation
    }
    internal static GameWindow? Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return null;
        var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
        if (text.Length == 0) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId) return null;
        try { using var process = Process.GetProcessById((int)pid); return new(hwnd, process.ProcessName, text.ToString()); }
        catch (Exception e) when (e is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { return null; }
    }
    internal static List<GameWindow> Windows()
    {
        var windows = new List<GameWindow>();
        EnumWindows((h, _) => { var w = Describe(h); if (w != null) windows.Add(w); return true; }, IntPtr.Zero);
        windows.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture)); return windows;
    }
    internal static bool ClientBounds(IntPtr hwnd, out Rect bounds)
    {
        bounds = default;
        if (IsIconic(hwnd) || !GetClientRect(hwnd, out var r)) return false;
        var pt = new Point(); if (!ClientToScreen(hwnd, ref pt)) return false;
        bounds = new Rect { Left = pt.X, Top = pt.Y, Right = pt.X + r.Width, Bottom = pt.Y + r.Height };
        return bounds.Width > 10 && bounds.Height > 10;
    }
    internal static (bool Connected, double Amount) RightStick()
    {
        bool connected = false; double amount = 0;
        for (uint i = 0; i < 4; i++)
        {
            if (XInputGetState(i, out var state) != 0) continue;
            connected = true;
            double radius = Math.Sqrt((double)state.Gamepad.RX * state.Gamepad.RX + (double)state.Gamepad.RY * state.Gamepad.RY);
            amount = Math.Max(amount, Math.Clamp((radius - 8689) / (32767 - 8689), 0, 1));
        }
        return (connected, amount);
    }
}
