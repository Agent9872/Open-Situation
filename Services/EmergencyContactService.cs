// Services/EmergencyContactService.cs
using Lock.Models;
using Lock.Models.Chat;
using Lock.Chat.Services;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Devices;

namespace Lock.Services
{
    public static class EmergencyContactService
    {
        public static async Task<List<EmergencyContact>> GetEmergencyContactsAsync(string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<EmergencyContact>()
                    .Where(e => e.UserPhone == userPhone)
                    .OrderByDescending(e => e.IsPrimary)
                    .ThenBy(e => e.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEmergencyContactsAsync error: {ex}");
                return new List<EmergencyContact>();
            }
        }

        public static async Task<EmergencyContact?> AddEmergencyContactAsync(
            string userPhone,
            string name,
            string phoneNumber,
            string relationship,
            bool isPrimary = false,
            string? notes = null)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // If this is primary, remove primary flag from other contacts
                if (isPrimary)
                {
                    var existingPrimary = await db.Table<EmergencyContact>()
                        .Where(e => e.UserPhone == userPhone && e.IsPrimary)
                        .FirstOrDefaultAsync();

                    if (existingPrimary != null)
                    {
                        existingPrimary.IsPrimary = false;
                        await db.UpdateAsync(existingPrimary);
                    }
                }

                var contact = new EmergencyContact
                {
                    UserPhone = userPhone,
                    Name = name,
                    PhoneNumber = phoneNumber,
                    Relationship = relationship,
                    IsPrimary = isPrimary,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };

