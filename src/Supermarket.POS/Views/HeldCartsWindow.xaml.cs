using System.Windows;
using System.Windows.Input;
using Supermarket.Core.DTOs;

namespace Supermarket.POS.Views;

/// <summary>
/// Resume / discard screen for the parked baskets (F8 on the sales screen).
/// The window only picks an action: the sales window performs the service call so a resumed basket
/// can flow straight back into the cart.
/// </summary>
public partial class HeldCartsWindow : Window
{
    public HeldCartsWindow(IReadOnlyList<HeldCartDto> carts)
    {
        InitializeComponent();
        Carts = carts;
        DataContext = this;
    }

    public IReadOnlyList<HeldCartDto> Carts { get; }

    /// <summary>The basket the cashier chose to resume, or null.</summary>
    public HeldCartDto? SelectedCart { get; private set; }

    /// <summary>The basket the cashier chose to delete, or null.</summary>
    public HeldCartDto? DeletedCart { get; private set; }

    public bool CloseRequested { get; private set; }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e) => ResumeSelected();

    private void OnResumeClick(object sender, RoutedEventArgs e) => ResumeSelected();

    private void ResumeSelected()
    {
        if (HeldGrid.SelectedItem is not HeldCartDto cart)
        {
            MessageBox.Show("اختر سلة من القائمة أولاً", "تنبيه", MessageBoxButton.OK,
                MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        SelectedCart = cart;
        DialogResult = true;
        Close();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (HeldGrid.SelectedItem is not HeldCartDto cart)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"هل تريد حذف السلة المعلقة ({cart.Label})؟\nلا يمكن التراجع عن هذه العملية.",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        DeletedCart = cart;
        DialogResult = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested = true;
        DialogResult = false;
        Close();
    }
}
