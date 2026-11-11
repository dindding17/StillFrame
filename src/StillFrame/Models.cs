using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StillFrame;

public sealed class Preferences
{
    public int SettingsVersion { get; set; }
    public double EdgeWidth { get; set; } = 0.19;
    public double Darkness { get; set; } = 0.42;
    public bool Mask { get; set; }
    public bool Dot { get; set; }
    public double DotSize { get; set; } = 4;
    public double ReferenceOpacity { get; set; } = 0.5;
    public string DotColor { get; set; } = "暖白";
    public bool Frame { get; set; }
    public bool Corners { get; set; }
    public bool SideGuides { get; set; }
    public bool Thirds { get; set; }
    public bool Ruler { get; set; }
    public bool OpenHorizon { get; set; }
    public bool SplitCross { get; set; }
    public bool Ring { get; set; }
    public bool DirectionLines { get; set; }
    public bool Triangles { get; set; }
    public bool Chevrons { get; set; }
    public bool Diamonds { get; set; }
    public bool TopEnabled { get; set; } = true;
    public bool BottomEnabled { get; set; } = true;
    public bool LeftEnabled { get; set; } = true;
    public bool RightEnabled { get; set; } = true;
    public string CustomColor { get; set; } = "";
    public string TopColor { get; set; } = "";
    public string BottomColor { get; set; } = "";
    public string LeftColor { get; set; } = "";
    public string RightColor { get; set; } = "";
    public double MarkerSize { get; set; } = 18;
    public bool FilledMarkers { get; set; }
    public double LineWidth { get; set; } = 1.5;
    public double LineInset { get; set; } = .08;
    public double RingRadius { get; set; } = 24;
    public string LinePattern { get; set; } = "实线";
    public bool Dynamic { get; set; }
    public double DynamicGain { get; set; } = 0.22;
    public int FirstReminder { get; set; } = 10;
    public int SecondReminder { get; set; } = 15;
    public string TargetProcess { get; set; } = "";
    public string Profile { get; set; } = "仅计时";
    public string GameNotes { get; set; } = "";
    public bool BlurOff { get; set; } = true;
    public bool ShakeOff { get; set; } = true;
    public bool SensitivityChecked { get; set; }
    public bool FramePacingChecked { get; set; }
    public bool ViewChecked { get; set; }
    public void Normalize()
    {
        EdgeWidth = Safe(EdgeWidth, .08, .34, .19); Darkness = Safe(Darkness, .1, .9, .42);
        DotSize = Safe(DotSize, 2, 12, 5); ReferenceOpacity = Safe(ReferenceOpacity, .1, 1, .65);
        DynamicGain = Safe(DynamicGain, .05, .4, .22);
        LineWidth = Safe(LineWidth, .75, 3, 1.5); LineInset = Safe(LineInset, .03, .18, .08);
        RingRadius = Safe(RingRadius, 8, 64, 24);
        MarkerSize = Safe(MarkerSize, 8, 48, 18);
        CustomColor = ReferenceColors.NormalizeHex(CustomColor); TopColor = ReferenceColors.NormalizeHex(TopColor);
        BottomColor = ReferenceColors.NormalizeHex(BottomColor); LeftColor = ReferenceColors.NormalizeHex(LeftColor); RightColor = ReferenceColors.NormalizeHex(RightColor);
        if (LinePattern is not ("实线" or "虚线" or "点线")) LinePattern = "实线";
        FirstReminder = Math.Clamp(FirstReminder, 1, 60); SecondReminder = Math.Clamp(SecondReminder, FirstReminder + 1, 90);
        TargetProcess ??= ""; Profile ??= "自定义"; GameNotes ??= "";
        if (DotColor is not ("薄荷绿" or "暖白" or "琥珀黄" or "雾蓝")) DotColor = "薄荷绿";
    }
    public void MigrateToCurrent()
    {
        if (SettingsVersion >= 2) return;
        Mask = Dot = Frame = Corners = SideGuides = Thirds = Ruler = OpenHorizon = SplitCross = Ring = DirectionLines = Triangles = Chevrons = Diamonds = Dynamic = false;
        Profile = "仅计时"; SettingsVersion = 2;
    }
    private static double Safe(double v, double min, double max, double fallback) => double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
}

public sealed class SessionRecord
{
    public DateTimeOffset StartedAt { get; set; }
    public string Game { get; set; } = "";
    public double ActiveSeconds { get; set; }
    public string Settings { get; set; } = "";
    public Preferences Configuration { get; set; } = new();
    public int? Discomfort { get; set; }
    public string Note { get; set; } = "";
}

public static class Store
{
    private static readonly Lazy<string> DataDirectory = new(() => Environment.GetEnvironmentVariable("STILLFRAME_DATA_DIR") is { Length: > 0 } customPath
        ? Path.GetFullPath(customPath) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StillFrame"));
    public static string DirectoryPath => DataDirectory.Value;
    public static string? Warning { get; private set; }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Preferences LoadPreferences()
    {
        var p = Read<Preferences>("settings.json") ?? new(); p.MigrateToCurrent(); p.Normalize(); return p;
    }
    public static List<SessionRecord> LoadSessions() => Read<List<SessionRecord>>("sessions.json") ?? new();
    private static T? Read<T>(string file)
    {
        var path = Path.Combine(DirectoryPath, file);
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Warning = "无法读取部分本机数据，已使用默认值。原文件已保留；下次保存将备份后替换。";
            return default;
        }
    }
    public static bool Save<T>(string file, T value)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, file), temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temp, path, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Warning = "数据保存失败：" + e.Message; return false; }
    }
    public static Preferences Snapshot(Preferences p) => JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!;
}

public sealed class PlaySession
{
    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public double Seconds { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    private bool firstSent, secondSent;
    public void Start() { Running = true; Paused = false; Seconds = 0; firstSent = secondSent = false; StartedAt = DateTimeOffset.Now; }
    public void TogglePause() { if (Running) Paused = !Paused; }
    public void Stop() { Running = false; Paused = false; }
    public int Tick(double elapsed, bool gameForeground, int first, int second)
    {
        if (!Running || Paused || !gameForeground || elapsed <= 0 || elapsed > 2) return 0;
        Seconds += elapsed;
        if (!secondSent && Seconds >= second * 60) { firstSent = secondSent = true; return 2; }
        if (!firstSent && Seconds >= first * 60) { firstSent = true; return 1; }
        return 0;
    }
}
