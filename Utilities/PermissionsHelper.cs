using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;
using Lock.Services;  

namespace Lock.Services
{
    public static class PermissionsHelper
    {
        public static async Task<bool> RequestStoragePermissionsAsync()
        {
            try
            {
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    if (DeviceInfo.Version.Major >= 13) // Android 13+
                    {
                        // Request media permissions for Android 13+
                        var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                        return status == PermissionStatus.Granted;
                    }
                    else
                    {
                        // For Android 12 and below
                        var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                        return status == PermissionStatus.Granted;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Permission error: {ex}");
                return false;
            }
        }

        public static async Task<bool> CheckAndRequestPermissionsAsync()
        {
            try
            {
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    if (DeviceInfo.Version.Major >= 13) // Android 13+
                    {
                        var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                        if (status != PermissionStatus.Granted)
                        {
                            status = await Permissions.RequestAsync<Permissions.StorageRead>();
                        }
                        return status == PermissionStatus.Granted;
                    }
                    else
                    {
                        var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                        if (status != PermissionStatus.Granted)
                        {
                            status = await Permissions.RequestAsync<Permissions.StorageRead>();
                        }
                        return status == PermissionStatus.Granted;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Permission check error: {ex}");
                return false;
            }
        }
    }
}