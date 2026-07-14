using ProjectManager.App.ViewModels;
using System.Windows;

namespace ProjectManager.App;

public partial class PackageTargetsWindow : Window
{
  public PackageTargetsWindow()
  {
    InitializeComponent();
  }

  private void Apply_Click(object sender, RoutedEventArgs e)
  {
    if (DataContext is MainViewModel viewModel && viewModel.ApplyPackageTargetsCommand.CanExecute(null))
    {
      viewModel.ApplyPackageTargetsCommand.Execute(null);
      Close();
    }
  }
}
