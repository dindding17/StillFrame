using StillFrame;

int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
Check(!ReferenceLayout.HasVisuals(new Preferences()), "Fresh install has no visual intervention");
var upgrade = new Preferences { Mask = true, Dot = true, Frame = true, Dynamic = true, GameNotes = "keep my notes", FirstReminder = 8 };
upgrade.MigrateToCurrent();
Check(upgrade.SettingsVersion == 2 && !ReferenceLayout.HasVisuals(upgrade) && !upgrade.Dynamic && upgrade.GameNotes == "keep my notes" && upgrade.FirstReminder == 8, "Upgrade disables old interventions while preserving personal notes and reminders");
upgrade.Corners = true; upgrade.MigrateToCurrent();
Check(upgrade.Corners, "Already migrated user selection persists");
var lineSettings = new Preferences { Corners = true };
var corners = ReferenceLayout.Create(lineSettings, 1920, 1080);
Check(corners.Count == 8 && corners.All(s => s.X1 >= 0 && s.X2 <= 1920 && s.Y1 >= 0 && s.Y2 <= 1080), "Corner geometry stays within viewport");
lineSettings.Corners = false; lineSettings.SideGuides = true;
var sides = ReferenceLayout.Create(lineSettings, 1920, 1080);
Check(sides.Count == 4 && sides.All(s => Math.Max(s.X1,s.X2) < 1920 * .3 || Math.Min(s.X1,s.X2) > 1920 * .7), "Side references leave the central field clear");
Check(ReferenceLayout.Create(lineSettings, double.NaN, 100).Count == 0 && ReferenceLayout.Create(lineSettings, 0, 100).Count == 0, "Invalid preview size yields no geometry");
foreach (string style in new[] { "四向短线", "四向三角", "方向折线", "菱形锚点" })
{
    var p = new Preferences(); ReferenceLayout.SelectPreset(p,style);
    int Count() => ReferenceLayout.Create(p,1920,1080).Count + ReferenceLayout.CreateMarkers(p,1920,1080).Count;
    Check(Count()==4,"Four-direction preset has exactly four elements: "+style);
    p.TopEnabled=false; p.LeftEnabled=false;
    Check(Count()==2 && ReferenceLayout.CreateMarkers(p,1920,1080).All(m => m.Side is ReferenceSide.Bottom or ReferenceSide.Right) && ReferenceLayout.Create(p,1920,1080).All(m => m.Side is ReferenceSide.Bottom or ReferenceSide.Right),"Per-direction switch filters exactly the requested sides: "+style);
    p.BottomEnabled=false;p.RightEnabled=false;
    Check(Count()==0,"All direction switches hide peripheral elements: "+style);
    ReferenceLayout.SelectPreset(p,"仅计时"); Check(!ReferenceLayout.HasVisuals(p),"All-off clears directional style: "+style);
}
var colorOptions = new Preferences { CustomColor="aabbcc",TopColor="#112233",RightColor="bad" };
colorOptions.Normalize();
Check(ReferenceColors.Resolve(colorOptions,ReferenceSide.Top)=="#112233" && ReferenceColors.Resolve(colorOptions,ReferenceSide.Right)=="#AABBCC","Per-direction color overrides global; invalid side color falls back");
Check(ReferenceColors.NormalizeHex("#12345678")=="" && ReferenceColors.NormalizeHex("#xyzxyz")=="" && ReferenceColors.NormalizeHex("# FFFFF")=="" && ReferenceColors.NormalizeHex("##123456")=="" && ReferenceColors.NormalizeHex(" #a1b2c3 ")=="#A1B2C3","Custom colors validate six hexadecimal digits");
var preservedDirections = Store.Snapshot(new Preferences { Triangles=true,DirectionLines=true,LeftEnabled=false,TopColor="#123456",FilledMarkers=true,MarkerSize=32 });
Check(preservedDirections.Triangles && preservedDirections.DirectionLines && !preservedDirections.LeftEnabled && preservedDirections.TopColor=="#123456" && preservedDirections.FilledMarkers && preservedDirections.MarkerSize==32,"Directional configuration round-trips independently");
foreach (double inset in new[] {.03,.08,.18}) foreach (double markerSize in new[] {8.0,48.0})
{
    var p = new Preferences { Triangles=true,LineInset=inset,MarkerSize=markerSize };
    var markers = ReferenceLayout.CreateMarkers(p,320,180);
    Check(markers.All(m => m.X-m.Size/2>=0 && m.X+m.Size/2<=320 && m.Y-m.Size/2>=0 && m.Y+m.Size/2<=180),"Directional markers stay inside small viewport at size "+markerSize+", inset "+inset);
}
foreach (string style in new[] { "边缘角标", "侧边短线", "三分刻度", "边缘标尺", "留空水平线", "中空十字", "中心空心环", "中心点与细框" })
{
    var p = new Preferences { Mask = true, Dynamic = true, Corners = true, Ruler = true, Ring = true };
    ReferenceLayout.SelectPreset(p, style);
    var segments = ReferenceLayout.Create(p, 1920, 1080);
    var circles = ReferenceLayout.CreateCircles(p, 1920, 1080);
    Check(ReferenceLayout.HasVisuals(p) && !p.Mask && !p.Dynamic && segments.All(s => s.X1 >= 0 && s.X1 <= 1920 && s.X2 >= 0 && s.X2 <= 1920 && s.Y1 >= 0 && s.Y1 <= 1080 && s.Y2 >= 0 && s.Y2 <= 1080), "Preset geometry is bounded and disables dimming: " + style);
    if (style == "留空水平线") Check(segments.Count == 2 && segments[0].X2 <= 1920 * .3 && segments[1].X1 >= 1920 * .7, "Open horizon leaves the middle 40 percent clear");
    if (style == "中心空心环") Check(segments.Count == 0 && circles.Count == 1 && circles[0].X == 960 && circles[0].Y == 540, "Ring preset is centered and contains no stale lines");
    ReferenceLayout.SelectPreset(p, "仅计时");
    Check(!ReferenceLayout.HasVisuals(p) && ReferenceLayout.Create(p,1920,1080).Count == 0 && ReferenceLayout.CreateCircles(p,1920,1080).Count == 0, "All-off clears style: " + style);
}
var settings = new Preferences { Dot = true, EdgeWidth = double.NaN, Darkness = 9, DotSize = -4, FirstReminder = 0, SecondReminder = -2, DotColor = "invalid", LineWidth = 99, LineInset = double.NaN };
settings.Normalize();
Check(settings.EdgeWidth == .19 && settings.Darkness == .9 && settings.DotSize == 2 && settings.FirstReminder == 1 && settings.SecondReminder == 2 && settings.DotColor == "薄荷绿", "Invalid settings recover within safe UI limits");
Check(settings.LineWidth == 3 && settings.LineInset == .08, "Reference line parameters normalize safely");
var newOptions = new Preferences { SettingsVersion = 2, Ring = true, Thirds = true, RingRadius = 999, LinePattern = "invalid", DotColor = "雾蓝" };
newOptions.Normalize();
Check(newOptions.RingRadius == 64 && newOptions.LinePattern == "实线" && newOptions.DotColor == "雾蓝", "New appearance parameters clamp and preserve valid color");
newOptions.LinePattern = "虚线";
var appearanceCopy = Store.Snapshot(newOptions);
Check(appearanceCopy.Ring && appearanceCopy.Thirds && appearanceCopy.LinePattern == "虚线" && appearanceCopy.RingRadius == 64, "New reference choices and line pattern persist in snapshots");
var snapshot = Store.Snapshot(settings); settings.Dot = false;
Check(snapshot.Dot && !settings.Dot, "Session settings snapshot is independent");
var session = new PlaySession();
Check(session.Tick(1, true, 1, 2) == 0 && session.Seconds == 0, "Idle session never counts");
session.Start();
for (int i = 0; i < 59; i++) CheckTick(1, true, 0);
Check(session.Seconds == 59, "Foreground seconds accumulate");
CheckTick(1, false, 0); session.TogglePause(); CheckTick(1, true, 0); session.TogglePause(); CheckTick(20, true, 0); CheckTick(-1, true, 0);
Check(session.Seconds == 59, "Background, pause, sleep gap and invalid delta do not count");
CheckTick(1, true, 1);
CheckTick(1, true, 0);
Check(session.Seconds == 61, "First reminder fires exactly once at threshold");
for (int i = 0; i < 58; i++) CheckTick(1, true, 0);
CheckTick(1, true, 2); CheckTick(1, true, 0);
Check(session.Seconds == 121, "Second reminder fires exactly once");
session.Stop(); CheckTick(1, true, 0);
Check(session.Seconds == 121 && !session.Running, "Stop preserves elapsed time without further counting");
session.Start();
Check(session.Seconds == 0 && !session.Paused, "New session resets elapsed time and pause");
for (int i = 0; i < 60; i++) session.Tick(1, true, 1, 2);
Check(session.Tick(1, true, 1, 2) == 0 && session.Seconds == 61, "New session resets reminder state");
Environment.SetEnvironmentVariable("STILLFRAME_DATA_DIR", Path.Combine(Path.GetTempPath(), "StillFrame-checks-" + Guid.NewGuid()));
// Store path is lazy until first use; avoid touching real user settings during verification.
Check(Store.Save("settings.json", snapshot), "Settings save atomically");
Check(Store.LoadPreferences().Darkness == .9, "Settings round-trip");
Check(Store.Save("settings.json", new Preferences()), "Settings update");
Check(File.Exists(Path.Combine(Store.DirectoryPath, "settings.json.bak")), "Previous settings backup preserved");
File.WriteAllText(Path.Combine(Store.DirectoryPath, "sessions.json"), "malformed json");
Check(Store.LoadSessions().Count == 0 && Store.Warning != null, "Corrupt history recovers without crash");
Check(File.ReadAllText(Path.Combine(Store.DirectoryPath, "sessions.json")) == "malformed json", "Corrupt original is retained");
var list = new List<SessionRecord> { new() { StartedAt = DateTimeOffset.Now, ActiveSeconds = 42, Game = "测试", Configuration = snapshot, Discomfort = null, Note = "走路时感觉" } };
Check(Store.Save("sessions.json", list), "History save");
var loaded = Store.LoadSessions();
Check(loaded.Count == 1 && loaded[0].Discomfort == null && loaded[0].Note == "走路时感觉" && loaded[0].Configuration.Dot, "Unrated history and full configuration round-trip");
Console.WriteLine($"All {count} checks passed. Test data: {Store.DirectoryPath}");
void CheckTick(double elapsed, bool foreground, int expected)
{
    int actual = session.Tick(elapsed, foreground, 1, 2);
    if (actual != expected) throw new Exception($"Unexpected reminder {actual}, expected {expected} at {session.Seconds}s");
}
