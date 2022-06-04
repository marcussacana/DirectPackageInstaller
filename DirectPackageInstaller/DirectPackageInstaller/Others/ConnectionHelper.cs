using System;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace DirectPackageInstaller;

public class ConnectionHelper
{
    public static async Task Reset()
    {
        if (!AllowReconnect || !await Ipv6Connected())
            return;
        
        switch (App.CurrentPlatform)
        {
            case App.OS.Linux:
                await LinuxReset();
                break;
            case App.OS.Windows:
                await WindowsReset();
                break;
        }

        await WaitIPV6Connect();
    }
    static async Task LinuxReset()
    { 
        await Process.Start("nmcli", "n off").WaitForExitAsync();
        await Task.Delay(500);
        await Process.Start("nmcli", "n on").WaitForExitAsync();
    }

#pragma warning disable CA1416 
    public static bool IsWindowsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return true;

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static async Task WindowsReset()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        SelectQuery wmiQuery = new SelectQuery("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
        ManagementObjectSearcher searchProcedure = new ManagementObjectSearcher(wmiQuery);
        foreach (ManagementObject item in searchProcedure.Get())
        {
            try
            {
                item.InvokeMethod("Disable", null);
            }catch {
                continue;
            }

            await Task.Delay(500);

            try
            {
                item.InvokeMethod("Enable", null);
            }
            catch
            {
                continue;
            }
        }
    }
#pragma warning restore CA1416

    public static async Task WaitIPV6Connect()
    {
        while (true)
        {
            if (await Ipv6Connected())
                break;
            
            await Task.Delay(500); 
        }
    }

    public static async Task<bool> Ipv6Connected()
    {
        HttpWebRequest Request = WebRequest.CreateHttp("http://ipv6.google.com");
        HttpWebResponse Response = null;
        Request.ConnectionGroupName = Guid.NewGuid().ToString();
        try
        {
            Request.Timeout = 10000;
            Response = (HttpWebResponse) Request.GetResponse();
            if (Response.StatusCode == HttpStatusCode.OK)
                return true;
        }
        catch  { }
        finally
        {
            Response?.Close();
            Request.ServicePoint.CloseConnectionGroup(Request.ConnectionGroupName);
        }

        return false;
    }

    public static bool AllowReconnect;
}