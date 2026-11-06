using System.Windows;
using Supermarket.Admin.ViewModels;

namespace Supermarket.Admin.Views;

/// <summary>Shell window hosting the admin pages.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) => _viewModel.Initialize();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
