using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Entities;
using Supermarket.POS.Services;

namespace Supermarket.POS.ViewModels;

/// <summary>
/// The checkout screen of the POS (Phase 3).
///
/// <para>
/// Keyboard first: the barcode box keeps the focus at all times (the view re-focuses it after every
/// command), so a wedge scanner types straight into <see cref="BarcodeBuffer"/>. Function keys are
/// exposed as commands so both the physical keyboard and the on-screen buttons drive the same code:
/// F1 help, F2 search, F3 quantity, F4 discount, F6 next line, F8 hold, F9 payment, F10 shift.
/// </para>
/// </summary>
public sealed partial class SalesViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ISalesService _salesService;
    private readonly IShiftService _shiftService;
    private readonly IStoreSettingsService _storeSettings;
    private readonly ICatalogService _catalogService;
    private readonly IHeldCartService _heldCartService;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _searchCts;
    private ShiftDto? _currentShift;
    private IReadOnlySet<string> _permissions = AppPermissions.DefaultsFor(UserRole.Cashier)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public SalesViewModel(
        IProductService productService,
        ISalesService salesService,
        IShiftService shiftService,
        IStoreSettingsService storeSettings,
        ICatalogService catalogService,
        IHeldCartService heldCartService,
        IDialogService dialogs)
    {
        _productService = productService;
        _salesService = salesService;
        _shiftService = shiftService;
        _storeSettings = storeSettings;
        _catalogService = catalogService;
        _heldCartService = heldCartService;
        _dialogs = dialogs;

        Cart.CollectionChanged += (_, _) =>
        {
            RecalculateTotals();
            OnPropertyChanged(nameof(IsCartEmpty));
        };
    }

    // ------------------------------------------------------------------ State
    public ObservableCollection<CartLineViewModel> Cart { get; } = new();

    public ObservableCollection<ProductDto> SearchResults { get; } = new();

    public ObservableCollection<LookupDto> Categories { get; } = new();

    [ObservableProperty]
    private CartLineViewModel? selectedLine;

    [ObservableProperty]
    private string barcodeBuffer = string.Empty;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private LookupDto? selectedCategory;

    [ObservableProperty]
    private CartPricingDto totals = new();

    [ObservableProperty]
    private decimal invoiceDiscount;

    [ObservableProperty]
    private decimal invoiceDiscountPercentage;

    [ObservableProperty]
    private string statusMessage = "جاهز للبيع - امسح البار كود أو ابحث عن المنتج";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isShiftOpen;

    [ObservableProperty]
    private string cashierName = string.Empty;

    [ObservableProperty]
    private string shiftNumber = string.Empty;

    [ObservableProperty]
    private string storeName = string.Empty;

    [ObservableProperty]
    private string terminalId = "POS-01";

    [ObservableProperty]
    private DateTime shiftOpenedAt = DateTime.Now;

    /// <summary>Number of baskets the cashier has parked; shown as a badge on the F5 button.</summary>
    [ObservableProperty]
    private int heldCartCount;

    [ObservableProperty]
    private PaymentMethod selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private decimal paidAmount;

    [ObservableProperty]
    private string lastChangeMessage = string.Empty;

    public bool IsCash => SelectedPaymentMethod == PaymentMethod.Cash;

    public bool IsWallet => SelectedPaymentMethod == PaymentMethod.Wallet;

    public bool IsCard => SelectedPaymentMethod == PaymentMethod.Card;

    public decimal ChangeAmount => SelectedPaymentMethod == PaymentMethod.Cash
        ? Math.Max(0m, Math.Round(PaidAmount - Totals.TotalAmount, 2))
        : 0m;

    public string ChangeText => $"{ChangeAmount:N2} {DomainConstants.CurrencySymbol}";

    public decimal RemainingAmount => SelectedPaymentMethod == PaymentMethod.Cash
        ? Math.Max(0m, Math.Round(Totals.TotalAmount - PaidAmount, 2))
        : 0m;

    public string RemainingText => $"{RemainingAmount:N2} {DomainConstants.CurrencySymbol}";

    public bool HasChange => ChangeAmount > 0m;

    public bool IsFullyPaid => SelectedPaymentMethod != PaymentMethod.Cash || PaidAmount >= Totals.TotalAmount;

    public bool IsCartEmpty => Cart.Count == 0;

    public string GrandTotalText => $"{Totals.TotalAmount:N2} {DomainConstants.CurrencySymbol}";

    public string NetTotalText => $"{Totals.NetTotal:N2} {DomainConstants.CurrencySymbol}";

    public string TaxTotalText => $"{Totals.TaxAmount:N2} {DomainConstants.CurrencySymbol}";

    public string DiscountTotalText => $"{Totals.TotalDiscount:N2} {DomainConstants.CurrencySymbol}";

    public string SubTotalText => $"{Totals.SubTotal:N2} {DomainConstants.CurrencySymbol}";

    public string ItemCountText => $"{Totals.LineCount:N0} صنف / {Totals.TotalQuantity:N2} وحدة";

    /// <summary>Raised when the sale is committed so the view can clear the barcode box / print.</summary>
    public event EventHandler<SaleResultDto>? SaleCompleted;

    /// <summary>Raised when the view should move the keyboard focus back to the barcode box.</summary>
    public event EventHandler? FocusBarcodeRequested;

    // -------------------------------------------------------------- Lifecycle
    /// <summary>Loads the store header, categories and the cashier's open shift.</summary>
    public async Task InitializeAsync(UserDto user, string terminal)
    {
        CashierName = user.FullName;
        TerminalId = terminal;

        IsBusy = true;
        try
        {
            var settings = await _storeSettings.GetAsync();
            StoreName = settings.StoreName;

            Categories.Clear();
            Categories.Add(new LookupDto { Id = 0, Name = "كل الأقسام" });
            foreach (var category in await _catalogService.GetCategoriesAsync())
            {
                Categories.Add(category);
            }

            SelectedCategory = Categories.FirstOrDefault();

            _currentShift = await _shiftService.GetOpenShiftAsync(user.Id, terminal);
            ApplyShift(_currentShift);

            await RefreshHeldCountAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل شاشة البيع:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
            RequestBarcodeFocus();
        }
    }

    private void ApplyShift(ShiftDto? shift)
    {
        _currentShift = shift;
        IsShiftOpen = shift is not null;
        ShiftNumber = shift?.ShiftNumber ?? "لا توجد ورديّة مفتوحة";
        ShiftOpenedAt = shift?.OpenedAtUtc.ToLocalTime() ?? DateTime.Now;
        OnPropertyChanged(nameof(CurrentShiftId));
    }

    // ------------------------------------------------------- Barcode & search
    /// <summary>
    /// Called by the view when the scanner (or the cashier) presses Enter in the barcode box.
    /// Anything scanned twice is merged into one cart line by the sales service.
    /// </summary>
    [RelayCommand]
    private async Task ProcessBarcodeAsync()
    {
        var code = (BarcodeBuffer ?? string.Empty).Trim();
        BarcodeBuffer = string.Empty;

        if (code.Length == 0)
        {
            return;
        }

        try
        {
            var lookup = await _productService.FindByBarcodeAsync(code);
            if (!lookup.Found || lookup.Product is null)
            {
                StatusMessage = lookup.Message;
                PlayError();
                return;
            }

            AddToCart(lookup.Product, 1m);
            StatusMessage = lookup.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر قراءة البار كود: {ex.Message}";
        }
        finally
        {
            RequestBarcodeFocus();
        }
    }

    /// <summary>Debounced name search used by the F2 shortcut.</summary>
    [RelayCommand]
    private async Task SearchProductsAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(250, _searchCts.Token);
            var results = await _productService.SearchAsync(SearchTerm, 40);

            SearchResults.Clear();
            foreach (var product in results.Where(p => SelectedCategory is null || SelectedCategory.Id == 0
                                                       || p.CategoryId == SelectedCategory.Id))
            {
                SearchResults.Add(product);
            }

            StatusMessage = SearchResults.Count == 0
                ? "لا توجد منتجات مطابقة"
                : $"نتائج البحث: {SearchResults.Count}";
        }
        catch (TaskCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    partial void OnSearchTermChanged(string value) => SearchProductsCommand.Execute(null);

    partial void OnSelectedCategoryChanged(LookupDto? value) => SearchProductsCommand.Execute(null);

    /// <summary>Adds a product from the touch tiles / search results.</summary>
    [RelayCommand]
    private void AddProduct(ProductDto? product)
    {
        if (product is null)
        {
            return;
        }

        AddToCart(product, product.IsWeighted ? 1m : 1m);
        StatusMessage = $"تمت إضافة {product.Name} - {product.EffectivePrice:N2} {DomainConstants.CurrencySymbol}";
        RequestBarcodeFocus();
    }

    /// <summary>Core add-to-cart logic: merges an existing line or creates a new one.</summary>
    public void AddToCart(ProductDto product, decimal quantity)
    {
        var existing = Cart.FirstOrDefault(l => l.ProductId == product.Id);

        if (existing is not null)
        {
            existing.Quantity += quantity;
            SelectedLine = existing;
            RecalculateTotals();
            return;
        }

        var line = new CartLineViewModel(product, quantity, RecalculateTotals);
        Cart.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    // -------------------------------------------------------- Cart operations
    [RelayCommand]
    private void RemoveLine(CartLineViewModel? line)
    {
        line ??= SelectedLine;
        if (line is null)
        {
            return;
        }

        Cart.Remove(line);
        SelectedLine = Cart.LastOrDefault();
        StatusMessage = $"تم حذف {line.ProductName} من السلة";
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void ClearCart()
    {
        if (Cart.Count == 0)
        {
            return;
        }

        if (!_dialogs.Confirm("هل تريد تفريغ السلة بالكامل؟", "تأكيد"))
        {
            return;
        }

        Cart.Clear();
        InvoiceDiscount = 0m;
        InvoiceDiscountPercentage = 0m;
        LastChangeMessage = string.Empty;
        PaidAmount = 0m;
        RecalculateTotals();
        NotifyPaymentProperties();
        StatusMessage = "تم تفريغ السلة";
    }

    [RelayCommand]
    private void IncreaseQuantity(CartLineViewModel? line)
    {
        line ??= SelectedLine;
        if (line is null)
        {
            return;
        }

        line.Quantity += line.IsWeighted ? 0.5m : 1m;
    }

    [RelayCommand]
    private void DecreaseQuantity(CartLineViewModel? line)
    {
        line ??= SelectedLine;
        if (line is null)
        {
            return;
        }

        if (line.Quantity <= 1m)
        {
            Cart.Remove(line);
            SelectedLine = Cart.LastOrDefault();
            return;
        }

        line.Quantity -= line.IsWeighted ? 0.5m : 1m;
    }

    [RelayCommand]
    private void SelectNextLine()
    {
        if (Cart.Count == 0)
        {
            return;
        }

        var index = SelectedLine is null ? 0 : Cart.IndexOf(SelectedLine) + 1;
        SelectedLine = index >= Cart.Count ? Cart[0] : Cart[index];
    }

    // ------------------------------------------------------------- F-key flows
    /// <summary>F1 – help sheet listing the shortcuts.</summary>
    [RelayCommand]
    private void ShowHelp() => _dialogs.ShowInformation(
        "F1 المساعدة\n" +
        "F2 البحث عن منتج\n" +
        "F3 تعديل كمية السطر المحدد\n" +
        "F4 خصم على السطر المحدد\n" +
        "F6 الانتقال للسطر التالي\n" +
        "F7 خصم على إجمالي الفاتورة\n" +
        "F8 حذف السطر المحدد\n" +
        "F5 تعليق السلة الحالية\n" +
        "Shift+F5 استكمال سلة معلقة\n" +
        "F9 الدفع وإتمام البيع\n" +
        "F10 بيانات الورديّة\n" +
        "Esc إلغاء / تفريغ السلة",
        "مفاتيح الاختصار");

    /// <summary>F2 – moves the focus to the search box.</summary>
    [RelayCommand]
    private void FocusSearch() => StatusMessage = "اكتب اسم المنتج أو البار كود ثم اختر من النتائج";

    /// <summary>F3 – asks for the quantity of the selected line.</summary>
    [RelayCommand]
    private void EditQuantity()
    {
        if (SelectedLine is null)
        {
            _dialogs.ShowWarning("من فضلك اختر سطراً من الفاتورة أولاً", "تنبيه");
            return;
        }

        var input = _dialogs.Prompt(
            $"أدخل الكمية للمنتج ({SelectedLine.ProductName})\nالمتاح بالمخزن: {SelectedLine.StockOnHand:N2}",
            "تعديل الكمية",
            SelectedLine.Quantity.ToString("N2"));

        if (input is null)
        {
            return;
        }

        if (!decimal.TryParse(input, out var quantity) || quantity <= 0m)
        {
            _dialogs.ShowWarning("الكمية غير صحيحة", "تنبيه");
            return;
        }

        if (!SelectedLine.IsWeighted && quantity != Math.Truncate(quantity))
        {
            _dialogs.ShowWarning("هذا المنتج لا يُباع بكسور، يجب إدخال كمية صحيحة", "تنبيه");
            return;
        }

        if (quantity > SelectedLine.StockOnHand)
        {
            _dialogs.ShowWarning($"الكمية المتاحة {SelectedLine.StockOnHand:N2} فقط", "تنبيه");
            return;
        }

        SelectedLine.Quantity = quantity;
        StatusMessage = $"تم تعديل كمية {SelectedLine.ProductName} إلى {quantity:N2}";
        RequestBarcodeFocus();
    }

    /// <summary>F4 – percentage discount on the selected line.</summary>
    [RelayCommand]
    private void EditLineDiscount()
    {
        if (!CanDiscount)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية منح الخصومات. تواصل مع المشرف.", "الصلاحيات");
            return;
        }

        if (SelectedLine is null)
        {
            _dialogs.ShowWarning("من فضلك اختر سطراً من الفاتورة أولاً", "تنبيه");
            return;
        }

        var input = _dialogs.Prompt(
            $"نسبة الخصم على ({SelectedLine.ProductName}) %",
            "خصم على السطر",
            "0");

        if (input is null || !decimal.TryParse(input, out var percentage) || percentage is < 0m or > 100m)
        {
            return;
        }

        SelectedLine.ApplyDiscount(percentage);
        StatusMessage = $"تم تطبيق خصم {percentage:N0}% على {SelectedLine.ProductName}";
        RequestBarcodeFocus();
    }

    /// <summary>F7 – invoice level discount, either an amount or a percentage.</summary>
    [RelayCommand]
    private void EditInvoiceDiscount()
    {
        if (!CanDiscount)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية منح الخصومات. تواصل مع المشرف.", "الصلاحيات");
            return;
        }

        if (Cart.Count == 0)
        {
            return;
        }

        var input = _dialogs.Prompt(
            "أدخل قيمة الخصم على إجمالي الفاتورة\n(أضف % في النهاية لنسبة مئوية، مثال: 10%)",
            "خصم على الفاتورة",
            InvoiceDiscount > 0m ? InvoiceDiscount.ToString("N2") : "0");

        if (input is null)
        {
            return;
        }

        input = input.Trim();

        if (input.EndsWith('%'))
        {
            if (!decimal.TryParse(input.TrimEnd('%'), out var percentage) || percentage is < 0m or > 100m)
            {
                _dialogs.ShowWarning("النسبة غير صحيحة", "تنبيه");
                return;
            }

            InvoiceDiscountPercentage = percentage;
            InvoiceDiscount = 0m;
        }
        else
        {
            if (!decimal.TryParse(input, out var amount) || amount < 0m)
            {
                _dialogs.ShowWarning("القيمة غير صحيحة", "تنبيه");
                return;
            }

            InvoiceDiscount = amount;
            InvoiceDiscountPercentage = 0m;
        }

        RecalculateTotals();
        RequestBarcodeFocus();
    }

    /// <summary>F10 – opens the shift closing / X-Z report screen.</summary>
    [RelayCommand]
    private void ShowShiftInfo()
    {
        if (!CanCloseShift)
        {
            _dialogs.ShowWarning("لا تمتلك صلاحية عرض/تقفيل الورديّة. تواصل مع المشرف.", "الصلاحيات");
            return;
        }

        if (_currentShift is null)
        {
            _dialogs.ShowWarning("لا توجد ورديّة مفتوحة. تواصل مع المشرف لفتح ورديّة.", "الورديّة");
            return;
        }

        ShiftCloseRequested?.Invoke(this, _currentShift.Id);
    }

    /// <summary>Raised by F10; the window opens the closing screen for this shift id.</summary>
    public event EventHandler<int>? ShiftCloseRequested;

    /// <summary>Called by the window after a closing so the header stops showing an open shift.</summary>
    public void ApplyClosedShift()
    {
        _currentShift = null;
        IsShiftOpen = false;
        ShiftNumber = "لا توجد ورديّة مفتوحة";
        StatusMessage = "تم تقفيل الورديّة. يجب فتح ورديّة جديدة لمتابعة البيع.";
        RequestBarcodeFocus();
    }

    /// <summary>Opens a shift for the signed-in cashier (used when none exists yet).</summary>
    [RelayCommand]
    private void OpenShift()
    {
        var input = _dialogs.Prompt("أدخل النقدية الافتتاحية في الدرج", "فتح ورديّة", "0");
        if (input is null || !decimal.TryParse(input, out var openingCash) || openingCash < 0m)
        {
            return;
        }

        // The window raises this event; the code-behind performs the async call with the cashier id.
        ShiftOpenRequested?.Invoke(this, openingCash);
    }

    /// <summary>Requested by the view model, executed by the window (async, needs the user id).</summary>
    public event EventHandler<decimal>? ShiftOpenRequested;

    public void ApplyOpenedShift(ShiftDto shift)
    {
        ApplyShift(shift);
        StatusMessage = $"تم فتح الورديّة {shift.ShiftNumber}";
        RequestBarcodeFocus();
    }

    // ------------------------------------------------------------- Held carts
    /// <summary>
    /// F5 – parks the current basket. The cashier is asked for a short label (customer name / note)
    /// and the cart is cleared so the next customer can be served.
    /// </summary>
    [RelayCommand]
    private async Task HoldCartAsync()
    {
        if (Cart.Count == 0)
        {
            _dialogs.ShowWarning("السلة فارغة، لا يوجد ما يتم تعليقه", "تنبيه");
            return;
        }

        var label = _dialogs.Prompt(
            "أدخل بياناً قصيراً للسلة المعلقة\n(مثال: العميل الذي طلب توصيل / العميل أحمد)",
            "تعليق السلة",
            $"سلة معلقة - {DateTime.Now:HH:mm}");

        if (label is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _heldCartService.HoldAsync(new HoldCartRequest
            {
                UserId = CurrentUserId,
                ShiftId = CurrentShiftId,
                TerminalId = TerminalId,
                Label = label,
                InvoiceDiscount = InvoiceDiscount,
                InvoiceDiscountPercentage = InvoiceDiscountPercentage,
                EstimatedTotal = Totals.TotalAmount,
                Lines = Cart.Select(l => l.ToDto()).ToList()
            });

            Cart.Clear();
            InvoiceDiscount = 0m;
            InvoiceDiscountPercentage = 0m;
            RecalculateTotals();
            await RefreshHeldCountAsync();

            StatusMessage = "تم تعليق السلة - يمكن استكمالها لاحقاً بالضغط على Shift+F5";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تعليق السلة:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
            RequestBarcodeFocus();
        }
    }

    /// <summary>Shift+F5 – asks the window to open the list of parked baskets.</summary>
    [RelayCommand]
    private async Task ShowHeldCartsAsync()
    {
        await RefreshHeldCountAsync();

        if (HeldCartCount == 0)
        {
            _dialogs.ShowInformation("لا توجد سلات معلقة حالياً", "السلات المعلقة");
            RequestBarcodeFocus();
            return;
        }

        if (Cart.Count > 0 && !_dialogs.Confirm(
                "لديك سلة مفتوحة. سيتم استبدالها بالسلة المعلقة التي تختارها. هل تريد المتابعة؟",
                "استكمال سلة معلقة"))
        {
            return;
        }

        HeldCartsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the cashier wants the list of parked baskets.</summary>
    public event EventHandler? HeldCartsRequested;

    /// <summary>Puts a resumed basket back into the cart (prices already refreshed by the service).</summary>
    public void ApplyResumedCart(HeldCartDto held)
    {
        Cart.Clear();

        foreach (var line in held.Lines)
        {
            var product = new ProductDto
            {
                Id = line.ProductId,
                Name = line.ProductName,
                Barcode = line.Barcode,
                UnitName = line.UnitName,
                CostPrice = line.CostPrice,
                SellingPrice = line.UnitPrice,
                VatRate = line.VatRate,
                StockQuantity = line.StockOnHand,
                IsWeighted = line.IsWeighted,
                IsActive = true
            };

            var row = new CartLineViewModel(product, line.Quantity, RecalculateTotals)
            {
                DiscountAmount = line.DiscountAmount
            };

            Cart.Add(row);
        }

        InvoiceDiscount = held.InvoiceDiscount;
        InvoiceDiscountPercentage = held.InvoiceDiscountPercentage;
        RecalculateTotals();
        SelectedLine = Cart.LastOrDefault();

        var shortages = Cart.Count(l => l.ExceedsStock);
        StatusMessage = shortages == 0
            ? $"تم استكمال السلة ({held.Label}) - {Totals.TotalAmount:N2} {DomainConstants.CurrencySymbol}"
            : $"تم استكمال السلة ({held.Label}) - تنبيه: {shortages} صنف بكمية أكبر من المتاح";

        _ = RefreshHeldCountAsync();
        RequestBarcodeFocus();
    }

    /// <summary>Deletes a parked basket permanently.</summary>
    public async Task DeleteHeldCartAsync(int heldCartId)
    {
        try
        {
            await _heldCartService.DeleteAsync(heldCartId);
            await RefreshHeldCountAsync();
            StatusMessage = "تم حذف السلة المعلقة";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر حذف السلة المعلقة:\n{ex.Message}", "خطأ");
        }
        finally
        {
            RequestBarcodeFocus();
        }
    }

    /// <summary>Asks the window to load the parked baskets of this cashier.</summary>
    public async Task<IReadOnlyList<HeldCartDto>> LoadHeldCartsAsync()
    {
        try
        {
            return await _heldCartService.GetHeldAsync(CurrentUserId);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر تحميل السلات المعلقة:\n{ex.Message}", "خطأ");
            return Array.Empty<HeldCartDto>();
        }
    }

    /// <summary>Resumes a parked basket through the service, then pushes it into the cart.</summary>
    public async Task<bool> ResumeHeldCartAsync(int heldCartId)
    {
        try
        {
            var held = await _heldCartService.ResumeAsync(heldCartId);
            if (held is null)
            {
                _dialogs.ShowWarning("السلة المعلقة لم تعد موجودة", "تنبيه");
                await RefreshHeldCountAsync();
                return false;
            }

            ApplyResumedCart(held);
            return true;
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر استكمال السلة:\n{ex.Message}", "خطأ");
            return false;
        }
    }

    private async Task RefreshHeldCountAsync()
    {
        if (CurrentUserId <= 0)
        {
            return;
        }

        try
        {
            HeldCartCount = await _heldCartService.CountAsync(CurrentUserId);
        }
        catch
        {
            // A badge must never block the till.
        }
    }

    // --------------------------------------------------------------- Checkout
    /// <summary>
    /// F9 – commits the sale directly using the selected payment method (Cash with change, Wallet, or Card).
    /// Modal payment window is eliminated.
    /// </summary>
    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (Cart.Count == 0)
        {
            _dialogs.ShowWarning("السلة فارغة، أضف منتجات قبل إتمام الدفع", "تنبيه");
            RequestBarcodeFocus();
            return;
        }

        if (!IsShiftOpen)
        {
            _dialogs.ShowWarning("يجب فتح ورديّة قبل إتمام البيع", "الورديّة");
            return;
        }

        var overStock = Cart.FirstOrDefault(l => l.ExceedsStock);
        if (overStock is not null)
        {
            _dialogs.ShowWarning(
                $"الكمية المطلوبة من ({overStock.ProductName}) أكبر من المتاح ({overStock.StockOnHand:N2})",
                "تنبيه");
            return;
        }

        if (SelectedPaymentMethod == PaymentMethod.Cash && PaidAmount < Totals.TotalAmount)
        {
            _dialogs.ShowWarning(
                $"المبلغ المدفوع ({PaidAmount:N2}) أقل من الإجمالي المستحق ({Totals.TotalAmount:N2})",
                "تنبيه");
            return;
        }

        IsBusy = true;
        try
        {
            var effectivePaid = SelectedPaymentMethod == PaymentMethod.Cash ? PaidAmount : Totals.TotalAmount;
            var request = new SaleRequestDto
            {
                UserId = CurrentUserId,
                ShiftId = CurrentShiftId,
                CustomerId = null,
                CustomerName = null,
                Lines = Cart.Select(l => l.ToDto()).ToList(),
                InvoiceDiscount = Totals.InvoiceDiscount,
                InvoiceDiscountPercentage = Totals.InvoiceDiscountPercentage,
                PaidAmount = effectivePaid,
                PaymentMethod = SelectedPaymentMethod
            };

            var result = await _salesService.CompleteSaleAsync(request);

            if (result.ChangeAmount > 0m)
            {
                LastChangeMessage = $"باقي العميل: {result.ChangeAmount:N2} {DomainConstants.CurrencySymbol}";
            }
            else
            {
                LastChangeMessage = "تم الدفع بالكامل - لا يوجد باقي";
            }

            await HandleSaleCompletedAsync(result);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"تعذر إتمام عملية البيع:\n{ex.Message}", "خطأ");
        }
        finally
        {
            IsBusy = false;
            RequestBarcodeFocus();
        }
    }

    [RelayCommand]
    private void SetExactPaid()
    {
        PaidAmount = Totals.TotalAmount;
        NotifyPaymentProperties();
        RequestBarcodeFocus();
    }

    [RelayCommand]
    private void SelectPaymentMethod(string method)
    {
        if (Enum.TryParse<PaymentMethod>(method, true, out var parsed))
        {
            SelectedPaymentMethod = parsed;
        }
        RequestBarcodeFocus();
    }

    /// <summary>Called once the sale is committed.</summary>
    public async Task HandleSaleCompletedAsync(SaleResultDto result)
    {
        Cart.Clear();
        InvoiceDiscount = 0m;
        InvoiceDiscountPercentage = 0m;
        RecalculateTotals();
        PaidAmount = 0m;
        NotifyPaymentProperties();

        _currentShift = await _shiftService.GetOpenShiftAsync(CurrentUserId, TerminalId);
        ApplyShift(_currentShift);

        StatusMessage = $"تمت الفاتورة {result.InvoiceNumber} بمبلغ {result.TotalAmount:N2} {DomainConstants.CurrencySymbol} - المتبقي {result.ChangeAmount:N2}";
        SaleCompleted?.Invoke(this, result);
        RequestBarcodeFocus();
    }

    /// <summary>Set by the window right after sign-in; the sales service needs it.</summary>
    public int CurrentUserId { get; set; }

    // -------------------------------------------------------------- Permissions
    /// <summary>Applies the effective permission keys of the signed-in cashier.</summary>
    public void ApplyPermissions(IReadOnlySet<string> permissions, UserRole role)
    {
        _permissions = permissions.Count == 0
            ? AppPermissions.DefaultsFor(role).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        OnPropertyChanged(nameof(CanDiscount));
        OnPropertyChanged(nameof(CanCloseShift));
    }

    public bool CanDiscount =>
        _permissions.Contains(AppPermissions.PosDiscount) || _permissions.Contains(AppPermissions.ProductsEdit);

    public bool CanCloseShift => _permissions.Contains(AppPermissions.PosShiftClose);

    /// <summary>Id of the open shift, or null when the cashier has none.</summary>
    public int? CurrentShiftId => _currentShift?.Id;

    // ---------------------------------------------------------------- Totals
    /// <summary>Recomputes the totals panel. Local math mirrors the service so the UI stays instant.</summary>
    private void RecalculateTotals()
    {
        var lines = Cart.Select(l => l.ToDto()).ToList();

        var subTotal = Math.Round(lines.Sum(l => l.LineTotal), 2);
        var lineDiscounts = Math.Round(lines.Sum(l => l.DiscountAmount), 2);

        var discount = InvoiceDiscount;
        var percentage = InvoiceDiscountPercentage;

        if (discount <= 0m && percentage > 0m && subTotal > 0m)
        {
            discount = Math.Round(subTotal * Math.Min(percentage, 100m) / 100m, 2);
        }
        else if (discount > 0m && subTotal > 0m)
        {
            percentage = Math.Round(discount / subTotal * 100m, 2);
        }

        discount = Math.Min(Math.Max(discount, 0m), subTotal);

        var tax = Math.Round(lines.Sum(l => l.TaxAmount), 2);
        if (discount > 0m && subTotal > 0m)
        {
            tax = Math.Round(tax * (1m - discount / subTotal), 2);
        }

        // Prices already include VAT in Egyptian retail, so the discount is subtracted from the gross.
        var total = Math.Round(subTotal - discount, 2);

        Totals = new CartPricingDto
        {
            SubTotal = subTotal,
            LineDiscountTotal = lineDiscounts,
            InvoiceDiscount = discount,
            InvoiceDiscountPercentage = percentage,
            TaxAmount = tax,
            TotalAmount = total,
            TotalQuantity = lines.Sum(l => l.Quantity),
            LineCount = lines.Count
        };

        if (SelectedPaymentMethod != PaymentMethod.Cash || PaidAmount <= 0m || PaidAmount < total)
        {
            PaidAmount = total;
        }

        OnPropertyChanged(nameof(GrandTotalText));
        OnPropertyChanged(nameof(NetTotalText));
        OnPropertyChanged(nameof(TaxTotalText));
        OnPropertyChanged(nameof(DiscountTotalText));
        OnPropertyChanged(nameof(SubTotalText));
        OnPropertyChanged(nameof(ItemCountText));
        NotifyPaymentProperties();
    }

    partial void OnPaidAmountChanged(decimal value)
    {
        NotifyPaymentProperties();
    }

    partial void OnSelectedPaymentMethodChanged(PaymentMethod value)
    {
        if (value != PaymentMethod.Cash || PaidAmount < Totals.TotalAmount)
        {
            PaidAmount = Totals.TotalAmount;
        }

        NotifyPaymentProperties();
    }

    public void NotifyPaymentProperties()
    {
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(HasChange));
        OnPropertyChanged(nameof(IsFullyPaid));
        OnPropertyChanged(nameof(IsCash));
        OnPropertyChanged(nameof(IsWallet));
        OnPropertyChanged(nameof(IsCard));
    }

    partial void OnInvoiceDiscountChanged(decimal value)
    {
        if (value > 0m)
        {
            InvoiceDiscountPercentage = 0m;
        }

        RecalculateTotals();
    }

    partial void OnInvoiceDiscountPercentageChanged(decimal value)
    {
        if (value > 0m)
        {
            InvoiceDiscount = 0m;
        }

        RecalculateTotals();
    }

    private void RequestBarcodeFocus() => FocusBarcodeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Hook for the view to play a short beep on an unknown barcode.</summary>
    public Action? ErrorSound { get; set; }

    private void PlayError() => ErrorSound?.Invoke();
}
