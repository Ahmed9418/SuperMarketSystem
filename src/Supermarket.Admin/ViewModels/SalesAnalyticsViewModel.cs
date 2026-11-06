using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Supermarket.Admin.Services;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Admin.ViewModels;

/// <summary>
/// Sales &amp; analytics page (Phase 5): KPI tiles, four LiveCharts visuals and three tables, all
/// driven by one <see cref="ReportFilter"/> so the whole page always describes the same period.
///
/// <para>
/// The chart series are rebuilt from the aggregates on every load instead of being mutated in place —
/// LiveCharts then animates from the previous shape to the new one.
/// </para>
/// </summary>
public sealed partial class SalesAnalyticsViewModel : ObservableObject
{
    private readonly IReportService _reports;
    private readonly IUserAdminService _users;
    private readonly IStoreSettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly AuthSession _session;

    // Chart palette. Must stay in sync with src/Supermarket.Admin/Themes/Colors.xaml
    // (PrimaryColor / PrimaryMidColor / AccentColor / DangerColor / TextSecondaryColor).
    private static readonly SKColor Primary = SKColor.Parse("#1D4ED8");
    private static readonly SKColor PrimaryLight = SKColor.Parse("#60A5FA");
    private static readonly SKColor Accent = SKColor.Parse("#F59E0B");
    private static readonly SKColor Danger = SKColor.Parse("#DC2626");
    private static readonly SKColor Muted = SKColor.Parse("#5B6675");

    public SalesAnalyticsViewModel(
        IReportService reports,
        IUserAdminService users,
        IStoreSettingsService settings,
        IDialogService dialogs,
        AuthSession session)
    {
        _reports = reports;
        _users = users;
        _settings = settings;
        _dialogs = dialogs;
        _session = session;

        BuildEmptyCharts();
        _ = LoadAsync();
    }

    // ------------------------------------------------------------------ Filters
    public ObservableCollection<LookupDto> Cashiers { get; } = new();

    public IReadOnlyList<string> PeriodPresets { get; } = new[]
    {
        "اليوم", "أمس", "آخر 7 أيام", "آخر 30 يوم", "هذا الشهر", "الشهر السابق", "هذا العام"
    };

    [ObservableProperty]
    private string selectedPeriod = "آخر 30 يوم";

    [ObservableProperty]
    private DateTime fromDate = DateTime.Today.AddDays(-29);

    [ObservableProperty]
    private DateTime toDate = DateTime.Today;

    [ObservableProperty]
    private LookupDto? selectedCashier;

    [ObservableProperty]
    private LookupDto? selectedPaymentMethod;

    // ------------------------------------------------------------------ Data
    [ObservableProperty]
    private SalesAnalyticsDto analytics = new();

    [ObservableProperty]
    private ObservableCollection<InvoiceListDto> invoices = new();

    [ObservableProperty]
    private InvoiceDetailDto? selectedInvoice;

    [ObservableProperty]
    private InvoiceListDto? selectedInvoiceRow;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "جاهز";

