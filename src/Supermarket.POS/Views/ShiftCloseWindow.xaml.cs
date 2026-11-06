using System.Windows;
using Supermarket.POS.ViewModels;

namespace Supermarket.POS.Views;

/// <summary>
/// Shift closing window (تقفيل الورديّة): X / Z preview, counted cash, difference and printing.
/// </summary>
public partial class ShiftCloseWindow : Window
{
    private readonly ShiftCloseViewModel _viewModel;

    public ShiftCloseWindow(ShiftCloseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }

    /// <summary>Loads the shift and its live figures.</summary>
    public Task InitializeAsync(int shiftId, bool isShiftClosed) => _viewModel.InitializeAsync(shiftId, isShiftClosed);

    /// <summary>True when the shift was actually closed in this dialog.</summary>
    public bool ShiftWasClosed => _viewModel.IsClosed;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
