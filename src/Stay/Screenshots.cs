using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Stay;

/// <summary>Renders each pane of the window from the made-up PC to PNG files, for the README and the cards.</summary>
public static class Screenshots
{
    public static int Render(string dir, TextWriter o, bool dark = false)
    {
        Directory.CreateDirectory(dir);
        var app = Application.Current ?? new App();
        if (app.Resources.Count == 0) (app as App)?.InitializeComponent();
        Theme.Apply(app, dark);

        var window = new MainWindow(Demo.Engine())
        {
            Width = 1140, Height = 760,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000, Top = -20000, ShowInTaskbar = false,
        };
        window.Show();

        var panes = new[] { "stand", "patched", "eleven", "shut", "speed", "junk", "receipts" };
        for (int i = 0; i < panes.Length; i++)
        {
            window.ShowPaneForShot(i);
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            var bitmap = Capture((FrameworkElement)window.Content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(dir, panes[i] + (dark ? "-dark" : "") + ".png");
            using var file = File.Create(path);
            encoder.Save(file);
            o.WriteLine(path);
        }

        window.Close();
        return 0;
    }

    public static RenderTargetBitmap Capture(FrameworkElement root)
    {
        var bitmap = new RenderTargetBitmap((int)(root.ActualWidth * 2), (int)(root.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }
}
