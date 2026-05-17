using Lock.Models.Chat;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Chat.Services
{
    public static class ChatEncryptionService
    {
        private const string EncryptionKeyPrefix = "chat_key_";

        // Generate a new AES key for a conversation
        public static byte[] GenerateKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                return aes.Key;
            }
        }

        // Generate a random IV for each encryption operation
        public static byte[] GenerateIV()
        {
            using (Aes aes = Aes.Create())
            {
                aes.GenerateIV();
                return aes.IV;
            }
        }

        // Store encryption key securely for a conversation
        public static async Task StoreConversationKeyAsync(string conversationId, byte[] key)
        {
            string keyName = $"{EncryptionKeyPrefix}{conversationId}";
            string keyString = Convert.ToBase64String(key);
            await SecureStorage.SetAsync(keyName, keyString);
        }

        // Retrieve encryption key for a conversation
        public static async Task<byte[]> GetConversationKeyAsync(string conversationId)
        {
            string keyName = $"{EncryptionKeyPrefix}{conversationId}";
            string keyString = await SecureStorage.GetAsync(keyName);

            if (string.IsNullOrEmpty(keyString))
                return null;

            return Convert.FromBase64String(keyString);
        }

        // Get or create a key for a conversation
        public static async Task<byte[]> GetOrCreateConversationKeyAsync(string conversationId)
        {
            var existingKey = await GetConversationKeyAsync(conversationId);
            if (existingKey != null)
                return existingKey;

            var newKey = GenerateKey();
            await StoreConversationKeyAsync(conversationId, newKey);
            return newKey;
        }

        // Encrypt a string message
        public static async Task<(string encryptedText, string iv)> EncryptAsync(string plainText, string conversationId)
        {
            if (string.IsNullOrEmpty(plainText))
                return (plainText, null);

            byte[] key = await GetOrCreateConversationKeyAsync(conversationId);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();

                ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                string encryptedText = Convert.ToBase64String(cipherBytes);
                string iv = Convert.ToBase64String(aes.IV);

                return (encryptedText, iv);
            }
        }

        // Decrypt a string message
        public static async Task<string> DecryptAsync(string encryptedText, string iv, string conversationId)
        {
            if (string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(iv))
                return encryptedText;

            byte[] key = await GetConversationKeyAsync(conversationId);
            if (key == null)
                return "[Encrypted message - key not available]";

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = Convert.FromBase64String(iv);

                    ICryptoTransform decryptor = aes.CreateDecryptor();
                    byte[] cipherBytes = Convert.FromBase64String(encryptedText);
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
            catch
            {
                return "[Encrypted message - cannot decrypt]";
            }
        }

        // Encrypt a ChatMessage object
        public static async Task EncryptMessageAsync(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Content))
                return;

            var (encrypted, iv) = await EncryptAsync(message.Content, message.ConversationId);
            message.Content = encrypted;
            message.EncryptionIV = iv;
            message.IsEncrypted = true;
        }

        // Decrypt a ChatMessage object
        public static async Task DecryptMessageAsync(ChatMessage message)
        {
            if (message == null || !message.IsEncrypted || string.IsNullOrEmpty(message.EncryptionIV))
            {
                Debug.WriteLine($"Skipping decryption: IsEncrypted={message?.IsEncrypted}, HasIV={!string.IsNullOrEmpty(message?.EncryptionIV)}");
                return;
            }

            byte[] key = await GetConversationKeyAsync(message.ConversationId);
            if (key == null)
            {
                Debug.WriteLine($"No key found for conversation {message.ConversationId}");
                message.Content = "[Encrypted message - key not available]";
                return;
            }

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = Convert.FromBase64String(message.EncryptionIV);

                    ICryptoTransform decryptor = aes.CreateDecryptor();
                    byte[] cipherBytes = Convert.FromBase64String(message.Content);
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    message.Content = Encoding.UTF8.GetString(plainBytes);
                    message.IsEncrypted = false; // Mark as decrypted

                    Debug.WriteLine($"Successfully decrypted message {message.Id}: {message.Content}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decryption failed for message {message.Id}: {ex.Message}");
                message.Content = "[Encrypted message - cannot decrypt]";
            }
        }

        // Batch decrypt multiple messages
        public static async Task DecryptMessagesAsync(IEnumerable<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                await DecryptMessageAsync(message);
            }
        }
    }
}