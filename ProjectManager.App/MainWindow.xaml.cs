using ProjectManager.App.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ProjectManager.App;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    DataContext = new MainViewModel();
  }

  private void NuGetSources_Click(object sender, RoutedEventArgs e)
  {
    var window = new NuGetSourcesWindow
    {
      Owner = this,
      DataContext = DataContext
    };
    window.ShowDialog();
  }

  private void PackageTargets_Click(object sender, RoutedEventArgs e)
  {
    var window = new PackageTargetsWindow
    {
      Owner = this,
      DataContext = DataContext
    };
    window.ShowDialog();
  }

  private void WorkflowLog_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (sender is TextBox textBox)
    {
      textBox.ScrollToEnd();
    }
  }

}
