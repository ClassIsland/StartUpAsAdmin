using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace StartUpAsAdmin.ViewModels;

public enum StartupMode
{
    Normal,      // 普通启动
    AdminTask,   // 以管理员身份启动（计划任务）
    Service,     // 以服务方式启动
    Disabled     // 禁用自启动
}

public partial class StartUpAsAdminSettingsViewModel : ObservableObject
{
    private const string RegistryKeyPath = @"Software\ClassIsland\StartUpAsAdmin";
    private const string RegistryValueName = "StartupMode";
    
    [ObservableProperty] 
    private bool _isRunningAsAdmin = false;
    
    [ObservableProperty]
    private StartupMode _selectedStartupMode = StartupMode.Normal;
    
    [ObservableProperty]
    private bool _isTaskCreated = false;
    
    public StartUpAsAdminSettingsViewModel()
    {
        // 从注册表加载上次选择
        LoadStartupMode();
        
        // 检查计划任务是否存在
        CheckTaskStatus();
    }
    
    partial void OnSelectedStartupModeChanged(StartupMode value)
    {
        SaveStartupMode(value);
        ApplyStartupMode(value);
    }
    
    private void LoadStartupMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            var val = key?.GetValue(RegistryValueName) as string;
            if (!string.IsNullOrEmpty(val) && Enum.TryParse<StartupMode>(val, out var mode))
            {
                SelectedStartupMode = mode;
            }
        }
        catch
        {
            // 忽略错误
        }
    }
    
    private void SaveStartupMode(StartupMode mode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key?.SetValue(RegistryValueName, mode.ToString());
        }
        catch
        {
            // 忽略保存错误
        }
    }
    
    private void ApplyStartupMode(StartupMode mode)
    {
        if (!IsRunningAsAdmin) return;
        
        switch (mode)
        {
            case StartupMode.Normal:
                // 移除计划任务和服务
                RemoveScheduledTask();
                RemoveService();
                break;
                
            case StartupMode.AdminTask:
                // 创建计划任务
                CreateScheduledTask();
                RemoveService();
                break;
                
            case StartupMode.Service:
                // 安装服务
                InstallService();
                RemoveScheduledTask();
                break;
                
            case StartupMode.Disabled:
                // 全部移除
                RemoveScheduledTask();
                RemoveService();
                break;
        }
    }
    
    [RelayCommand]
    private void CreateScheduledTask()
    {
        if (!IsRunningAsAdmin) return;
        
        // 调用 TaskSchedulerHelper 创建计划任务
        IsTaskCreated = TaskSchedulerHelper.CreateStartupTask();
    }
    
    [RelayCommand]
    private void RemoveScheduledTask()
    {
        if (!IsRunningAsAdmin) return;
        
        // 调用 TaskSchedulerHelper 删除计划任务
        TaskSchedulerHelper.DeleteStartupTask();
        IsTaskCreated = false;
    }
    
    private void CheckTaskStatus()
    {
        IsTaskCreated = TaskSchedulerHelper.IsTaskExists();
    }
    
    private void InstallService()
    {
        // 调用 CIServerServiceInstaller.InstallService
    }
    
    private void RemoveService()
    {
        // 调用 CIServerServiceInstaller.UninstallService
    }
}