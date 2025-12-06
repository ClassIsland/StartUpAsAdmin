// TaskSchedulerHelper.cs
using Microsoft.Win32.TaskScheduler;

namespace StartUpAsAdmin;

public static class TaskSchedulerHelper
{
    private const string TaskName = "ClassIslandStartUpAdmin";
    
    public static bool CreateStartupTask()
    {
        try
        {
            using var ts = new TaskService();
            
            // 删除已存在的任务
            ts.RootFolder.DeleteTask(TaskName, false);
            
            // 创建新任务
            var td = ts.NewTask();
            td.RegistrationInfo.Description = "以管理员身份启动 ClassIsland";
            
            // 触发器：用户登录时
            td.Triggers.Add(new LogonTrigger { UserId = Environment.UserName });
            
            // 操作：启动 ClassIsland.exe
            var exePath = GetClassIslandExePath();
            td.Actions.Add(new ExecAction(exePath, "--uri classisland://app/settings/classisland.startUpAsAdmin"));
            
            // 设置：以最高权限运行
            td.Principal.RunLevel = TaskRunLevel.Highest;
            
            // 注册任务
            ts.RootFolder.RegisterTaskDefinition(TaskName, td);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public static bool DeleteStartupTask()
    {
        try
        {
            using var ts = new TaskService();
            ts.RootFolder.DeleteTask(TaskName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public static bool IsTaskExists()
    {
        try
        {
            using var ts = new TaskService();
            return ts.GetTask(TaskName) != null;
        }
        catch
        {
            return false;
        }
    }
    
    private static string GetClassIslandExePath()
    {
        // 从注册表或默认路径获取 ClassIsland.exe 路径
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "ClassIsland", 
            "ClassIsland.exe");
    }
}