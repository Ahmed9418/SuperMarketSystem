using System.Windows;
using Supermarket.Core.DTOs;
using Supermarket.POS.ViewModels;

namespace Supermarket.POS.Views;

/// <summary>
/// Payment dialog. Shows the totals, the payment method, quick cash notes and a numeric keypad;
/// on confirmation it hands the cart to <c>ISalesService</c> and exposes the committed sale to the
/// sales window so the receipt can be printed.
/// </summary>
public partial class PaymentWindow : Window
{
    private readonly PaymentViewModel _viewModel;

    public PaymentWindow(PaymentViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Completed += OnCompleted;
    }

    /// <summary>Loads the dialog with the cart and its live totals.</summary>
    public Task InitializeAsync(IReadOnlyList<CartLineDto> lines, CartPricingDto totals) =>
        _viewModel.InitializeAsync(lines, totals);

    /// <summary>Committed sale, or null when the cashier cancelled.</summary>
    public SaleResultDto? Result => _viewModel.Result;

    private void OnCompleted(object? sender, SaleResultDto result)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Completed -= OnCompleted;
        base.OnClosed(e);
    }
}