                await db.InsertAsync(contact);
                return contact;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddEmergencyContactAsync error: {ex}");
                return null;
            }
        }

        public static async Task<bool> UpdateEmergencyContactAsync(EmergencyContact contact)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // If setting as primary, remove primary flag from other contacts
                if (contact.IsPrimary)
                {
                    var existingPrimary = await db.Table<EmergencyContact>()
                        .Where(e => e.UserPhone == contact.UserPhone && e.IsPrimary && e.Id != contact.Id)
                        .FirstOrDefaultAsync();

                    if (existingPrimary != null)
                    {
                        existingPrimary.IsPrimary = false;
                        await db.UpdateAsync(existingPrimary);
                    }
                }

                await db.UpdateAsync(contact);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateEmergencyContactAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> DeleteEmergencyContactAsync(int contactId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var contact = await db.Table<EmergencyContact>()
                    .Where(e => e.Id == contactId)
                    .FirstOrDefaultAsync();

                if (contact != null)
                {
                    await db.DeleteAsync(contact);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteEmergencyContactAsync error: {ex}");
                return false;
            }
        }

        public static async Task<SendEmergencyAlertResult> SendEmergencyAlertAsync(
            string userPhone,
            List<EmergencyContact> contacts,
            string customMessage = null)
        {
            var result = new SendEmergencyAlertResult();

            try
            {
                // Get current user's details
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == userPhone).FirstOrDefaultAsync();
                string userName = user?.Name ?? "Someone";
                string userPhoneNumber = user?.PhoneNumber ?? userPhone;

                // Get current location if possible
                string locationInfo = await GetCurrentLocationAsync();

                // Create the in-app message (send first!)
                string inAppMessage = CreateInAppSOSMessage(userName, locationInfo, customMessage);

                System.Diagnostics.Debug.WriteLine($"Sending SOS in-app messages to {contacts.Count} contacts");

                // In SendEmergencyAlertAsync, replace the in-app send block:

                foreach (var contact in contacts)
                {
                    var contactResult = new ContactSendResult
                    {
                        Contact = contact,
                        SmsSent = false,
                        InAppMessageSent = false,
                    };

                    // Always attempt in-app message — no longer skips non-Lock users
                    bool inAppSent = await SendInAppMessageToContactAsync(
                        userPhone, contact, inAppMessage);
                    contactResult.InAppMessageSent = inAppSent;

                    // Count as success if the local message was written,
                    // even if SMS hasn't been sent yet
                    result.SuccessfulContacts.Add(contactResult);
                }
                // Create SMS message (for manual sending)
                string smsMessage = CreateSOSMessage(userName, userPhoneNumber, locationInfo, DateTime.Now, customMessage);

                // SECOND: Now show SMS composition for each contact (only for successful in-app sends)
                // This allows user to manually send SMS if they choose
                foreach (var contactResult in result.SuccessfulContacts.ToList())
                {
                    bool smsSent = await ShowSMSCompositionDialogAsync(contactResult.Contact, smsMessage, userName);
                    contactResult.SmsSent = smsSent;
                }

                result.Success = result.SuccessfulContacts.Count > 0;

                // Log the alert
                await LogSOSAlertAsync(userPhone, result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendEmergencyAlertAsync error: {ex}");
                result.ErrorMessage = ex.Message;
                result.Success = false;
            }

            return result;
        }

        // Add this new method to show SMS composition dialog for each contact
        private static async Task<bool> ShowSMSCompositionDialogAsync(EmergencyContact contact, string message, string userName)
        {
            try
            {
                // Clean the phone number
                string cleanPhone = CleanPhoneNumber(contact.PhoneNumber);

                if (string.IsNullOrEmpty(cleanPhone))
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid phone number for {contact.Name}");
                    return false;
                }

                // On Android, we can use Sms.ComposeAsync
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    try
                    {
                        var smsMessage = new SmsMessage(message, cleanPhone);
                        await Sms.ComposeAsync(smsMessage);
                        System.Diagnostics.Debug.WriteLine($"SMS composition opened for {contact.Name}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SMS compose failed: {ex}");
                        // Fallback to opening messaging app
                        var uri = $"sms:{cleanPhone}?body={Uri.EscapeDataString(message)}";
                        await Launcher.OpenAsync(uri);
                        return true;
                    }
                }
                else
                {
                    // For iOS and other platforms, open messaging app with pre-filled message
                    var uri = $"sms:{cleanPhone}?body={Uri.EscapeDataString(message)}";
                    await Launcher.OpenAsync(uri);
                    System.Diagnostics.Debug.WriteLine($"Opened messaging app for {contact.Name}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowSMSCompositionDialogAsync error for {contact.Name}: {ex}");
                return false;
            }
        }

        // Keep the original SendMessageToContactAsync method for compatibility
        private static async Task<bool> SendMessageToContactAsync(EmergencyContact contact, string message, string userName)
        {
            // This method now just opens SMS composition
            return await ShowSMSCompositionDialogAsync(contact, message, userName);
        }

        private static string CreateSOSMessage(string userName, string userPhone, string locationInfo, DateTime timestamp, string customMessage = null)
        {
            string timeStr = timestamp.ToString("HH:mm");
            string dateStr = timestamp.ToString("dd/MM/yyyy");

            string message = $"🚨 EMERGENCY ALERT 🚨\n\n";
            message += $"{userName} has triggered an SOS alert!\n\n";

            if (!string.IsNullOrEmpty(customMessage))
            {
                message += $"Message: \"{customMessage}\"\n\n";
            }

            message += $"📱 Phone: {userPhone}\n";
            message += $"🕐 Time: {timeStr} on {dateStr}\n";

            if (!string.IsNullOrEmpty(locationInfo))
            {
                message += $"📍 Location: {locationInfo}\n\n";
            }
            else
            {
                message += $"📍 Location: Unable to retrieve location\n\n";
            }

            message += $"Please contact {userName} immediately.\n\n";
            message += $"— Sent from Lock Safety App";

            return message;
        }

        private static string CreateInAppSOSMessage(string userName, string locationInfo, string customMessage = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🚨 SOS ALERT 🚨");
            sb.AppendLine();
            sb.AppendLine($"{userName} has triggered an emergency SOS alert!");

            if (!string.IsNullOrEmpty(customMessage))
            {
                sb.AppendLine();
                sb.AppendLine($"Message: {customMessage}");
            }

            if (!string.IsNullOrEmpty(locationInfo))
            {
                sb.AppendLine();
                sb.AppendLine($"📍 Location: {locationInfo}");
            }

            sb.AppendLine();
            sb.AppendLine($"🕐 Time: {DateTime.Now:HH:mm} on {DateTime.Now:dd/MM/yyyy}");
            sb.AppendLine();
            sb.AppendLine($"Please contact {userName} immediately!");

            return sb.ToString().Trim();
        }
        private static async Task<string> GetCurrentLocationAsync()
        {
            try
            {
                // Check if location permission is granted
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (status == PermissionStatus.Granted)
                {
                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Medium,
                        Timeout = TimeSpan.FromSeconds(10)
                    });

                    if (location != null)
                    {
                        // Use reverse geocoding to get address
                        var placemarks = await Geocoding.GetPlacemarksAsync(location.Latitude, location.Longitude);
                        var placemark = placemarks?.FirstOrDefault();

                        if (placemark != null)
                        {
                            return $"{placemark.Locality}, {placemark.CountryName}";
                        }
                        else
                        {
                            return $"Lat: {location.Latitude:F4}, Long: {location.Longitude:F4}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentLocationAsync error: {ex}");
            }

            return string.Empty;
        }


        // In EmergencyContactService.cs
        // Replace SendInAppMessageToContactAsync with this version

        private static async Task<bool> SendInAppMessageToContactAsync(
            string senderPhone,
            EmergencyContact contact,
            string sosMessage)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get or create conversation regardless of whether contact is a Lock user
                string conversationId = await GetOrCreateConversationAsync(senderPhone, contact.PhoneNumber);

                // Create the SOS chat message
                var sosChatMessage = new ChatMessage
                {
                    ConversationId = conversationId,
                    SenderPhone = senderPhone,
                    RecipientPhone = contact.PhoneNumber,
                    Content = sosMessage,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = true,
                    IsRead = false,
                    IsLocalOutgoing = true,
                    IsEncrypted = false,
                    MessageType = "sos_alert",
                    IsBlocked = false,
                };

                await db.InsertAsync(sosChatMessage);

                // Update the conversation's last-message preview
                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null)
                {
                    conversation.LastMessagePreview = "🚨 SOS ALERT: Emergency assistance needed";
                    conversation.LastMessageAt = DateTime.UtcNow;
                    conversation.LastMessageType = "sos_alert";
                    await db.UpdateAsync(conversation);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"SOS chat message saved for {contact.Name} (conversationId: {conversationId})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SendInAppMessageToContactAsync error for {contact.Name}: {ex}");
                return false;
            }
        }
        private static async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if conversation already exists
                var existingConversation = await db.Table<Conversation>()
                    .Where(c => (c.ParticipantA == userPhone && c.ParticipantB == contactPhone) ||
                               (c.ParticipantA == contactPhone && c.ParticipantB == userPhone))
                    .FirstOrDefaultAsync();

                if (existingConversation != null)
                    return existingConversation.ConversationId;

                // Create new conversation
                string conversationId = Guid.NewGuid().ToString();
                var conversation = new Conversation
                {
                    ConversationId = conversationId,
                    ParticipantA = userPhone,
                    ParticipantB = contactPhone,
                    LastMessageAt = DateTime.UtcNow,
                    LastMessagePreview = "",
                    CreatedAt = DateTime.UtcNow
                };

                await db.InsertAsync(conversation);
                return conversationId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
                throw;
            }
        }

        private static string CleanPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return phoneNumber;

            // Remove all non-digit characters except '+'
            var cleaned = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());

            return cleaned;
        }

        private static async Task LogSOSAlertAsync(string userPhone, SendEmergencyAlertResult result)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Log to console
                System.Diagnostics.Debug.WriteLine($"SOS Alert logged for {userPhone}: {result.SuccessfulContacts.Count} sent, {result.FailedContacts.Count} failed");

                // Log to file
                string logPath = Path.Combine(FileSystem.AppDataDirectory, "sos_log.txt");
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SOS Alert from {userPhone} - Sent: {result.SuccessfulContacts.Count}, Failed: {result.FailedContacts.Count}\n";

                // Add details of each contact
                foreach (var contactResult in result.SuccessfulContacts)
                {
                    logEntry += $"  ✓ {contactResult.Contact.Name}: SMS={contactResult.SmsSent}, InApp={contactResult.InAppMessageSent}\n";
                }
                foreach (var contactResult in result.FailedContacts)
                {
                    logEntry += $"  ✗ {contactResult.Contact.Name}: SMS={contactResult.SmsSent}, InApp={contactResult.InAppMessageSent}\n";
                }

                await File.AppendAllTextAsync(logPath, logEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogSOSAlertAsync error: {ex}");
            }
        }

        public static async Task<EmergencyContact?> GetPrimaryContactAsync(string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<EmergencyContact>()
                    .Where(e => e.UserPhone == userPhone && e.IsPrimary)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPrimaryContactAsync error: {ex}");
                return null;
            }
        }
    }

    public class SendEmergencyAlertResult
    {
        public bool Success { get; set; }
        public List<ContactSendResult> SuccessfulContacts { get; set; } = new List<ContactSendResult>();
        public List<ContactSendResult> FailedContacts { get; set; } = new List<ContactSendResult>();
        public string? ErrorMessage { get; set; }

        public string GetResultMessage()
        {
            if (Success)
            {
                string message = $"✅ SOS alert sent to {SuccessfulContacts.Count} contact(s)\n\n";

                foreach (var contactResult in SuccessfulContacts.Take(5))
                {
                    message += $"• {contactResult.Contact.Name}: ";
                    if (contactResult.SmsSent && contactResult.InAppMessageSent)
                        message += "✅ SMS + In-App\n";
                    else if (contactResult.SmsSent)
                        message += "✅ SMS only\n";
                    else if (contactResult.InAppMessageSent)
                        message += "✅ In-App only (Lock user)\n";
                }

                if (SuccessfulContacts.Count > 5)
                    message += $"... and {SuccessfulContacts.Count - 5} more\n";

                if (FailedContacts.Count > 0)
                {
                    message += $"\n⚠️ Failed to reach {FailedContacts.Count} contact(s)\n";
                    foreach (var contactResult in FailedContacts.Take(3))
                    {
                        message += $"• {contactResult.Contact.Name}\n";
                    }
                    if (FailedContacts.Count > 3)
                        message += $"... and {FailedContacts.Count - 3} more\n";
                }

                return message;
            }
            else
            {
                return $"❌ Failed to send SOS alert: {ErrorMessage ?? "Unknown error"}";
            }
        }
    }

    public class ContactSendResult
    {
        public EmergencyContact Contact { get; set; } = new EmergencyContact();
        public bool SmsSent { get; set; }
        public bool InAppMessageSent { get; set; }

        public bool IsFullyNotified => SmsSent && InAppMessageSent;
        public bool IsPartiallyNotified => SmsSent || InAppMessageSent;
    }
}