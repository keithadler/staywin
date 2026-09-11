using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Grid = System.Windows.Controls.Grid;

namespace Stay;

/// <summary>
/// Four 1600x900 announcement cards, rendered at 2x from the screenshots the app has just taken of itself, so the
/// pictures in the post are the real window rather than a drawing of one.
/// </summary>
public static class Promo
{
    private sealed record Card(string File, string Headline, string Sub, string? Shot, string? Mono = null);

    private static readonly Card[] Cards =
    {
        new("1-hero", "Windows 10 stopped\nbeing supported.",
            "Your PC will not tell you where that leaves it. This will: whether Windows here is still being "
            + "patched, until when, and what to do about it.",
            "stand"),

        new("2-working", "Enrolled is not the same\nas actually getting them.",
            "It reads the day an update really installed, not what the licence claims. An enrolment that stopped "
            + "delivering is worse than none, because you think you are covered.",
            "patched"),

        new("3-faster", "Your PC took 94 seconds\nto start this morning.",
            "Windows times its own start, and times the programs holding it up, in a log almost nobody knows is "
            + "there. Real measurements, not guesses.",
            "speed"),

        new("4-free", "Every change on a receipt\nthat undoes it.",
            "Free, open source, one exe. No installer, no account, no cloud, no telemetry. It cannot patch "
            + "Windows, and says so.",
            null, Mono: "stay status"),
    };

    public static void Render(string dir, TextWriter o)
    {
        var promoDir = Path.Combine(dir, "promo");
        Directory.CreateDirectory(promoDir);

        foreach (var card in Cards)
        {
            var root = new Grid { Width = 1600, Height = 900 };

            // The colours of a PC that is still going: slate and a cold blue, not an alarm red. The app is not
            // trying to frighten anybody into anything.
            root.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromRgb(0x2C, 0x5F, 0x8A), 0),
                    new(Color.FromRgb(0x1D, 0x3D, 0x5C), 0.55),
                    new(Color.FromRgb(0x14, 0x22, 0x33), 1),
                },
                new Point(0, 0), new Point(1, 1));

            var brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(72, 56, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            try
            {
                brand.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Stay-256.png")),
                    Width = 56, Height = 56, Margin = new Thickness(0, 0, 18, 0),
                });
            }
            catch { /* the card is still a card without the icon */ }
            brand.Children.Add(new TextBlock
            {
                Text = "Stay for Windows 10",
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
            });
            root.Children.Add(brand);

            bool hasShot = card.Shot is not null;
            var text = new StackPanel
            {
                // The text column has to stop before the picture starts, or it runs underneath it. The picture
                // is 780 wide against the right edge with 44 to spare, so it begins at 776; the text is given
                // 620 from a left margin of 72 and so ends at 692.
                Margin = new Thickness(72, 40, hasShot ? 830 : 72, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = hasShot ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                Width = hasShot ? 620 : 1200,
            };
            text.Children.Add(new TextBlock
            {
                Text = card.Headline,
                FontSize = hasShot ? 47 : 62,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = hasShot ? 60 : 76,
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                TextAlignment = hasShot ? TextAlignment.Left : TextAlignment.Center,
            });
            text.Children.Add(new TextBlock
            {
                Text = card.Sub,
                FontSize = 25,
                Foreground = new SolidColorBrush(Color.FromArgb(0xE4, 0xFF, 0xFF, 0xFF)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 26, 0, 0),
                LineHeight = 36,
                TextAlignment = hasShot ? TextAlignment.Left : TextAlignment.Center,
            });
            if (card.Mono is not null)
            {
                var pill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(28, 14, 28, 14),
                    Margin = new Thickness(0, 44, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                pill.Child = new TextBlock
                {
                    Text = card.Mono,
                    FontSize = 28,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                };
                text.Children.Add(pill);
            }
            root.Children.Add(text);

            if (card.Shot is { } name)
            {
                var file = Path.Combine(dir, name + ".png");
                if (!File.Exists(file)) { o.WriteLine($"no screenshot named {name} for card {card.File}"); continue; }

                var shot = new BitmapImage();
                shot.BeginInit();
                shot.CacheOption = BitmapCacheOption.OnLoad;
                shot.UriSource = new Uri(Path.GetFullPath(file));
                shot.EndInit();

                root.Children.Add(new Border
                {
                    Width = 780,
                    Height = 500,
                    CornerRadius = new CornerRadius(14),
                    ClipToBounds = true,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 50, 44, 0),
                    // Aligned to the top left so the headline of the pane shows, rather than its middle.
                    Background = new ImageBrush(shot)
                    {
                        Stretch = Stretch.UniformToFill,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                    },
                    Effect = new DropShadowEffect { BlurRadius = 60, ShadowDepth = 18, Opacity = 0.5, Direction = 270 },
                });
            }

            root.Children.Add(new TextBlock
            {
                Text = "github.com/keithadler/staywin",
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 0, 72, 44),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            });

            root.Measure(new Size(1600, 900));
            root.Arrange(new Rect(0, 0, 1600, 900));
            root.UpdateLayout();

            var bmp = new RenderTargetBitmap(3200, 1800, 192, 192, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None }, null, new Rect(0, 0, 1600, 900));
            bmp.Render(visual);

            var outPath = Path.Combine(promoDir, card.File + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var stream = File.Create(outPath)) encoder.Save(stream);
            o.WriteLine($"wrote {outPath}");
        }
    }
}
