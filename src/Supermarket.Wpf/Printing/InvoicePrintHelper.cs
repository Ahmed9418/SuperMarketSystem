using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Wpf.Printing;

/// <summary>
/// Helper for generating and printing invoices and test pages across all supported paper sizes
/// (80mm Thermal, A4 Standard Tax Invoice, and A5 Compact Invoice).
/// </summary>
public static class InvoicePrintHelper
{
    private static readonly byte[] InitCommands = { 0x1B, 0x40 };
    private static readonly byte[] CutCommands = { 0x1B, 0x64, 0x04, 0x1D, 0x56, 0x01 };

    public static string ResolvePrinterName(string? directName, string? settingsName)
    {
        if (!string.IsNullOrWhiteSpace(directName)) return directName;
        if (!string.IsNullOrWhiteSpace(settingsName)) return settingsName;
        return RawPrinterHelper.GetDefaultPrinterName() ?? string.Empty;
    }

    /// <summary>
    /// Executes a test print matching the store's configured paper size and logo.
    /// </summary>
    public static async Task<PrintResult> PrintTestAsync(
        StoreSetting settings,
        string? printerName = null,
        CancellationToken cancellationToken = default)
    {
        var paper = settings.InvoicePaperSize?.Trim().ToUpperInvariant() ?? "80MM";
        var printer = ResolvePrinterName(printerName, settings.ReceiptPrinterName);

        if (paper == "A4" || paper == "A5")
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var doc = CreateTestDocument(settings, isA5: paper == "A5");
                    var printDlg = new PrintDialog();

                    if (!string.IsNullOrWhiteSpace(printer))
                    {
                        try
                        {
                            var ps = new LocalPrintServer();
                            printDlg.PrintQueue = ps.GetPrintQueue(printer);
                        }
                        catch
                        {
                            // fallback to user choice in PrintDialog if queue name didn't match
                        }
                    }

                    var idoc = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDlg.PrintDocument(idoc, $"اختبار فاتورة {paper} - {settings.StoreName}");
                    return PrintResult.Ok(printDlg.PrintQueue?.Name ?? printer);
                }
                catch (Exception ex)
                {
                    return PrintResult.Fail($"تعذرت طباعة الفاتورة ({paper}): {ex.Message}", printer);
                }
            });
        }

        // Default: 80mm Thermal Print
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(printer))
            {
                return PrintResult.Fail("لم يتم العثور على طابعة. يرجى اختيار طابعة أو تعيين طابعة افتراضية في ويندوز.");
            }

            try
            {
                var text = BuildTestReceiptText(settings);
                var bitmap = EscPosImageBuilder.RenderReceipt(text, settings.LogoPath);
                var raster = EscPosImageBuilder.BuildRasterCommands(bitmap);

                var payload = new byte[InitCommands.Length + raster.Length + CutCommands.Length];
                Buffer.BlockCopy(InitCommands, 0, payload, 0, InitCommands.Length);
                Buffer.BlockCopy(raster, 0, payload, InitCommands.Length, raster.Length);
                Buffer.BlockCopy(CutCommands, 0, payload, InitCommands.Length + raster.Length, CutCommands.Length);

                RawPrinterHelper.SendBytes(printer, payload, $"TestReceipt-80mm-{DateTime.Now:yyyyMMddHHmmss}");
                return PrintResult.Ok(printer);
            }
            catch (Exception ex)
            {
                return PrintResult.Fail($"تعذرت الطباعة الحرارية 80 مم: {ex.Message}", printer);
            }
        }, cancellationToken);
    }

    public static string BuildTestReceiptText(StoreSetting settings)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine($"           {settings.StoreName}           ");
        if (!string.IsNullOrWhiteSpace(settings.Address))
            sb.AppendLine($"العنوان: {settings.Address}");
        if (!string.IsNullOrWhiteSpace(settings.PhoneNumber))
            sb.AppendLine($"الهاتف: {settings.PhoneNumber}");
        if (!string.IsNullOrWhiteSpace(settings.TaxNumber))
            sb.AppendLine($"الرقم الضريبي: {settings.TaxNumber}");
        sb.AppendLine("========================================");
        sb.AppendLine("         ** تجربة طباعة إيصال **        ");
        sb.AppendLine($"التاريخ: {DateTime.Now:yyyy/MM/dd HH:mm}");
        sb.AppendLine("نوع الفاتورة: إيصال حراري 80 مم");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("الصنف               الكمية     السعر");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("أرز مصري فاخر 1 كجم    2      66.00");
        sb.AppendLine("زيت عباد شمس 1 لتر     1      78.00");
        sb.AppendLine("جبنة رومي فاخرة        0.5   142.50");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("الإجمالي قبل الضريبة:       251.32 ج.م");
        sb.AppendLine("ضريبة القيمة المضافة:        35.18 ج.م");
        sb.AppendLine("الإجمالي النهائي:           286.50 ج.م");
        sb.AppendLine("========================================");
        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooterMessage))
        {
            sb.AppendLine(settings.ReceiptFooterMessage);
            sb.AppendLine("========================================");
        }
        sb.AppendLine("إذا كانت الحروف واضحة فالطابعة جاهزة للعمل!");
        return sb.ToString();
    }

    private static FlowDocument CreateTestDocument(StoreSetting settings, bool isA5)
    {
        var doc = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Segoe UI, Tahoma"),
            PagePadding = isA5 ? new Thickness(24) : new Thickness(40),
            PageWidth = isA5 ? 559 : 793,
            PageHeight = isA5 ? 793 : 1122,
            ColumnWidth = double.PositiveInfinity
        };

        // Header Table (Logo + Store Details)
        var headerTable = new Table();
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(140) });
        var headerRowGroup = new TableRowGroup();
        var headerRow = new TableRow();

        // Left: Store metadata
        var metaCell = new TableCell();
        metaCell.Blocks.Add(new Paragraph(new Run(settings.StoreName))
        {
            FontSize = isA5 ? 18 : 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (!string.IsNullOrWhiteSpace(settings.Address))
            metaCell.Blocks.Add(new Paragraph(new Run($"العنوان: {settings.Address}")) { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        if (!string.IsNullOrWhiteSpace(settings.PhoneNumber))
            metaCell.Blocks.Add(new Paragraph(new Run($"الهاتف: {settings.PhoneNumber}")) { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        if (!string.IsNullOrWhiteSpace(settings.TaxNumber))
            metaCell.Blocks.Add(new Paragraph(new Run($"الرقم الضريبي: {settings.TaxNumber}")) { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });

        headerRow.Cells.Add(metaCell);

        // Right: Logo or Title
        var logoCell = new TableCell { TextAlignment = TextAlignment.Left };
        if (!string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(Path.GetFullPath(settings.LogoPath), UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();

                var img = new Image
                {
                    Source = bi,
                    MaxWidth = isA5 ? 100 : 130,
                    MaxHeight = isA5 ? 70 : 90,
                    Stretch = Stretch.Uniform,
                    FlowDirection = FlowDirection.LeftToRight
                };
                logoCell.Blocks.Add(new BlockUIContainer(img));
            }
            catch
            {
                logoCell.Blocks.Add(new Paragraph(new Run("فاتورة تجريبية")) { FontSize = 16, FontWeight = FontWeights.Bold });
            }
        }
        else
        {
            logoCell.Blocks.Add(new Paragraph(new Run(isA5 ? "فاتورة تجريبية A5" : "فاتورة تجريبية A4"))
            {
                FontSize = isA5 ? 14 : 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray
            });
        }
        headerRow.Cells.Add(logoCell);
        headerRowGroup.Rows.Add(headerRow);
        headerTable.RowGroups.Add(headerRowGroup);
        doc.Blocks.Add(headerTable);

        // Divider
        doc.Blocks.Add(new BlockUIContainer(new Separator { Margin = new Thickness(0, 10, 0, 14), Background = Brushes.LightGray }));

        // Invoice Meta Info
        var invoiceMeta = new Paragraph
        {
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 14),
            Background = new SolidColorBrush(Color.FromArgb(20, 29, 78, 216)),
            Padding = new Thickness(10, 6, 10, 6)
        };
        invoiceMeta.Inlines.Add(new Bold(new Run($"فاتورة ضريبية تجريبية ({ (isA5 ? "A5" : "A4") })  |  ")));
        invoiceMeta.Inlines.Add(new Run($"التاريخ: {DateTime.Now:yyyy/MM/dd HH:mm}  |  "));
        invoiceMeta.Inlines.Add(new Run("العميل: عميل نقدي عام  |  طريقة الدفع: نقدي"));
        doc.Blocks.Add(invoiceMeta);

        // Items Table
        var itemsTable = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1) };
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(40) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(70) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(80) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var tGroup = new TableRowGroup();

        // Header Row
        var th = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
        th.Cells.Add(MakeCell("#", true));
        th.Cells.Add(MakeCell("الصنف والوصف", true));
        th.Cells.Add(MakeCell("الكمية", true));
        th.Cells.Add(MakeCell("سعر الوحدة", true));
        th.Cells.Add(MakeCell("الإجمالي", true));
        tGroup.Rows.Add(th);

        // Sample Data Rows
        AddDataRow(tGroup, "1", "أرز مصري فاخر 1 كجم", "2.00", "33.00 ج.م", "66.00 ج.م");
        AddDataRow(tGroup, "2", "زيت عباد شمس عافية 1 لتر", "1.00", "78.00 ج.م", "78.00 ج.م");
        AddDataRow(tGroup, "3", "جبنة رومي طازجة (كيلو)", "0.50", "285.00 ج.م", "142.50 ج.م");
        itemsTable.RowGroups.Add(tGroup);
        doc.Blocks.Add(itemsTable);

        // Totals Box
        var totalsP = new Paragraph
        {
            TextAlignment = TextAlignment.Left,
            Margin = new Thickness(0, 14, 0, 0),
            FontSize = 12
        };
        totalsP.Inlines.Add(new Run("الإجمالي قبل الضريبة: 251.32 ج.م\n"));
        totalsP.Inlines.Add(new Run("ضريبة القيمة المضافة (14%): 35.18 ج.م\n"));
        totalsP.Inlines.Add(new Bold(new Run("صافي الفاتورة الإجمالي: 286.50 ج.م")) { FontSize = 14 });
        doc.Blocks.Add(totalsP);

        // Footer Message
        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooterMessage))
        {
            doc.Blocks.Add(new Paragraph(new Run(settings.ReceiptFooterMessage))
            {
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });
        }

        return doc;
    }

    private static TableCell MakeCell(string text, bool isHeader)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            FontSize = isHeader ? 11 : 10,
            Margin = new Thickness(0),
            TextAlignment = TextAlignment.Center
        })
        {
            Padding = new Thickness(6, 6, 6, 6),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }

    private static void AddDataRow(TableRowGroup group, string num, string name, string qty, string price, string total)
    {
        var row = new TableRow();
        row.Cells.Add(MakeCell(num, false));
        var nameCell = MakeCell(name, false);
        ((Paragraph)nameCell.Blocks.FirstBlock).TextAlignment = TextAlignment.Right;
        row.Cells.Add(nameCell);
        row.Cells.Add(MakeCell(qty, false));
        row.Cells.Add(MakeCell(price, false));
        row.Cells.Add(MakeCell(total, false));
        group.Rows.Add(row);
    }
}
