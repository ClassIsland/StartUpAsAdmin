using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Interactivity;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Win32.TaskScheduler;
using StartUpAsAdmin.ViewModels;
using YamlDotNet.Core;

namespace StartUpAsAdmin;

/// <summary>
/// StartUpAsAdminSettingsPage.xaml 的交互逻辑
/// </summary>
[SettingsPageInfo("classisland.startUpAsAdmin", "管理员自启动", "\uef5d", "\uef5c")]
public partial class StartUpAsAdminSettingsPage : SettingsPageBase
{
    private const string TaskName = "ClassIsland.AdminStartup";

    public StartUpAsAdminSettingsViewModel ViewModel { get; } = new();

    public StartUpAsAdminSettingsPage()
    {
        DataContext = this;
        InitializeComponent();
        ViewModel.IsRunningAsAdmin = AdminHelper.IsRunningInAdmin();
    }

    private void ButtonCreateTask_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var taskService = new TaskService();
            var task = taskService.NewTask();

            task.Triggers.Add(new LogonTrigger()
            {
                UserId = Environment.UserName
            });
            task.Actions.Add(new ExecAction()
            {
                Path = Environment.ProcessPath?.Replace(".dll", ".exe"),
                WorkingDirectory = Environment.CurrentDirectory
            });
            //task.Settings.RunOnlyIfLoggedOn = true;
            task.Settings.RunOnlyIfIdle = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            task.Principal.RunLevel = TaskRunLevel.Highest;
            taskService.RootFolder.RegisterTaskDefinition(TaskName, task, TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken);
            PlatformServices.DesktopService.IsAutoStartEnabled = false;
            this.ShowSuccessToast("成功创建/更新了计划任务。");
        }
        catch (Exception exception)
        {
            this.ShowErrorToast("无法创建计划任务", exception);
        }
        
    }

    private void ButtonRestartAsAdmin_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = Environment.ProcessPath?.Replace(".dll", ".exe"),
                ArgumentList = { "-m", "--uri", "classisland://app/settings/classisland.startUpAsAdmin" },
                Verb = "runas",
                UseShellExecute = true
            };
            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0);
            foreach (var i in args)
            {
                processStartInfo.ArgumentList.Add(i);
            }
            Process.Start(processStartInfo);
            AppBase.Current.Stop();

        }
        catch (Exception exception)
        {
            this.ShowErrorToast("无法以管理员身份重启", exception);
        }
    }

    private void ButtonRemoveTask_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var taskService = new TaskService();
            taskService.RootFolder.DeleteTask(TaskName);
            this.ShowSuccessToast("成功删除了计划任务。");
        }
        catch (Exception exception)
        {
            this.ShowErrorToast("无法删除计划任务", exception);
        }
    }
}