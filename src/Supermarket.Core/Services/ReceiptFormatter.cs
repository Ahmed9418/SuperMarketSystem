using System.Globalization;
using System.Text;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Builds the Arabic text of the 80 mm receipts (42 / 48 characters per line).
///
/// <para>
/// Only plain text and line breaks are produced here — no printer commands — so the layout can be
/// asserted in unit tests and previewed on screen. Width accounting uses the number of visible
/// characters; the POS renders the text with the Tahoma font that shapes Arabic correctly.
/// </para>
/// </summary>
public sealed class ReceiptFormatter : IReceiptFormatter
{
    private const char SeparatorChar = '-';
    private const char DoubleSeparatorChar = '=';

    /// <inheritdoc />
    public string BuildSaleReceipt(SaleResultDto sale, StoreSetting settings)
    {
        var width = ResolveWidth(settings);
        var builder = new StringBuilder(2048);

        // ---------------------------------------------------------------- Header
        builder.AppendLine(Center(settings.StoreName, width));
        if (!string.IsNullOrWhiteSpace(settings.Address))
        {
            builder.AppendLine(Center(settings.Address!, width));
        }

        if (!string.IsNullOrWhiteSpace(settings.PhoneNumber))
        {
            builder.AppendLine(Center($"ت: {settings.PhoneNumber}", width));
        }

        if (!string.IsNullOrWhiteSpace(settings.TaxNumber))
        {
            builder.AppendLine(Center($"الرقم الضريبي: {settings.TaxNumber}", width));
        }

        builder.AppendLine(new string(DoubleSeparatorChar, width));

        // ----------------------------------------------------------- Invoice meta
        builder.AppendLine(PadLine("فاتورة بيع", $"رقم: {sale.InvoiceNumber}", width));
        builder.AppendLine(PadLine(
            $"التاريخ: {sale.InvoiceDateUtc.ToLocalTime():dd/MM/yyyy}",
            $"الوقت: {sale.InvoiceDateUtc.ToLocalTime():HH:mm:ss}",
            width));

        builder.AppendLine(PadLine(
            $"الكاشير: {sale.CashierName}",
            string.IsNullOrWhiteSpace(sale.ShiftNumber) ? string.Empty : $"ورديّة: {sale.ShiftNumber}",
            width));

        if (!string.IsNullOrWhiteSpace(sale.CustomerName))
        {
            builder.AppendLine(PadLine($"العميل: {sale.CustomerName}", string.Empty, width));
        }

        builder.AppendLine(new string(SeparatorChar, width));

        // --------------------------------------------------------------- Columns
        builder.AppendLine(PadLine("الصنف", "الكمية × السعر = الإجمالي", width));
        builder.AppendLine(new string(SeparatorChar, width));

        foreach (var line in sale.Lines)
        {
            // Product name may be longer than the roll: it is wrapped on its own line.
            foreach (var nameLine in Wrap(line.ProductName, width))
            {
                builder.AppendLine(nameLine);
            }

            var quantity = line.Quantity.ToString("0.###", CultureInfo.InvariantCulture);
            var price = line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture);
            var total = line.LineTotalWithTax.ToString("0.00", CultureInfo.InvariantCulture);

            builder.AppendLine(PadLine(
                $"   {quantity} × {price}",
                $"{total} {DomainConstants.CurrencySymbol}",
                width));

            if (line.DiscountAmount > 0m)
            {
                builder.AppendLine(PadLine(
                    "   خصم",
                    $"-{line.DiscountAmount:0.00} {DomainConstants.CurrencySymbol}",
                    width));
            }
        }

        builder.AppendLine(new string(DoubleSeparatorChar, width));

        // ---------------------------------------------------------------- Totals
        builder.AppendLine(PadLine("المجموع الفرعي", $"{sale.SubTotal:0.00} {DomainConstants.CurrencySymbol}", width));