    // ------------------------------------------------------------------ Chart series
    [ObservableProperty]
    private ISeries[] salesSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] salesXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] salesYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] paymentSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] categorySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] categoryXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] categoryYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] topProductSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] topProductXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] topProductYAxes = Array.Empty<Axis>();

    // ------------------------------------------------------------------ Derived text
    public string StoreName { get; private set; } = "سوبر ماركت";

    public string PeriodText => $"{FromDate:dd/MM/yyyy} — {ToDate:dd/MM/yyyy}";

    public string GrowthText => Analytics.SalesGrowth >= 0m
        ? $"▲ {Analytics.SalesGrowth:N1}% عن الفترة السابقة"
        : $"▼ {Math.Abs(Analytics.SalesGrowth):N1}% عن الفترة السابقة";

    public bool IsGrowthPositive => Analytics.SalesGrowth >= 0m;

    public string CashDifferenceText => Analytics.CashDifference >= 0m
        ? $"زيادة {Analytics.CashDifference:N2} {DomainConstants.CurrencySymbol}"
        : $"عجز {Math.Abs(Analytics.CashDifference):N2} {DomainConstants.CurrencySymbol}";

    public bool CanExport => _session.Can(AppPermissions.ReportsExport) || _session.Can(AppPermissions.ReportsView);

    // ------------------------------------------------------------------ Commands
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "جارٍ تحميل التحليلات...";

        try
        {
            if (Analytics.Filter is null || Analytics.Filter.FromDate != FromDate || Analytics.Filter.ToDate != ToDate)
            {
                OnPropertyChanged(nameof(PeriodText));
            }

            var filter = new ReportFilter
            {
                FromDate = FromDate,
                ToDate = ToDate,
                UserId = SelectedCashier is { Id: > 0 } ? SelectedCashier.Id : null,
                PaymentMethod = SelectedPaymentMethod?.Id is > 0 and var id ? (PaymentMethod)id : null
            };

            Analytics = await _reports.GetSalesAnalyticsAsync(filter);

            await LoadCashiersAsync();
            await LoadInvoicesAsync();

            RebuildCharts();

            StatusMessage = $"تم التحميل — {Analytics.InvoiceCount:N0} فاتورة في {filter.DayCount:N0} يوم";
            OnPropertyChanged(nameof(PeriodText));
            OnPropertyChanged(nameof(GrowthText));
            OnPropertyChanged(nameof(IsGrowthPositive));
            OnPropertyChanged(nameof(CashDifferenceText));
        }
        catch (Exception ex)
        {
            StatusMessage = "تعذر تحميل التحليلات";
            _dialogs.ShowError($"تعذر تحميل تقارير المبيعات:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyPresetAsync(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        var today = DateTime.Today;

        (FromDate, ToDate) = preset switch
        {
            "اليوم" => (today, today),
            "أمس" => (today.AddDays(-1), today.AddDays(-1)),
            "آخر 7 أيام" => (today.AddDays(-6), today),
            "آخر 30 يوم" => (today.AddDays(-29), today),
            "هذا الشهر" => (new DateTime(today.Year, today.Month, 1), today),
            "الشهر السابق" => PreviousMonth(today),
            "هذا العام" => (new DateTime(today.Year, 1, 1), today),
            _ => (FromDate, ToDate)
        };

        SelectedPeriod = preset;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ApplyFilterAsync() => await LoadAsync();

    /// <summary>Loads the detail panel of the selected invoice (double click in the grid).</summary>
    [RelayCommand]
    private async Task OpenInvoiceAsync(InvoiceListDto? row)
    {
        row ??= SelectedInvoiceRow;
        if (row is null)
        {
            return;
        }

        try
        {
            SelectedInvoice = await _reports.GetInvoiceDetailAsync(row.Id);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل الفاتورة:\n{ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private void CloseInvoice() => SelectedInvoice = null;

    /// <summary>
    /// Exports the invoice grid of the current period to a CSV file (UTF-8 with BOM so Excel shows the
    /// Arabic correctly). Deliberately dependency free — no Excel interop, no extra package.
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SupermarketReports");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"Sales-{FromDate:yyyyMMdd}-{ToDate:yyyyMMdd}.csv");

            var lines = new List<string>
            {
                "رقم الفاتورة,التاريخ,الكاشير,العميل,طريقة الدفع,عدد الأصناف,الإجمالي,الضريبة,الخصم,المدفوع,المتبقي,الحالة"
            };

            foreach (var invoice in Invoices)
            {
                lines.Add(string.Join(',',
                    Escape(invoice.InvoiceNumber),
                    Escape(invoice.InvoiceDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                    Escape(invoice.CashierName),
                    Escape(invoice.CustomerName ?? "-"),
                    Escape(invoice.PaymentMethodName),
                    invoice.ItemCount.ToString(),
                    invoice.TotalAmount.ToString("0.00"),
                    invoice.TaxAmount.ToString("0.00"),
                    invoice.DiscountAmount.ToString("0.00"),
                    invoice.PaidAmount.ToString("0.00"),
                    invoice.RemainingAmount.ToString("0.00"),
                    Escape(invoice.StatusName)));
            }

            await File.WriteAllLinesAsync(path, lines, new System.Text.UTF8Encoding(true));

            StatusMessage = $"تم تصدير {Invoices.Count:N0} فاتورة";
            _dialogs.ShowInformation($"تم تصدير التقرير إلى:\n{path}", "تم التصدير");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تصدير التقرير:\n{ex.Message}", "خطأ");
        }
    }

    // ------------------------------------------------------------------ Loading helpers
    private async Task LoadCashiersAsync()
    {
        if (Cashiers.Count > 0)
        {
            return;
        }

        try
        {
            var storeSettings = await _settings.GetAsync();
            StoreName = storeSettings.StoreName;
            OnPropertyChanged(nameof(StoreName));

            Cashiers.Add(new LookupDto { Id = 0, Name = "كل الكاشيرين" });
            foreach (var user in await _users.GetAllAsync())
            {
                Cashiers.Add(new LookupDto { Id = user.Id, Name = user.FullName });
            }

            SelectedCashier = Cashiers.FirstOrDefault();
        }
        catch
        {
            // The cashier filter is a convenience: never fail the whole page for it.
        }
    }

    private async Task LoadInvoicesAsync()
    {
        var page = await _reports.GetInvoicesAsync(new InvoiceFilter
        {
            FromDate = FromDate,
            ToDate = ToDate,
            UserId = SelectedCashier is { Id: > 0 } ? SelectedCashier.Id : null,
            PaymentMethod = SelectedPaymentMethod?.Id is > 0 and var id ? (PaymentMethod)id : null,
            PageNumber = 1,
            PageSize = 100
        });

        Invoices = new ObservableCollection<InvoiceListDto>(page.Items);
    }

    // ------------------------------------------------------------------ Charting
    private void BuildEmptyCharts()
    {
        SalesSeries = Array.Empty<ISeries>();
        PaymentSeries = Array.Empty<ISeries>();
        CategorySeries = Array.Empty<ISeries>();
        TopProductSeries = Array.Empty<ISeries>();
    }

    private void RebuildCharts()
    {
        BuildSalesChart();
        BuildPaymentChart();
        BuildCategoryChart();
        BuildTopProductChart();
    }

    private void BuildSalesChart()
    {
        var points = Analytics.DailySales;

        SalesSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Name = $"المبيعات ({DomainConstants.CurrencySymbol})",
                Values = points.Select(p => new DateTimePoint(p.Date, (double)p.Sales)).ToArray(),
                Stroke = new SolidColorPaint(Primary, 3),
                Fill = new SolidColorPaint(Primary.WithAlpha(38)),
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(Primary, 3),
                GeometryFill = new SolidColorPaint(SKColors.White),
                LineSmoothness = 0.4
            },
            new LineSeries<DateTimePoint>
            {
                Name = $"الأرباح ({DomainConstants.CurrencySymbol})",
                Values = points.Select(p => new DateTimePoint(p.Date, (double)p.Profit)).ToArray(),
                Stroke = new SolidColorPaint(Accent, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.4
            }
        };

        SalesXAxes = new[]
        {
            new Axis
            {
                Labeler = value => value >= 0 && value <= DateTime.MaxValue.Ticks
                    ? new DateTime((long)value).ToString("dd/MM")
                    : string.Empty,
                UnitWidth = TimeSpan.FromDays(1).Ticks,
                MinStep = TimeSpan.FromDays(1).Ticks,
                LabelsRotation = -35,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#DDE3EE"))
            }
        };

        SalesYAxes = new[]
        {
            new Axis
            {
                Labeler = value => value.ToString("N0"),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#DDE3EE"))
            }
        };
    }

    private void BuildPaymentChart()
    {
        var palette = new[] { Primary, PrimaryLight, Accent, Danger };

        PaymentSeries = Analytics.PaymentBreakdown
            .Select((p, index) => (ISeries)new PieSeries<double>
            {
                Name = p.MethodName,
                Values = new[] { (double)p.Amount },
                Fill = new SolidColorPaint(palette[index % palette.Length]),
                Stroke = new SolidColorPaint(SKColors.White, 2),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 12,
                DataLabelsFormatter = _ => p.ShareText,
                ToolTipLabelFormatter = _ => $"{p.MethodName}: {p.Amount:N2} {DomainConstants.CurrencySymbol} ({p.ShareText})"
            })
            .ToArray();
    }

    private void BuildCategoryChart()
    {
        var top = Analytics.CategorySales.Take(8).ToList();

        CategorySeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = $"المبيعات ({DomainConstants.CurrencySymbol})",
                Values = top.Select(c => (double)c.Sales).ToArray(),
                Fill = new SolidColorPaint(Primary),
                MaxBarWidth = 34,
                Rx = 4,
                Ry = 4
            }
        };

        CategoryXAxes = new[]
        {
            new Axis
            {
                Labels = top.Select(c => Trim(c.CategoryName, 14)).ToArray(),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                LabelsRotation = -25
            }
        };

        CategoryYAxes = new[]
        {
            new Axis
            {
                Labeler = value => value.ToString("N0"),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#DDE3EE"))
            }
        };
    }

    private void BuildTopProductChart()
    {
        var top = Analytics.TopProducts.Take(8).ToList();

        TopProductSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Name = $"الإيراد ({DomainConstants.CurrencySymbol})",
                Values = top.Select(p => (double)p.Revenue).ToArray(),
                Fill = new SolidColorPaint(PrimaryLight),
                MaxBarWidth = 24,
                Rx = 4,
                Ry = 4
            }
        };

        TopProductXAxes = new[]
        {
            new Axis
            {
                Labeler = value => value.ToString("N0"),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#DDE3EE"))
            }
        };

        TopProductYAxes = new[]
        {
            new Axis
            {
                // Rows are drawn bottom-up: reverse so the best seller appears on top.
                Labels = top.Select(p => Trim(p.ProductName, 18)).Reverse().ToArray(),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(Muted)
            }
        };
    }

    private static (DateTime From, DateTime To) PreviousMonth(DateTime today)
    {
        var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastOfPrevious = firstOfThisMonth.AddDays(-1);
        return (new DateTime(lastOfPrevious.Year, lastOfPrevious.Month, 1), lastOfPrevious);
    }

    private static string Trim(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
