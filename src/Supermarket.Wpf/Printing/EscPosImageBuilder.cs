using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Supermarket.Wpf.Printing;

/// <summary>
/// Renders a plain text receipt (plus optional store logo) into the 1-bit raster bitmap
/// expected by an ESC/POS thermal printer.
/// </summary>
public static class EscPosImageBuilder
{
    /// <summary>Printable width of 80 mm paper at 203 dpi.</summary>
    public const int PrinterWidthDots = 576;

    /// <summary>Rows per raster stripe emitted by the ESC/POS command.</summary>
    private const int SliceHeight = 24;

    /// <summary>
    /// Renders receipt text and an optional logo into a 1-bit black &amp; white bitmap for thermal printing.
    /// </summary>
    public static BitmapSource RenderReceipt(
        string text,
        string? logoPath = null,
        string fontFamily = "Tahoma",
        double fontSize = 20)
    {
        var typeface = new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formatted = BuildFormattedText(text, typeface, fontSize);

        BitmapImage? logo = null;
        double logoHeight = 0;
        double logoWidth = 0;

        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try
            {
                logo = new BitmapImage();
                logo.BeginInit();
                logo.UriSource = new Uri(Path.GetFullPath(logoPath), UriKind.Absolute);
                logo.CacheOption = BitmapCacheOption.OnLoad;
                logo.EndInit();
                logo.Freeze();

                var maxLogoW = 280d;
                var maxLogoH = 120d;
                var scale = Math.Min(maxLogoW / logo.PixelWidth, maxLogoH / logo.PixelHeight);
                if (scale > 1.0) scale = 1.0;

                logoWidth = Math.Max(1, logo.PixelWidth * scale);
                logoHeight = Math.Max(1, logo.PixelHeight * scale);
            }
            catch
            {
                logo = null;
                logoHeight = 0;
            }
        }

        var topOffset = logo != null ? logoHeight + 16 : 8;
        var totalHeight = (int)Math.Ceiling(formatted.Height + topOffset) + 16;
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, PrinterWidthDots, totalHeight));

            if (logo != null)
            {
                var logoX = (PrinterWidthDots - logoWidth) / 2d;
                context.DrawImage(logo, new Rect(logoX, 8, logoWidth, logoHeight));
            }

            context.DrawText(formatted, new Point(4, topOffset));
        }

        var bitmap = new RenderTargetBitmap(PrinterWidthDots, Math.Max(totalHeight, 1), 203, 203, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        // Convert to 1 bit per pixel: the thermal head only knows black or white.
        var converted = new FormatConvertedBitmap();
        converted.BeginInit();
        converted.Source = bitmap;
        converted.DestinationFormat = PixelFormats.BlackWhite;
        converted.EndInit();
        converted.Freeze();

        return converted;
    }

    /// <summary>
    /// Converts a bitmap into the byte stream of <c>GS v 0</c> (print raster bit image) commands,
    /// sliced so that no single command exceeds the printer buffer.
    /// </summary>
    public static byte[] BuildRasterCommands(BitmapSource bitmap)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = (width + 7) / 8;

        var pixels = new byte[width * height];
        bitmap.CopyPixels(pixels, stride, 0);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        for (var y = 0; y < height; y += SliceHeight)
        {
            var rows = Math.Min(SliceHeight, height - y);
            var slice = new byte[stride * rows];

            for (var row = 0; row < rows; row++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y + row) * stride + (x / 8);
                    var bit = (pixels[index] >> (7 - (x % 8))) & 1;
                    // Black pixels must be a set bit in the ESC/POS raster format.
                    if (bit == 0)
                    {
                        slice[row * stride + (x / 8)] |= (byte)(0x80 >> (x % 8));
                    }
                }
            }

            // GS v 0 m xL xH yL yH data
            writer.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00 });
            writer.Write((byte)(stride & 0xFF));
            writer.Write((byte)((stride >> 8) & 0xFF));
            writer.Write((byte)(rows & 0xFF));
            writer.Write((byte)((rows >> 8) & 0xFF));
            writer.Write(slice);
        }

        // Feed paper
        writer.Write(new byte[] { 0x1B, 0x64, 0x04 });
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>Builds the formatted text block used by both the printer and the on-screen preview.</summary>
    public static FormattedText BuildFormattedText(string text, Typeface typeface, double fontSize, Brush? foreground = null)
    {
        return new FormattedText(
            text ?? string.Empty,
            CultureInfo.GetCultureInfo("ar-EG"),
            FlowDirection.RightToLeft,
            typeface,
            fontSize,
            foreground ?? Brushes.Black,
            1.0)
        {
            MaxTextWidth = PrinterWidthDots - 8,
            Trimming = TextTrimming.None,
            LineHeight = fontSize * 1.35
        };
    }
}
