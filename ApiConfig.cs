using System;
using Microsoft.Maui.Devices;

namespace Lock
{
    public static class ApiConfig
    {
        // Production settings
        private const string ProductionBaseUrl = "https://yourdomain.smarterasp.net"; // Use HTTPS in production!
        private const string DevelopmentBaseUrl = "http://localhost:5000";
        private const string AndroidEmulatorBaseUrl = "http://10.0.2.2:5000";

        public static string BaseUrl
        {
            get
            {
#if DEBUG
                // Development mode
                if (DeviceInfo.Platform == DevicePlatform.Android)
                    return AndroidEmulatorBaseUrl;
                else
                    return DevelopmentBaseUrl;
#else
                // Production mode
                return ProductionBaseUrl;
#endif
            }
        }

        public static string HubUrl => $"{BaseUrl}/messageHub";

        public static class Endpoints
        {
            public static string Login => $"{BaseUrl}/api/auth/login";
            public static string Register => $"{BaseUrl}/api/auth/register";
            public static string Refresh => $"{BaseUrl}/api/auth/refresh";
            public static string Logout => $"{BaseUrl}/api/auth/logout";
            public static string Validate => $"{BaseUrl}/api/auth/validate";
            public static string Messages => $"{BaseUrl}/api/messages";
            public static string Conversations => $"{BaseUrl}/api/conversations";
            public static string SendMessage => $"{BaseUrl}/api/messages/send";
            public static string MarkAsRead => $"{BaseUrl}/api/messages/mark-as-read";
            public static string DeleteMessage => $"{BaseUrl}/api/messages/delete";
            public static string UserProfile => $"{BaseUrl}/api/user/profile";
            public static string UpdateProfile => $"{BaseUrl}/api/user/update";
            public static string ChangePassword => $"{BaseUrl}/api/user/change-password";
            public static string DeleteAccount => $"{BaseUrl}/api/user/delete";
            public static string SearchUsers => $"{BaseUrl}/api/search/users";
            public static string GetUserByPhone => $"{BaseUrl}/api/user/by-phone";
        }
    }
}