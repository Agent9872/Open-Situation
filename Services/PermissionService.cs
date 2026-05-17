using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class PermissionService
    {
        public static async Task<bool> RequestSOSPermissionsAsync()
        {
            try
            {
                bool allGranted = true;

                // Request location permission
                var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locationStatus != PermissionStatus.Granted)
                {
                    locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (locationStatus != PermissionStatus.Granted)
                {
                    allGranted = false;
                }

                // On Android, also request SMS permission
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    var smsStatus = await Permissions.CheckStatusAsync<Permissions.Sms>();
                    if (smsStatus != PermissionStatus.Granted)
                    {
                        smsStatus = await Permissions.RequestAsync<Permissions.Sms>();
                    }

                    if (smsStatus != PermissionStatus.Granted)
                    {
                        allGranted = false;
                    }
                }

                return allGranted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PermissionService.RequestSOSPermissionsAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> RequestLocationPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestLocationPermissionAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> RequestSmsPermissionAsync()
        {
            try
            {
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.Sms>();
                    }
                    return status == PermissionStatus.Granted;
                }

                // iOS doesn't need explicit SMS permission
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestSmsPermissionAsync error: {ex}");
                return false;
            }
        }
    }
}