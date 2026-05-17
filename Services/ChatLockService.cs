using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Lock.Services
{
    public class ChatLockService
    {
        private const string LockEnabledKey = "chat_lock_enabled_{0}";
        private const string LockTypeKey = "chat_lock_type_{0}";
        private const string LockPinKey = "chat_lock_pin_{0}";
        private const string LockPatternKey = "chat_lock_pattern_{0}";

        public enum LockType
        {
            None,
            Pin,
            Biometric,
            Pattern
        }

        public static async Task<bool> IsChatLockedAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return false;
            string enabledKey = string.Format(LockEnabledKey, conversationId);
            return await Task.Run(() => Preferences.Get(enabledKey, false));
        }

        public static async Task<LockType> GetLockTypeAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId)) return LockType.None;
            string lockTypeKey = string.Format(LockTypeKey, conversationId);
            int lockTypeValue = await Task.Run(() => Preferences.Get(lockTypeKey, (int)LockType.None));
            return (LockType)lockTypeValue;
        }

        public static async Task<bool> SetChatLockAsync(string conversationId, LockType lockType, string? pinOrPattern = null)
        {
            try
            {
                if (string.IsNullOrEmpty(conversationId)) return false;

                string enabledKey = string.Format(LockEnabledKey, conversationId);
                string lockTypeKey = string.Format(LockTypeKey, conversationId);

                if (lockType == LockType.None)
                {
                    // Remove lock
                    await Task.Run(() => Preferences.Set(enabledKey, false));
                    await Task.Run(() => Preferences.Set(lockTypeKey, (int)LockType.None));

                    // Clear stored credentials
                    string pinKey = string.Format(LockPinKey, conversationId);
                    string patternKey = string.Format(LockPatternKey, conversationId);
                    await Task.Run(() => Preferences.Set(pinKey, string.Empty));
                    await Task.Run(() => Preferences.Set(patternKey, string.Empty));
                }
                else
                {
                    // Set lock
                    await Task.Run(() => Preferences.Set(enabledKey, true));
                    await Task.Run(() => Preferences.Set(lockTypeKey, (int)lockType));

                    if (lockType == LockType.Pin && !string.IsNullOrEmpty(pinOrPattern))
                    {
                        string pinKey = string.Format(LockPinKey, conversationId);
                        await Task.Run(() => Preferences.Set(pinKey, pinOrPattern));
                    }
                    else if (lockType == LockType.Pattern && !string.IsNullOrEmpty(pinOrPattern))
                    {
                        string patternKey = string.Format(LockPatternKey, conversationId);
                        await Task.Run(() => Preferences.Set(patternKey, pinOrPattern));
                    }
                    // Biometric doesn't store any credentials - just the lock type
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetChatLockAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> VerifyChatLockAsync(string conversationId, string input, LockType lockType)
        {
            try
            {
                if (string.IsNullOrEmpty(conversationId)) return false;

                if (lockType == LockType.Pin)
                {
                    string pinKey = string.Format(LockPinKey, conversationId);
                    string storedPin = await Task.Run(() => Preferences.Get(pinKey, string.Empty));
                    return storedPin == input;
                }
                else if (lockType == LockType.Pattern)
                {
                    string patternKey = string.Format(LockPatternKey, conversationId);
                    string storedPattern = await Task.Run(() => Preferences.Get(patternKey, string.Empty));
                    return storedPattern == input;
                }
                else if (lockType == LockType.Biometric)
                {
                    // For biometric, authenticate using the service
                    return await BiometricService.AuthenticateAsync("Unlock this chat");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VerifyChatLockAsync error: {ex}");
                return false;
            }
        }
    }
}