using Prism.Workbench.Wpf.Services;
using Prism.Workbench.Wpf.ViewModels;
using System.Windows;

namespace Prism.Workbench.Wpf.Views;

/// <summary>
/// Hosts the desktop workbench surface.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main workbench window and its view model.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(new PrismCoreWorkbenchService());
    }
}
