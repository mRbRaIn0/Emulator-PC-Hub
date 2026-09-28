using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EmulatorPCHub.App.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Quadratischer Cover-Zuschnitt wie die Kacheln im Hauptmenü: Bild verschieben (Maus ziehen / Pfeil-Tasten)
/// und zoomen (Mausrad / +/−). Liefert ein quadratisches PNG in einer temporären Datei oder null.
/// </summary>
public static class CoverCropDialog
{
    private const double View = 380;
    private const double MaxZoom = 4;
    private const uint MaxOutput = 1024;

    public static async Task<string?> ShowAsync(string sourceFile)
    {
        uint w, h;
        using (var stream = await OpenAsync(sourceFile))
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            (w, h) = (decoder.PixelWidth, decoder.PixelHeight);
        }
        if (w == 0 || h == 0)
            return null;

        var baseScale = View / Math.Min(w, h);
        var zoom = 1.0;
        var scale = baseScale;
        double ox = 0, oy = 0;

        var image = new Image
        {
            Source = new BitmapImage(new Uri(sourceFile)) { CreateOptions = BitmapCreateOptions.IgnoreImageCache },
            Stretch = Stretch.Fill,
        };
        var canvas = new Canvas
        {
            Width = View,
            Height = View,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)),
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, View, View) },
        };
        canvas.Children.Add(image);

        void Apply()
        {
            scale = baseScale * zoom;
            ox = Math.Clamp(ox, View - w * scale, 0);
            oy = Math.Clamp(oy, View - h * scale, 0);
            image.Width = w * scale;
            image.Height = h * scale;
            Canvas.SetLeft(image, ox);
            Canvas.SetTop(image, oy);
        }

        void Center()
        {
            zoom = 1;
            scale = baseScale;
            ox = (View - w * scale) / 2;
            oy = (View - h * scale) / 2;
            Apply();
        }

        // Zoomen um einen Punkt (Standard: Mitte des Rahmens), damit der Ausschnitt nicht springt.
        void ZoomBy(double factor, double cx = View / 2, double cy = View / 2)
        {
            var old = scale;
            zoom = Math.Clamp(zoom * factor, 1, MaxZoom);
            var next = baseScale * zoom;
            ox = cx - (cx - ox) * (next / old);
            oy = cy - (cy - oy) * (next / old);
            Apply();
        }

        void Pan(double dx, double dy)
        {
            ox += dx;
            oy += dy;
            Apply();
        }

        Center();

        // Maus / Touch: ziehen und Mausrad
        Windows.Foundation.Point? last = null;
        canvas.PointerPressed += (_, e) =>
        {
            last = e.GetCurrentPoint(canvas).Position;
            canvas.CapturePointer(e.Pointer);
            e.Handled = true;
        };
        canvas.PointerMoved += (_, e) =>
        {
            if (last is not { } p)
                return;
            var now = e.GetCurrentPoint(canvas).Position;
            Pan(now.X - p.X, now.Y - p.Y);
            last = now;
        };
        canvas.PointerReleased += (_, e) =>
        {
            last = null;
            canvas.ReleasePointerCapture(e.Pointer);
        };
        canvas.PointerCaptureLost += (_, _) => last = null;
        canvas.PointerWheelChanged += (_, e) =>
        {
            var pt = e.GetCurrentPoint(canvas);
            ZoomBy(pt.Properties.MouseWheelDelta > 0 ? 1.1 : 1 / 1.1, pt.Position.X, pt.Position.Y);
            e.Handled = true;
        };

        var frame = new Border
        {
            Child = canvas,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(3),
            BorderBrush = Ui.AccentBrush,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // Controller-Steuerung: Schaltflächen, die mit A gedrückt werden
        const double step = View * 0.06;
        Button Small(string glyph, Action onClick)
        {
            var b = new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 18 },
                Style = (Style)Application.Current.Resources["HubButton"],
                Width = 56,
                Height = 56,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(28),
            };
            b.Click += (_, _) => onClick();
            return b;
        }

        var pad = new Grid { RowSpacing = 6, ColumnSpacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            pad.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            pad.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }
        void Place(UIElement el, int row, int col)
        {
            Grid.SetRow((FrameworkElement)el, row);
            Grid.SetColumn((FrameworkElement)el, col);
            pad.Children.Add(el);
        }
        // Pfeile verschieben den Ausschnitt (das Bild bewegt sich entgegengesetzt)
        Place(Small("", () => Pan(0, step)), 0, 1);
        Place(Small("", () => Pan(step, 0)), 1, 0);
        Place(Small("", Center), 1, 1);
        Place(Small("", () => Pan(-step, 0)), 1, 2);
        Place(Small("", () => Pan(0, -step)), 2, 1);

        var zoomRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        zoomRow.Children.Add(Small("", () => ZoomBy(1 / 1.15)));
        zoomRow.Children.Add(Small("", () => ZoomBy(1.15)));

        var side = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Width = 190 };
        side.Children.Add(Ui.Subtle("Ausschnitt verschieben"));
        side.Children.Add(pad);
        side.Children.Add(Ui.Subtle("Zoom"));
        side.Children.Add(zoomRow);
        side.Children.Add(Ui.Subtle("Maus: ziehen zum Verschieben, Mausrad zum Zoomen."));

        var layout = new Grid { ColumnSpacing = 24 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(frame);
        Grid.SetColumn(side, 1);
        layout.Children.Add(side);

        var result = await Dialogs.ShowAsync("Cover zuschneiden", layout, "Übernehmen", "Abbrechen");
        if (result != ContentDialogResult.Primary)
            return null;

        // Ausschnitt in Bildpixeln
        var cropSide = View / scale;
        var cropX = -ox / scale;
        var cropY = -oy / scale;
        return await CropAsync(sourceFile, w, h, cropX, cropY, cropSide);
    }

    private static async Task<IRandomAccessStream> OpenAsync(string file)
    {
        var sf = await StorageFile.GetFileFromPathAsync(file);
        return await sf.OpenReadAsync();
    }

    private static async Task<string> CropAsync(string sourceFile, uint w, uint h, double x, double y, double side)
    {
        var output = (uint)Math.Clamp(Math.Round(side), 1, MaxOutput);
        var f = output / side;
        var scaledW = Math.Max(output, (uint)Math.Round(w * f));
        var scaledH = Math.Max(output, (uint)Math.Round(h * f));
        var bx = (uint)Math.Clamp(Math.Round(x * f), 0, scaledW - output);
        var by = (uint)Math.Clamp(Math.Round(y * f), 0, scaledH - output);

        using var input = await OpenAsync(sourceFile);
        var decoder = await BitmapDecoder.CreateAsync(input);
        var transform = new BitmapTransform
        {
            ScaledWidth = scaledW,
            ScaledHeight = scaledH,
            InterpolationMode = BitmapInterpolationMode.Fant,
            Bounds = new BitmapBounds { X = bx, Y = by, Width = output, Height = output },
        };
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.ColorManageToSRgb);

        var target = Path.Combine(Path.GetTempPath(), $"hub-cover-{Guid.NewGuid():N}.png");
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(target)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(target), CreationCollisionOption.ReplaceExisting);
        using (var outStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, output, output, 96, 96,
                pixels.DetachPixelData());
            await encoder.FlushAsync();
        }
        return target;
    }
}
