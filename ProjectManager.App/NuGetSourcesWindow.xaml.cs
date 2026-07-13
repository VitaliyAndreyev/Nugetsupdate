using System.Windows;
using Microsoft.Win32;
using ProjectManager.App.ViewModels;

namespace ProjectManager.App;

public partial class NuGetSourcesWindow : Window
{
    public NuGetSourcesWindow()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Close();

    private void BrowseMSBuild_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MSBuild.exe|MSBuild.exe|Executable files (*.exe)|*.exe",
            Title = "Select MSBuild.exe"
        };

        if (dialog.ShowDialog(this) == true && DataContext is MainViewModel viewModel)
        {
            viewModel.MSBuildPath = dialog.FileName;
        }
    }
}
