using System;
using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.Win32;
using StartUpAsAdmin.ViewModels;

namespace StartUpAsAdmin
{
    public partial class StartUpAsAdminSettingsPage : Page
    {
        private const string RegistryKeyPath = @"Software\ClassIsland\StartUpAsAdmin";
        private const string RegistryValueName = "StartupMode";

        private readonly StartUpAsAdminSettingsViewModel _vm;

        public StartUpAsAdminSettingsPage()
        {
            InitializeComponent();

            _vm = new StartUpAsAdminSettingsViewModel
            {
                IsRunningAsAdmin = AdminHelper.IsRunningInAdmin()
            };

            // 从注册表加载用户上次选择
            try
            {
                var saved = LoadStartupModeFromRegistry();
                if (saved.HasValue)
                    _vm.SelectedStartupMode = saved.Value;
            }
            catch
            {
                // 忽略加载错误，保持默认
            }

            // 保存选择变化
            _vm.PropertyChanged += Vm_PropertyChanged;

            this.DataContext = _vm;
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StartUpAsAdminSettingsViewModel.SelectedStartupMode))
            {
                try
                {
                    SaveStartupModeToRegistry(_vm.SelectedStartupMode);
                }
                catch
                {
                    // 忽略保存错误（不阻塞 UI）
                }
            }
        }

        private static void SaveStartupModeToRegistry(StartupMode mode)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key.SetValue(RegistryValueName, mode.ToString(), RegistryValueKind.String);
            }
            catch
            {
                throw;
            }
        }

        private static StartupMode? LoadStartupModeFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                var val = key?.GetValue(RegistryValueName) as string;
                if (string.IsNullOrEmpty(val)) return null;
                if (Enum.TryParse<StartupMode>(val, ignoreCase: true, out var mode))
                    return mode;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}