using CommunityToolkit.Mvvm.ComponentModel;

namespace StartUpAsAdmin.ViewModels;

public partial class StartUpAsAdminSettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _isRunningAsAdmin = false;
}