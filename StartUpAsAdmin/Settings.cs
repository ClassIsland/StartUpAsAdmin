using CommunityToolkit.Mvvm.ComponentModel;

namespace StartUpAsAdmin;

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    private bool _autoElevateAdmin = false;
}