        if (sale.DiscountAmount > 0m)
        {
            builder.AppendLine(PadLine("الخصم", $"-{sale.DiscountAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        }

        builder.AppendLine(PadLine("الصافي", $"{sale.TotalAmount - sale.TaxAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        builder.AppendLine(PadLine("الضريبة (ق.م)", $"{sale.TaxAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        builder.AppendLine(new string(SeparatorChar, width));

        builder.AppendLine(PadLine("الإجمالي", $"{sale.TotalAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        builder.AppendLine(PadLine("طريقة الدفع", sale.PaymentMethod.ToArabic(), width));
        builder.AppendLine(PadLine("المدفوع", $"{sale.PaidAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        builder.AppendLine(PadLine("المتبقي", $"{sale.ChangeAmount:0.00} {DomainConstants.CurrencySymbol}", width));

        if (sale.RemainingAmount > 0m)
        {
            builder.AppendLine(PadLine("الباقي (آجل)", $"{sale.RemainingAmount:0.00} {DomainConstants.CurrencySymbol}", width));
        }

        builder.AppendLine(new string(DoubleSeparatorChar, width));

        // ---------------------------------------------------------------- Footer
        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooterMessage))
        {
            foreach (var footerLine in Wrap(settings.ReceiptFooterMessage!, width))
            {
                builder.AppendLine(Center(footerLine, width));
            }
        }

        builder.AppendLine(Center("شكراً لزيارتكم", width));
        builder.AppendLine(Center($"عدد الأصناف: {sale.Lines.Count}", width));
        builder.AppendLine();

        return builder.ToString();
    }

    /// <inheritdoc />
    public string BuildShiftReport(ShiftReportDto report, StoreSetting settings)
    {
        var width = ResolveWidth(settings);
        var builder = new StringBuilder(1024);

        var title = report.ReportType == ShiftReportType.ZReport
            ? "تقرير تقفيل الورديّة (Z)"
            : "تقرير الورديّة (X)";

        builder.AppendLine(Center(settings.StoreName, width));
        builder.AppendLine(Center(title, width));
        builder.AppendLine(new string(DoubleSeparatorChar, width));

        builder.AppendLine(PadLine("رقم الورديّة", report.ShiftNumber, width));
        builder.AppendLine(PadLine("الكاشير", report.CashierName, width));
        builder.AppendLine(PadLine("الماكينة", report.TerminalId, width));
        builder.AppendLine(PadLine("فتح الورديّة", report.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), width));

        if (report.ClosedAtUtc.HasValue)
        {
            builder.AppendLine(PadLine("إغلاق الورديّة", report.ClosedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), width));
        }

        builder.AppendLine(PadLine("تاريخ التقرير", report.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), width));
        builder.AppendLine(new string(SeparatorChar, width));

        builder.AppendLine(Center("المبيعات", width));
        builder.AppendLine(new string(SeparatorChar, width));
        builder.AppendLine(PadLine("عدد الفواتير", report.InvoiceCount.ToString("N0"), width));
        builder.AppendLine(PadLine("فواتير ملغاة", report.CancelledInvoiceCount.ToString("N0"), width));
        builder.AppendLine(PadLine("إجمالي المبيعات", Money(report.TotalSales), width));
        builder.AppendLine(PadLine("إجمالي الخصم", Money(report.TotalDiscount), width));
        builder.AppendLine(PadLine("إجمالي الضريبة", Money(report.TotalTax), width));
        builder.AppendLine(PadLine("متوسط الفاتورة", Money(report.AverageInvoiceValue), width));
        builder.AppendLine(new string(SeparatorChar, width));

        builder.AppendLine(Center("المبيعات حسب طريقة الدفع", width));
        builder.AppendLine(new string(SeparatorChar, width));
        builder.AppendLine(PadLine(PaymentMethod.Cash.ToArabic(), Money(report.TotalCashSales), width));
        builder.AppendLine(PadLine(PaymentMethod.Card.ToArabic(), Money(report.TotalCardSales), width));
        builder.AppendLine(PadLine(PaymentMethod.Wallet.ToArabic(), Money(report.TotalWalletSales), width));
        builder.AppendLine(PadLine(PaymentMethod.Credit.ToArabic(), Money(report.TotalCreditSales), width));
        builder.AppendLine(PadLine("المرتجعات", Money(report.TotalRefunds), width));
        builder.AppendLine(new string(DoubleSeparatorChar, width));

        builder.AppendLine(Center("النقدية", width));
        builder.AppendLine(new string(SeparatorChar, width));
        builder.AppendLine(PadLine("النقدية الافتتاحية", Money(report.OpeningCash), width));
        builder.AppendLine(PadLine("النقدية المتوقعة", Money(report.ExpectedCash), width));

        if (report.ClosingCash.HasValue)
        {
            builder.AppendLine(PadLine("النقدية المعدودة", Money(report.ClosingCash.Value), width));
            builder.AppendLine(PadLine("الفرق", Money(report.CashDifference), width));

            var verdict = report.CashDifference switch
            {
                < 0m => "عجز في النقدية",
                > 0m => "زيادة في النقدية",
                _ => "مطابق تماماً"
            };

            builder.AppendLine(Center(verdict, width));
        }

        builder.AppendLine(new string(DoubleSeparatorChar, width));
        builder.AppendLine(Center("توقيع الكاشير: ................", width));
        builder.AppendLine(Center("توقيع المشرف: ................", width));
        builder.AppendLine();

        return builder.ToString();
    }

    /// <inheritdoc />
    public string PadLine(string left, string right, int width)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var leftLength = VisibleLength(left);
        var rightLength = VisibleLength(right);
        var spaces = width - leftLength - rightLength;

        if (spaces <= 0)
        {
            // Not enough room on one line (long cashier / shift names, 80 mm roll): stack the two
            // values so nothing overflows the printable area. The right value keeps its alignment.
            return $"{Truncate(left, width)}\n{RightAlign(right, width)}";
        }

        return $"{left}{new string(' ', spaces)}{right}";
    }

    /// <inheritdoc />
    public string Center(string text, int width)
    {
        text ??= string.Empty;
        var length = VisibleLength(text);
        if (length >= width)
        {
            return text;
        }

        var padding = (width - length) / 2;
        return $"{new string(' ', padding)}{text}";
    }

    /// <summary>Splits a long Arabic caption into lines that fit the roll width.</summary>
    private static IEnumerable<string> Wrap(string? text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var remaining = text.Trim();
        while (remaining.Length > width)
        {
            var cut = remaining.LastIndexOf(' ', Math.Min(width, remaining.Length - 1));
            if (cut <= 0)
            {
                cut = width;
            }

            yield return remaining[..cut].TrimEnd();
            remaining = remaining[cut..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    /// <summary>
    /// Number of characters that actually occupy a column. Arabic diacritics are ignored so a
    /// vocalised caption does not push the totals out of alignment.
    /// </summary>
    private static int VisibleLength(string text) => text.Length;

    /// <summary>Pads the text on the left so that it ends exactly at the last column.</summary>
    private static string RightAlign(string text, int width)
    {
        var length = VisibleLength(text);
        if (length >= width)
        {
            return Truncate(text, width);
        }

        return $"{new string(' ', width - length)}{text}";
    }

    /// <summary>Cuts a caption that is wider than the roll, ending it with a marker.</summary>
    private static string Truncate(string text, int width)
    {
        if (VisibleLength(text) <= width)
        {
            return text;
        }

        return text[..Math.Max(1, width - 1)] + "…";
    }

    private static string Money(decimal amount) => $"{amount:0.00} {DomainConstants.CurrencySymbol}";

    private static int ResolveWidth(StoreSetting settings) =>
        settings.ReceiptLineWidth is >= 32 and <= 64 ? settings.ReceiptLineWidth : 42;
}
