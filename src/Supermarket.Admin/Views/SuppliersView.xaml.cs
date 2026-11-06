using System.Windows.Controls;
using System.Windows.Input;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>Supplier management page.</summary>
public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }

    /// <summary>Enter in the search box reloads the list.</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SuppliersViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
