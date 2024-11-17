using System.Security.Principal;

namespace StartUpAsAdmin;

public static class AdminHelper
{
    public static bool IsRunningInAdmin()
    {
        var id = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(id);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}