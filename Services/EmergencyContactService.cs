// Services/EmergencyContactService.cs
using Lock.Models;
using Lock.Models.Chat;
using Lock.Chat.Services;
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
                return await SupabaseService.GetAsync<EmergencyContact>("EmergencyContacts",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=IsPrimary.desc,Name.asc");
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
                // If this is primary, remove primary flag from other contacts
                if (isPrimary)
                {
                    var existingPrimary = await SupabaseService.GetAsync<EmergencyContact>("EmergencyContacts",
                        $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&IsPrimary=eq.true&limit=1");

                    if (existingPrimary.Any())
                    {
                        var primary = existingPrimary.First();
                        primary.IsPrimary = false;
                        await SupabaseService.UpdateAsync("EmergencyContacts", $"Id=eq.{primary.Id}",
                            new { IsPrimary = false });
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

                var inserted = await SupabaseService.InsertAndReturnAsync<EmergencyContact>("EmergencyContacts", contact);
                return inserted;
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
                // If setting as primary, remove primary flag from other contacts
                if (contact.IsPrimary)
                {
                    var existingPrimary = await SupabaseService.GetAsync<EmergencyContact>("EmergencyContacts",
                        $"UserPhone=eq.{Uri.EscapeDataString(contact.UserPhone)}&IsPrimary=eq.true&Id=ne.{contact.Id}&limit=1");

                    if (existingPrimary.Any())
                    {
                        var primary = existingPrimary.First();
                        primary.IsPrimary = false;
                        await SupabaseService.UpdateAsync("EmergencyContacts", $"Id=eq.{primary.Id}",
                            new { IsPrimary = false });
                    }
                }

                return await SupabaseService.UpdateAsync("EmergencyContacts", $"Id=eq.{contact.Id}", contact);
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
                return await SupabaseService.DeleteAsync("EmergencyContacts", $"Id=eq.{contactId}");
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
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
                var user = users.FirstOrDefault();
                string userName = user?.Name ?? "Someone";
                string userPhoneNumber = user?.PhoneNumber ?? userPhone;

                // Get current location if possible
                string locationInfo = await GetCurrentLocationAsync();

                // Create the in-app message
                string inAppMessage = CreateInAppSOSMessage(userName, locationInfo, customMessage);

                System.Diagnostics.Debug.WriteLine($"Sending SOS in-app messages to {contacts.Count} contacts");

                foreach (var contact in contacts)
                {
                    var contactResult = new ContactSendResult
                    {
                        Contact = contact,
                        SmsSent = false,
                        InAppMessageSent = false,
                    };

                    // Attempt in-app message
                    bool inAppSent = await SendInAppMessageToContactAsync(userPhone, contact, inAppMessage);
                    contactResult.InAppMessageSent = inAppSent;

                    result.SuccessfulContacts.Add(contactResult);
                }

                // Create SMS message
                string smsMessage = CreateSOSMessage(userName, userPhoneNumber, locationInfo, DateTime.Now, customMessage);

                // Show SMS composition for each contact
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

        private static async Task<bool> ShowSMSCompositionDialogAsync(EmergencyContact contact, string message, string userName)
        {
            try
            {
                string cleanPhone = CleanPhoneNumber(contact.PhoneNumber);

                if (string.IsNullOrEmpty(cleanPhone))
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid phone number for {contact.Name}");
                    return false;
                }

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
                        var uri = $"sms:{cleanPhone}?body={Uri.EscapeDataString(message)}";
                        await Launcher.OpenAsync(uri);
                        return true;
                    }
                }
                else
                {
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

        private static async Task<bool> SendInAppMessageToContactAsync(
            string senderPhone,
            EmergencyContact contact,
            string sosMessage)
        {
            try
            {
                // Get or create conversation
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
                };

                await SupabaseService.InsertAsync("ChatMessages", sosChatMessage);

                // Update the conversation's last-message preview
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&limit=1");
                var conversation = conversations.FirstOrDefault();

                if (conversation != null)
                {
                    await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                        new
                        {
                            LastMessagePreview = "🚨 SOS ALERT: Emergency assistance needed",
                            LastMessageAt = DateTime.UtcNow,
                            LastMessageType = "sos_alert"
                        });
                }

                System.Diagnostics.Debug.WriteLine($"SOS chat message saved for {contact.Name} (conversationId: {conversationId})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendInAppMessageToContactAsync error for {contact.Name}: {ex}");
                return false;
            }
        }

        private static async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone)
        {
            try
            {
                // Check if conversation already exists
                var existingConvs = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(and(ParticipantA.eq.{Uri.EscapeDataString(userPhone)},ParticipantB.eq.{Uri.EscapeDataString(contactPhone)})," +
                    $"and(ParticipantA.eq.{Uri.EscapeDataString(contactPhone)},ParticipantB.eq.{Uri.EscapeDataString(userPhone)}))&limit=1");

                if (existingConvs.Any())
                    return existingConvs.First().ConversationId;

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

                await SupabaseService.InsertAsync("Conversations", conversation);
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

            var cleaned = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());
            return cleaned;
        }

        private static async Task LogSOSAlertAsync(string userPhone, SendEmergencyAlertResult result)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"SOS Alert logged for {userPhone}: {result.SuccessfulContacts.Count} sent, {result.FailedContacts.Count} failed");

                string logPath = Path.Combine(FileSystem.AppDataDirectory, "sos_log.txt");
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SOS Alert from {userPhone} - Sent: {result.SuccessfulContacts.Count}, Failed: {result.FailedContacts.Count}\n";

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
                var contacts = await SupabaseService.GetAsync<EmergencyContact>("EmergencyContacts",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&IsPrimary=eq.true&limit=1");
                return contacts.FirstOrDefault();
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