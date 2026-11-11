using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace StillFrame;

public partial class MainWindow : Window
{
    private readonly Preferences preferences = Store.LoadPreferences();
    private readonly List<SessionRecord> records = Store.LoadSessions();
    private readonly PlaySession session = new();
    private readonly OverlayWindow overlay = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private GameWindow? target;
    private HwndSource? source;
    private bool ready, quitting, overlayEnabled = true;
    private double lastTime, motion, previewUntil, toastUntil, nextControllerPoll, nextUiUpdate;
    private bool controllerConnected;
    private string? toast;
    private bool wasGameForeground;
    private const int previewDuration = 15;
    private readonly List<int> hotkeys = new();
    private string shortcutPrefix = "Ctrl + Alt + ";

    public MainWindow(bool renderOnly = false)
    {
        InitializeComponent();
        using (var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/StillFrame.ico")).Stream)
        using (var sourceIcon = new System.Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize))
            trayIcon = (System.Drawing.Icon)sourceIcon.Clone();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "静界 · 游戏舒适辅助", Visible = !renderOnly };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开静界", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("隐藏 / 恢复辅助", null, (_, _) => Dispatcher.Invoke(ToggleVisual));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        LoadControls();
        ScorePicker.Items.Add("未评分"); for (int i = 0; i <= 10; i++) ScorePicker.Items.Add(i == 0 ? "0 · 无不适" : i == 10 ? "10 · 非常严重" : i.ToString());
        ScorePicker.SelectedIndex = 0;
        DataPathText.Text = "本机数据：" + Store.DirectoryPath;
        ready = true; if (!renderOnly) RefreshWindowList(); RefreshHistory();
        timer.Tick += Tick; saveTimer.Tick += (_, _) => { saveTimer.Stop(); Persist(); };
        if (!renderOnly) Loaded += (_, _) => { RegisterKeys(); lastTime = clock.Elapsed.TotalSeconds; timer.Start(); if (Store.Warning != null) SetStatus(Store.Warning); };
        Closing += OnClosing;
        Application.Current.Exit += (_, _) => Cleanup();
    }

    private void LoadControls()
    {
        bool previous = ready; ready = false;
        MaskCheck.IsChecked = preferences.Mask; DotCheck.IsChecked = preferences.Dot; FrameCheck.IsChecked = preferences.Frame;
        DynamicCheck.IsChecked = preferences.Dynamic; WidthSlider.Value = preferences.EdgeWidth; DarknessSlider.Value = preferences.Darkness;
        SizeSlider.Value = preferences.DotSize; OpacitySlider.Value = preferences.ReferenceOpacity; GainSlider.Value = preferences.DynamicGain;
        ColorPicker.SelectedIndex = preferences.DotColor switch { "暖白" => 1, "琥珀黄" => 2, "雾蓝" => 3, _ => 0 };
        FirstReminderInput.Text = preferences.FirstReminder.ToString(); SecondReminderInput.Text = preferences.SecondReminder.ToString();
        GameNotesInput.Text = preferences.GameNotes; BlurCheck.IsChecked = preferences.BlurOff; ShakeCheck.IsChecked = preferences.ShakeOff;
        SensitivityCheck.IsChecked = preferences.SensitivityChecked; PacingCheck.IsChecked = preferences.FramePacingChecked; ViewCheck.IsChecked = preferences.ViewChecked;
        CornersCheck.IsChecked = preferences.Corners; SideGuidesCheck.IsChecked = preferences.SideGuides;
        LineWidthSlider.Value = preferences.LineWidth; InsetSlider.Value = preferences.LineInset;
        ThirdsCheck.IsChecked = preferences.Thirds; RulerCheck.IsChecked = preferences.Ruler; HorizonCheck.IsChecked = preferences.OpenHorizon;
        CrossCheck.IsChecked = preferences.SplitCross; RingCheck.IsChecked = preferences.Ring; RingSizeSlider.Value = preferences.RingRadius;
        PatternPicker.SelectedIndex = preferences.LinePattern switch { "虚线" => 1, "点线" => 2, _ => 0 };
        DirectionLinesCheck.IsChecked = preferences.DirectionLines; TrianglesCheck.IsChecked = preferences.Triangles;
        ChevronsCheck.IsChecked = preferences.Chevrons; DiamondsCheck.IsChecked = preferences.Diamonds;
        TopDirectionCheck.IsChecked = preferences.TopEnabled; BottomDirectionCheck.IsChecked = preferences.BottomEnabled;
        LeftDirectionCheck.IsChecked = preferences.LeftEnabled; RightDirectionCheck.IsChecked = preferences.RightEnabled;
        GlobalColorInput.Text = preferences.CustomColor; TopColorInput.Text = preferences.TopColor; BottomColorInput.Text = preferences.BottomColor;
        LeftColorInput.Text = preferences.LeftColor; RightColorInput.Text = preferences.RightColor;
        MarkerSizeSlider.Value = preferences.MarkerSize; FilledCheck.IsChecked = preferences.FilledMarkers;
        ready = previous; UpdateLabels(); overlay.RefreshSettings();
    }
    private void UpdateLabels()
    {
        WidthLabel.Text = $"每侧遮罩范围  {preferences.EdgeWidth:P0}";
        DarknessLabel.Text = $"边缘深浅  {preferences.Darkness:P0}";
        SizeLabel.Text = $"参考点直径  {preferences.DotSize:0} DIP";
        OpacityLabel.Text = $"参考元素不透明度  {preferences.ReferenceOpacity:P0}";
        GainLabel.Text = $"转镜头最大加深  +{preferences.DynamicGain:P0}";
        LineWidthLabel.Text = $"线条宽度  {preferences.LineWidth:0.##} DIP";
        InsetLabel.Text = $"距屏幕边缘  {preferences.LineInset:P0}";
        RingSizeLabel.Text = $"空心环半径  {preferences.RingRadius:0} DIP";
        MarkerSizeLabel.Text = $"方向标记尺寸  {preferences.MarkerSize:0} DIP";
        bool visuals = ReferenceLayout.HasVisuals(preferences);
        string mode = visuals ? string.Join(" + ", new[] { preferences.DirectionLines ? "四向短线" : null, preferences.Triangles ? "三角标" : null, preferences.Chevrons ? "方向折线" : null, preferences.Diamonds ? "菱形锚点" : null, preferences.Corners ? "角标" : null, preferences.SideGuides ? "侧边短线" : null, preferences.Thirds ? "三分刻度" : null, preferences.Ruler ? "边缘标尺" : null, preferences.OpenHorizon ? "留空水平线" : null, preferences.SplitCross ? "中空十字" : null, preferences.Ring ? "空心环" : null, preferences.Dot ? "中心点" : null, preferences.Frame ? "细框" : null, preferences.Mask ? "变暗" : null }.Where(x => x != null)) : "原画面 · 无辅助";
        PresetText.Text = $"当前：{preferences.Profile}。调整只影响你开启的元素，预览不累计游戏时间。";
        HeaderBadge.Text = !visuals ? "视觉辅助 · 全部关闭" : overlayEnabled ? "视觉辅助 · 按需启用" : "视觉辅助 · 已暂时隐藏";
        OverlayButton.Content = overlayEnabled ? "隐藏辅助" : "恢复辅助";
        HomePreviewTitle.Text = mode; PreviewDescription.Text = mode;
        HomePreviewNote.Text = !visuals ? "默认无辅助，保留原画面。" : preferences.Mask ? "变暗已开启，可在高级选项关闭。" : "固定参考，保持原亮度。";
        ActiveProfileText.Text = preferences.Profile;
        HomePreview.Update(preferences); ComfortPreview.Update(preferences);
        DynamicCheck.IsEnabled = preferences.Mask;
        foreach (var (button,side) in new[] { (TopColorButton,ReferenceSide.Top),(BottomColorButton,ReferenceSide.Bottom),(LeftColorButton,ReferenceSide.Left),(RightColorButton,ReferenceSide.Right),(GlobalColorButton,ReferenceSide.Center) })
        {
            string hex = ReferenceColors.Resolve(preferences,side);
            button.Content = new System.Windows.Shapes.Ellipse { Width=16,Height=16,Fill=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),Stroke=new SolidColorBrush(Color.FromRgb(122,147,164)),StrokeThickness=1 };
            button.ToolTip = "选择颜色 · " + hex;
        }
        foreach (Button tile in StyleGallery.Children)
        {
            if ((string)tile.Tag == preferences.Profile)
            { tile.Background = new SolidColorBrush(Color.FromRgb(226,242,247)); tile.BorderBrush = new SolidColorBrush(Color.FromRgb(82,143,163)); }
            else { tile.ClearValue(Button.BackgroundProperty); tile.ClearValue(Button.BorderBrushProperty); }
        }
    }
    private void CaptureControls(bool custom = true)
    {
        if (!ready) return;
        preferences.Mask = MaskCheck.IsChecked == true; preferences.Dot = DotCheck.IsChecked == true; preferences.Frame = FrameCheck.IsChecked == true;
        preferences.Dynamic = DynamicCheck.IsChecked == true; preferences.EdgeWidth = WidthSlider.Value; preferences.Darkness = DarknessSlider.Value;
        preferences.DotSize = SizeSlider.Value; preferences.ReferenceOpacity = OpacitySlider.Value; preferences.DynamicGain = GainSlider.Value;
        preferences.DotColor = (ColorPicker.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "薄荷绿";
        preferences.Corners = CornersCheck.IsChecked == true; preferences.SideGuides = SideGuidesCheck.IsChecked == true;
        preferences.LineWidth = LineWidthSlider.Value; preferences.LineInset = InsetSlider.Value;
        preferences.Thirds = ThirdsCheck.IsChecked == true; preferences.Ruler = RulerCheck.IsChecked == true;
        preferences.OpenHorizon = HorizonCheck.IsChecked == true; preferences.SplitCross = CrossCheck.IsChecked == true; preferences.Ring = RingCheck.IsChecked == true;
        preferences.RingRadius = RingSizeSlider.Value;
        preferences.LinePattern = (PatternPicker.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "实线";
        preferences.DirectionLines = DirectionLinesCheck.IsChecked == true; preferences.Triangles = TrianglesCheck.IsChecked == true;
        preferences.Chevrons = ChevronsCheck.IsChecked == true; preferences.Diamonds = DiamondsCheck.IsChecked == true;
        preferences.TopEnabled = TopDirectionCheck.IsChecked == true; preferences.BottomEnabled = BottomDirectionCheck.IsChecked == true;
        preferences.LeftEnabled = LeftDirectionCheck.IsChecked == true; preferences.RightEnabled = RightDirectionCheck.IsChecked == true;
        preferences.MarkerSize = MarkerSizeSlider.Value; preferences.FilledMarkers = FilledCheck.IsChecked == true;
        if (!preferences.Mask) { preferences.Dynamic = false; DynamicCheck.IsChecked = false; }
        preferences.SettingsVersion = 2;
        if (custom) preferences.Profile = "自定义";
        UpdateLabels(); overlay.RefreshSettings(); ScheduleSave();
    }
    private void SettingsChanged(object sender, RoutedEventArgs e) => CaptureControls();
    private void PaletteChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        preferences.CustomColor = ""; GlobalColorInput.Text = ""; CaptureControls();
    }
    private void ColorChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        foreach (var field in new[] { GlobalColorInput, TopColorInput, BottomColorInput, LeftColorInput, RightColorInput })
        {
            string valid = ReferenceColors.NormalizeHex(field.Text);
            if (!string.IsNullOrWhiteSpace(field.Text) && valid.Length == 0)
            {
                SetStatus("颜色应为 #RRGGBB，例如 #A8D8EA；留空可跟随全局色。请修正标记的输入。");
                field.BorderBrush = new SolidColorBrush(Color.FromRgb(176,85,62)); return;
            }
            field.ClearValue(TextBox.BorderBrushProperty);
        }
        preferences.CustomColor = ReferenceColors.NormalizeHex(GlobalColorInput.Text);
        preferences.TopColor = ReferenceColors.NormalizeHex(TopColorInput.Text); preferences.BottomColor = ReferenceColors.NormalizeHex(BottomColorInput.Text);
        preferences.LeftColor = ReferenceColors.NormalizeHex(LeftColorInput.Text); preferences.RightColor = ReferenceColors.NormalizeHex(RightColorInput.Text);
        CaptureControls();
    }
    private void ChooseColor(object sender, RoutedEventArgs e)
    {
        string name = (string)((Button)sender).Tag;
        TextBox field = name switch { "Top" => TopColorInput, "Bottom" => BottomColorInput, "Left" => LeftColorInput, "Right" => RightColorInput, _ => GlobalColorInput };
        using var picker = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(ReferenceColors.NormalizeHex(field.Text) is { Length: > 0 } hex ? hex : ReferenceColors.Resolve(preferences)) };
        if (picker.ShowDialog(new WindowOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
        field.Text = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"; ColorChanged(field,new RoutedEventArgs());
    }
    private sealed class WindowOwner : Forms.IWin32Window
    { public WindowOwner(IntPtr handle) => Handle = handle; public IntPtr Handle { get; } }
    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => CaptureControls();
    private void ScheduleSave() { saveTimer.Stop(); saveTimer.Start(); }
    private void Persist()
    {
        if (!Store.Save("settings.json", preferences)) SetStatus(Store.Warning ?? "设置保存失败");
    }
    private void Preset(object sender, RoutedEventArgs e)
    {
        string name = (string)((Button)sender).Tag;
        ReferenceLayout.SelectPreset(preferences, name);
        LoadControls(); ScheduleSave(); SetStatus(name == "仅计时" ? "所有画面辅助已关闭，仍可使用计时和休息提醒。" : "已选择" + name + "，边缘变暗保持关闭。先在预览中比较。");
    }
    private void Navigate(object sender, RoutedEventArgs e) => ShowPage((string)((FrameworkElement)sender).Tag);
    private void OpenComfort(object sender, RoutedEventArgs e) => ShowPage("Comfort");
    private void CompareDown(object sender, MouseButtonEventArgs e)
    { OriginalButton.CaptureMouse(); ComfortPreview.ShowOriginal(true); e.Handled = true; }
    private void CompareUp(object sender, MouseButtonEventArgs e)
    { ComfortPreview.ShowOriginal(false); OriginalButton.ReleaseMouseCapture(); e.Handled = true; }
    private void CompareLost(object sender, RoutedEventArgs e) => ComfortPreview.ShowOriginal(false);
    private void CompareKeyDown(object sender, KeyEventArgs e)
    { if (e.Key is Key.Space or Key.Return) { ComfortPreview.ShowOriginal(true); e.Handled = true; } }
    private void CompareKeyUp(object sender, KeyEventArgs e)
    { if (e.Key is Key.Space or Key.Return) { ComfortPreview.ShowOriginal(false); e.Handled = true; } }
    private void ShowPage(string page)
    {
        HomeNav.IsChecked = page == "Home"; ComfortNav.IsChecked = page == "Comfort"; GuideNav.IsChecked = page == "Guide"; HistoryNav.IsChecked = page == "History"; HelpNav.IsChecked = page == "Help";
        HomePanel.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        ComfortPanel.Visibility = page == "Comfort" ? Visibility.Visible : Visibility.Collapsed;
        GuidePanel.Visibility = page == "Guide" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = page == "History" ? Visibility.Visible : Visibility.Collapsed;
        HelpPanel.Visibility = page == "Help" ? Visibility.Visible : Visibility.Collapsed;
        (PageTitle.Text, PageSubtitle.Text) = page switch
        {
            "Comfort" => ("画面实验室", "先在这里比较，再决定要不要启用。"),
            "Guide" => ("游戏设置", "一次调整一项，留下对你有用的设置。"),
            "History" => ("体验记录", "短时体验，逐项比较，不必坚持到不适。"),
            "Help" => ("使用与快捷键", "了解操作、兼容性与本机数据。"),
            _ => ("概览与计时", "保持原画面，从一次轻松的短时体验开始。")
        };
    }
    private void RefreshWindows(object sender, RoutedEventArgs e) => RefreshWindowList();
    private void RefreshWindowList()
    {
        if (session.Running) { SetStatus("请先结束当前体验，再更换游戏窗口。"); return; }
        var windows = Native.Windows();
        bool previous = ready; ready = false;
        WindowPicker.ItemsSource = windows;
        WindowPicker.SelectedItem = windows.FirstOrDefault(w => target != null && w.Handle == target.Handle)
            ?? windows.FirstOrDefault(w => string.Equals(w.Process, preferences.TargetProcess, StringComparison.OrdinalIgnoreCase));
        ready = previous;
        if (WindowPicker.SelectedItem is GameWindow selected) BindTarget(selected); else { target = null; TargetText.Text = "尚未选择游戏；启动游戏后刷新，或使用前台窗口绑定快捷键。"; }
    }
    private void SelectWindow(object sender, SelectionChangedEventArgs e)
    { if (ready && !session.Running && WindowPicker.SelectedItem is GameWindow selected) BindTarget(selected); }
    private void BindTarget(GameWindow selected)
    {
        target = selected; preferences.TargetProcess = selected.Process;
        TargetText.Text = $"已选择：{selected.Title}  ·  {selected.Process}.exe";
        SetStatus("已绑定窗口。开始后切回游戏，辅助层才会显示。"); ScheduleSave();
    }
    private void StartSession(object sender, RoutedEventArgs e)
    {
        if (session.Running) return;
        if (target == null || !Native.IsWindow(target.Handle)) { SetStatus("请先选择仍在运行的游戏窗口。"); return; }
        ReminderChanged(this, new RoutedEventArgs());
        previewUntil = 0; toast = null; toastUntil = 0; motion = 0; wasGameForeground = false; overlayEnabled = true;
        session.Start(); WindowPicker.IsEnabled = false; StartButton.IsEnabled = false; PauseButton.IsEnabled = StopButton.IsEnabled = true;
        SetStatus("已开始。请切回游戏；只有游戏在前台时才显示辅助和累计时间。"); UpdateLabels();
    }
    private void PauseSession(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        if (!session.Running) { SetStatus("尚未开始体验。"); return; }
        session.TogglePause(); PauseButton.Content = session.Paused ? "继续计时" : "暂停计时";
        SetStatus(session.Paused ? "计时暂停；画面辅助继续。如需休息，请主动暂停游戏。" : "计时已继续。");
    }
    private void StopSession(object sender, RoutedEventArgs e)
    { if (session.Running) { CompleteSession(); ShowPage("History"); SetStatus("已保存未评分体验。可在充分休息后补充感受。"); } }
    private void CompleteSession()
    {
        if (!session.Running) return;
        records.Insert(0, new SessionRecord
        {
            StartedAt = session.StartedAt, Game = target?.Process ?? preferences.TargetProcess, ActiveSeconds = session.Seconds,
            Configuration = Store.Snapshot(preferences),
            Settings = $"末次配置：{preferences.Profile}；四向线{preferences.DirectionLines} / 三角{preferences.Triangles} / 折线{preferences.Chevrons} / 菱形{preferences.Diamonds}；角标{(preferences.Corners ? "开" : "关")}；侧边短线{(preferences.SideGuides ? "开" : "关")}；三分刻度{(preferences.Thirds ? "开" : "关")}；标尺{(preferences.Ruler ? "开" : "关")}；留空水平线{(preferences.OpenHorizon ? "开" : "关")}；中空十字{(preferences.SplitCross ? "开" : "关")}；空心环{(preferences.Ring ? "开" : "关")}；参考点{(preferences.Dot ? "开" : "关")}；细框{(preferences.Frame ? "开" : "关")}；变暗{(preferences.Mask ? "开" : "关")}；上{preferences.TopEnabled} 下{preferences.BottomEnabled} 左{preferences.LeftEnabled} 右{preferences.RightEnabled}；{preferences.LinePattern} / 线宽{preferences.LineWidth:0.##} / 透明度{preferences.ReferenceOpacity:P0}；结束时辅助{(overlayEnabled ? "启用" : "隐藏")}"
        });
        session.Stop(); overlay.Hide(); previewUntil = toastUntil = 0; toast = null;
        WindowPicker.IsEnabled = StartButton.IsEnabled = true; PauseButton.IsEnabled = StopButton.IsEnabled = false; PauseButton.Content = "暂停计时";
        SaveRecords(); RefreshHistory();
    }
    private void Tick(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds, elapsed = now - lastTime; lastTime = now;
        IntPtr foreground = Native.GetForegroundWindow();
        bool gameForeground = target != null && foreground == target.Handle && !Native.IsIconic(target.Handle);
        // Count only intervals whose two sampled endpoints were both in the game.
        int reminder = session.Tick(elapsed, gameForeground && wasGameForeground, preferences.FirstReminder, preferences.SecondReminder);
        wasGameForeground = gameForeground;
        if (reminder > 0)
        {
            toast = reminder == 1 ? $"已游玩 {preferences.FirstReminder} 分钟，给眼睛和身体一点休息。" : $"已游玩 {preferences.SecondReminder} 分钟。建议暂停；有不适就结束。";
            toastUntil = now + 9;
        }
        if (now >= nextControllerPoll)
        {
            var state = Native.RightStick(); controllerConnected = state.Connected;
            requestedMotion = preferences.Dynamic && gameForeground ? state.Amount : 0;
            nextControllerPoll = now + (preferences.Dynamic && gameForeground ? .05 : 1);
        }
        if (!preferences.Dynamic || !gameForeground) requestedMotion = 0;
        double targetMotion = requestedMotion;
        motion += (targetMotion - motion) * (1 - Math.Exp(-Math.Min(Math.Max(elapsed, 0), .1) / (targetMotion > motion ? .22 : .45)));
        bool preview = now < previewUntil;
        bool showToast = now < toastUntil;
        bool show = preview || session.Running && gameForeground && overlayEnabled;
        Native.Rect bounds = default;
        if (preview)
        {
            var screen = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).Bounds;
            bounds = new Native.Rect { Left = screen.Left, Top = screen.Top, Right = screen.Right, Bottom = screen.Bottom };
        }
        else if (show && target != null) show = Native.ClientBounds(target.Handle, out bounds);
        if (show)
        {
            bool hasVisuals = ReferenceLayout.HasVisuals(preferences);
            if (hasVisuals || showToast || preview) overlay.Update(preferences, preferences.Dynamic ? motion : 0, bounds, showToast ? toast : preview ? $"画面预览 · 不累计游戏时间 · {shortcutPrefix}F11 隐藏" : null);
            else if (overlay.IsVisible) overlay.Hide();
        }
        else if (overlay.IsVisible) overlay.Hide();
        if (now >= nextUiUpdate)
        {
            nextUiUpdate = now + .5;
            var duration = TimeSpan.FromSeconds(session.Seconds); TimerText.Text = duration.TotalHours >= 1 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");
            SessionPill.Text = !session.Running ? "尚未开始" : session.Paused ? "计时暂停" : gameForeground ? "正在游玩" : "等待游戏";
            SessionProgress.Value = Math.Min(100, session.Seconds / (preferences.SecondReminder * 60) * 100);
            NextReminderText.Text = session.Seconds >= preferences.SecondReminder * 60 ? "已到休息时间，建议暂停一下。" : session.Seconds >= preferences.FirstReminder * 60 ? $"再次提醒 {preferences.SecondReminder} 分钟 / 首次提醒已送达" : $"首次提醒 {preferences.FirstReminder} 分钟 / 再次提醒 {preferences.SecondReminder} 分钟";
            SessionStatus.Text = !session.Running ? "准备好后，选择游戏并开始辅助。" : target == null || !Native.IsWindow(target.Handle) ? "游戏窗口已关闭，请结束并记录。" : session.Paused ? "计时已暂停 · 画面辅助仍可使用" : gameForeground ? $"游戏在前台 · 辅助{(overlayEnabled ? "启用" : "隐藏")}" : "等待切回游戏 · 暂不累计时间";
            ControllerText.Text = controllerConnected ? "已识别 XInput 手柄。" : "未识别 XInput 手柄；固定参考元素不受影响。";
        }
    }
    private double requestedMotion;
    private void ToggleOverlay(object sender, RoutedEventArgs e) => ToggleVisual();
    private void ToggleVisual()
    {
        overlayEnabled = !overlayEnabled; previewUntil = 0; if (!overlayEnabled) overlay.Hide(); UpdateLabels();
        SetStatus(overlayEnabled ? "辅助画面已恢复；开始体验并切回游戏后显示。" : $"辅助画面已隐藏，计时继续。{shortcutPrefix}F9 可恢复。");
    }
    private void Preview(object sender, RoutedEventArgs e)
    { overlayEnabled = true; previewUntil = clock.Elapsed.TotalSeconds + previewDuration; UpdateLabels(); SetStatus($"预览 15 秒；可继续调整参数。{shortcutPrefix}F11 立即隐藏。"); }
    private void TestReminder(object sender, RoutedEventArgs e)
    { previewUntil = toastUntil = clock.Elapsed.TotalSeconds + 8; toast = "休息提醒预览 · 提醒不会自动暂停游戏。"; SetStatus("正在预览提醒，不会改变游戏计时。"); }
    private void ReminderChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        if (!int.TryParse(FirstReminderInput.Text, out int first) || !int.TryParse(SecondReminderInput.Text, out int second) || first < 1 || first > 60 || second <= first || second > 90)
        { FirstReminderInput.Text = preferences.FirstReminder.ToString(); SecondReminderInput.Text = preferences.SecondReminder.ToString(); SetStatus("提醒时间需为整数：首次 1～60 分钟，再次大于首次且不超过 90 分钟。已恢复原值。"); return; }
        preferences.FirstReminder = first; preferences.SecondReminder = second; ScheduleSave();
    }
    private void GuideChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        preferences.GameNotes = GameNotesInput.Text; preferences.BlurOff = BlurCheck.IsChecked == true; preferences.ShakeOff = ShakeCheck.IsChecked == true;
        preferences.SensitivityChecked = SensitivityCheck.IsChecked == true; preferences.ViewChecked = ViewCheck.IsChecked == true; preferences.FramePacingChecked = PacingCheck.IsChecked == true;
        ScheduleSave(); SetStatus("游戏设置笔记已更新，保存在本机。");
    }
    private sealed record HistoryItem(SessionRecord Record)
    { public override string ToString() => $"{Record.StartedAt.LocalDateTime:MM-dd HH:mm} · {Record.Game} · {TimeSpan.FromSeconds(Record.ActiveSeconds).TotalMinutes:0.#} 分钟 · {(Record.Discomfort.HasValue ? Record.Discomfort + " 分" : "未评分")}"; }
    private void RefreshHistory(SessionRecord? selected = null)
    {
        HistoryPicker.ItemsSource = records.Select(r => new HistoryItem(r)).ToList();
        HistoryPicker.SelectedIndex = selected == null ? records.Count > 0 ? 0 : -1 : records.IndexOf(selected);
        if (records.Count == 0) RecordDetails.Text = "还没有体验记录。开始并结束一次体验后，会自动创建未评分记录。";
    }
    private void HistorySelected(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryPicker.SelectedItem is not HistoryItem item) return;
        RecordDetails.Text = item.Record.Settings; ScorePicker.SelectedIndex = item.Record.Discomfort.HasValue ? item.Record.Discomfort.Value + 1 : 0;
        RecordNoteInput.Text = item.Record.Note;
    }
    private void SaveRating(object sender, RoutedEventArgs e)
    {
        if (HistoryPicker.SelectedItem is not HistoryItem item) { SetStatus("请先结束一次体验，生成记录。"); return; }
        item.Record.Discomfort = ScorePicker.SelectedIndex <= 0 ? null : ScorePicker.SelectedIndex - 1;
        item.Record.Note = RecordNoteInput.Text; if (SaveRecords()) { RefreshHistory(item.Record); SetStatus("评价已保存。"); }
    }
    private bool SaveRecords() { bool ok = Store.Save("sessions.json", records); if (!ok) SetStatus(Store.Warning ?? "记录保存失败"); return ok; }
    private void ExportHistory(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "静界体验记录.csv" };
        if (dialog.ShowDialog(this) != true) return;
        static string Cell(string value)
        {
            // Prevent spreadsheet formula execution when opening exported user notes.
            if (value.Length > 0 && "=+-@\t\r\n".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("开始时间,游戏进程,前台秒数,末次配置,不适程度0到10,笔记\r\n");
        foreach (var r in records) csv.AppendLine(string.Join(",", Cell(r.StartedAt.ToString("o")), Cell(r.Game), r.ActiveSeconds.ToString("F1", CultureInfo.InvariantCulture), Cell(r.Settings), r.Discomfort?.ToString() ?? "", Cell(r.Note)));
        try { File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true)); SetStatus("已导出体验记录。"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus("导出失败：" + error.Message); }
    }
    private void RegisterKeys()
    {
        var handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(handle); source?.AddHook(WindowMessage);
        void RegisterGroup(uint modifiers)
        { for (int id = 8; id <= 11; id++) if (Native.RegisterHotKey(handle, id, modifiers, (uint)(0x70 + id - 1))) hotkeys.Add(id); }
        RegisterGroup(0x4003);
        if (hotkeys.Count != 4)
        {
            foreach (int key in hotkeys) Native.UnregisterHotKey(handle, key);
            hotkeys.Clear(); shortcutPrefix = "Ctrl + Alt + Shift + "; RegisterGroup(0x4007);
        }
        ShortcutList.Text = $"{shortcutPrefix}F8     绑定当前前台窗口（未开始时）\n{shortcutPrefix}F9     隐藏 / 恢复辅助画面\n{shortcutPrefix}F10   暂停 / 继续计时\n{shortcutPrefix}F11   立即隐藏画面并取消预览";
        BindHint.Text = $"也可切到游戏后按 {shortcutPrefix}F8 绑定当前窗口。";
        HotkeyText.Text = hotkeys.Count == 4
            ? (shortcutPrefix.Contains("Shift") ? "默认组合被占用，已整组切换为上方带 Shift 的备用组合。" : "四个快捷键均已注册。") + "紧急隐藏不改变计时状态。"
            : "部分快捷键被占用：" + string.Join("、", Enumerable.Range(8, 4).Except(hotkeys).Select(i => shortcutPrefix + "F" + i)) + "。请使用窗口或托盘操作。";
        if (hotkeys.Count != 4 || shortcutPrefix.Contains("Shift")) SetStatus(HotkeyText.Text);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != Native.WM_HOTKEY) return IntPtr.Zero;
        handled = true;
        switch (wParam.ToInt32())
        {
            case 8:
                if (session.Running) { SetStatus("请先结束体验，再绑定其他窗口。"); break; }
                var current = Native.Describe(Native.GetForegroundWindow());
                if (current != null) { BindTarget(current); RefreshWindowList(); tray.ShowBalloonTip(2500, "静界", "已绑定：" + current.Title + "。回到静界点击开始辅助。", Forms.ToolTipIcon.Info); }
                break;
            case 9: ToggleVisual(); break;
            case 10: TogglePause(); break;
            case 11: overlayEnabled = false; previewUntil = toastUntil = 0; overlay.Hide(); UpdateLabels(); SetStatus("已立即隐藏辅助画面。计时状态保持；F9 可恢复。"); break;
        }
        return IntPtr.Zero;
    }
    private void SetStatus(string message) => StatusText.Text = message;
    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (quitting) return;
        e.Cancel = true; Hide(); tray.ShowBalloonTip(2000, "静界仍在运行", "从通知区域图标打开，或右键退出。", Forms.ToolTipIcon.Info);
    }
    private void OpenData(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(Store.DirectoryPath); Process.Start(new ProcessStartInfo(Store.DirectoryPath) { UseShellExecute = true }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception) { SetStatus("无法打开目录：" + error.Message); }
    }
    private void ExitApp(object sender, RoutedEventArgs e) => Quit();
    private void Quit()
    {
        if (quitting) return;
        ColorChanged(this,new RoutedEventArgs());
        if (session.Running) CompleteSession(); GuideChanged(this, new RoutedEventArgs()); Persist();
        quitting = true; Cleanup(); Application.Current.Shutdown();
    }
    private bool cleaned;
    internal void ExportDesignScreens(string directory)
    {
        saveTimer.Stop();
        FirstReminderInput.Text = "10"; SecondReminderInput.Text = "15";
        TargetText.Text = "尚未选择游戏窗口"; BindHint.Text = "也可切到游戏后，使用全局快捷键绑定。";
        void Render(string filename, string page, double width = 1260, double height = 800)
        {
            ShowPage(page);
            var root = (FrameworkElement)Content;
            root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, filename)); png.Save(stream);
        }
        Render("01-overview.png", "Home");
        Render("02-lab-off.png", "Comfort");
        preferences.Corners = true; preferences.SideGuides = false; preferences.Profile = "边缘角标"; LoadControls();
        Render("03-lab-corners.png", "Comfort");
        preferences.Corners = false; preferences.SideGuides = true; preferences.Profile = "侧边短线"; LoadControls();
        Render("04-lab-side-guides.png", "Comfort");
        Render("05-overview-compact.png", "Home", 1080, 700);
        Render("06-game-settings.png", "Guide");
        Render("07-records.png", "History");
        ShortcutList.Text = "Ctrl + Alt + F8     绑定当前窗口\nCtrl + Alt + F9     隐藏 / 恢复辅助\nCtrl + Alt + F10   暂停 / 继续计时\nCtrl + Alt + F11   立即隐藏并取消预览";
        HotkeyText.Text = "默认组合被占用时，将整组尝试带 Shift 的备用组合。";
        Render("08-help.png", "Help");
        ReferenceLayout.SelectPreset(preferences, "三分刻度"); LoadControls(); Render("09-thirds.png", "Comfort");
        ReferenceLayout.SelectPreset(preferences, "边缘标尺"); LoadControls(); Render("10-ruler.png", "Comfort");
        ReferenceLayout.SelectPreset(preferences, "留空水平线"); LoadControls(); Render("11-open-horizon.png", "Comfort");
        ReferenceLayout.SelectPreset(preferences, "中空十字"); LoadControls(); Render("12-split-cross.png", "Comfort");
        ReferenceLayout.SelectPreset(preferences, "中心空心环"); LoadControls(); Render("13-ring.png", "Comfort");
        ReferenceLayout.SelectPreset(preferences,"四向三角");
        preferences.TopColor = "#B6D8EE"; preferences.BottomColor = "#E8C99B"; preferences.LeftColor = "#B8DFCA"; preferences.RightColor = "#D7C5E9";
        preferences.ReferenceOpacity = .8; preferences.FilledMarkers = true; LoadControls(); Render("14-direction-triangles.png","Comfort",1360,840);
        InspectorTabs.SelectedIndex=1; Render("18-direction-colors.png","Comfort",1360,840);
        var layoutAudit = new List<string>();
        void AuditDirectionRows(string viewport)
        {
          foreach (var (toggle,field,button,label) in new[] { (TopDirectionCheck,TopColorInput,TopColorButton,"上"),(BottomDirectionCheck,BottomColorInput,BottomColorButton,"下"),(LeftDirectionCheck,LeftColorInput,LeftColorButton,"左"),(RightDirectionCheck,RightColorInput,RightColorButton,"右") })
          {
            double Center(FrameworkElement element) => element.TranslatePoint(new Point(0,element.ActualHeight/2),(UIElement)Content).Y;
            double drift = Math.Max(Math.Abs(Center(toggle)-Center(field)),Math.Abs(Center(field)-Center(button)));
            layoutAudit.Add($"{viewport} {label}: checkbox/input/color button center drift = {drift:0.###} DIP; input width = {field.ActualWidth:0.###} DIP");
            if (drift > 1) throw new InvalidOperationException("Direction row lost vertical alignment: "+label);
            if (field.ActualWidth < 100) throw new InvalidOperationException("Color input is too narrow for HEX text.");
          }
        }
        AuditDirectionRows("1360x840");
        InspectorTabs.SelectedIndex=2; Render("19-parameters.png","Comfort",1360,840); InspectorTabs.SelectedIndex=0;
        Render("15-lab-compact.png","Comfort",1120,720);
        InspectorTabs.SelectedIndex=1; Render("20-colors-compact.png","Comfort",1120,720); AuditDirectionRows("1120x720");
        InspectorTabs.SelectedIndex=2; Render("21-parameters-compact.png","Comfort",1120,720); InspectorTabs.SelectedIndex=0;
        File.WriteAllLines(Path.Combine(directory,"layout-audit.txt"),layoutAudit);
        ReferenceLayout.SelectPreset(preferences,"方向折线"); LoadControls(); Render("16-chevrons.png","Comfort",1360,840);
        ReferenceLayout.SelectPreset(preferences,"四向短线"); LoadControls(); Render("17-four-direction-lines.png","Comfort",1360,840);
        quitting = true;
    }
    private void Cleanup()
    {
        if (cleaned) return; cleaned = true;
        timer.Stop(); saveTimer.Stop();
        var handle = new WindowInteropHelper(this).Handle;
        foreach (int key in hotkeys) Native.UnregisterHotKey(handle, key);
        source?.RemoveHook(WindowMessage); overlay.Close(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose();
    }
}
