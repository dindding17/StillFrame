using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fixture;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application();
        var window = new Window { Title = "StillFrame 验证场景（非游戏）", Width = 1100, Height = 720, Background = new SolidColorBrush(Color.FromRgb(82, 113, 130)), WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var grid = new Grid();
        for (int i = 0; i < 8; i++) { grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.RowDefinitions.Add(new RowDefinition()); }
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
        {
            var cell = new Border { Background = new SolidColorBrush((x + y) % 2 == 0 ? Color.FromRgb(95, 128, 145) : Color.FromRgb(72, 104, 125)), BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), BorderThickness = new Thickness(.5) };
            Grid.SetColumn(cell, x); Grid.SetRow(cell, y); grid.Children.Add(cell);
        }
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "STILLFRAME / LOCAL TEST SCENE", FontSize = 27, Foreground = Brushes.White, Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "验证覆盖对齐、点击穿透、前台计时与快捷键。\n这是本机静态测试窗口，不模拟实际游戏性能。", FontSize = 17, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, Margin = new Thickness(20) });
        var button = new Button { Content = "点击穿透测试 · 0", FontSize = 20, Padding = new Thickness(30, 20, 30, 20), Margin = new Thickness(20) };
        int clicks = 0; button.Click += (_, _) => button.Content = "点击穿透测试 · " + ++clicks;
        panel.Children.Add(button); Grid.SetColumnSpan(panel, 8); Grid.SetRowSpan(panel, 8); grid.Children.Add(panel);
        window.Content = grid; app.Run(window);
    }
}
