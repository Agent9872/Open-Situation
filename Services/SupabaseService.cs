// Lock/Services/SupabaseService.cs
using Lock.Models;
using Supabase;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Services
{
    public class SupabaseService
    {
        private static Supabase.Client? _client;
        private static bool _initialized = false;

        public static async Task<Supabase.Client> GetClientAsync()
        {
            if (_initialized && _client != null)
                return _client;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            _client = new Supabase.Client(
                SupabaseConfig.Url,
                SupabaseConfig.AnonKey,
                options);

            await _client.InitializeAsync();
            _initialized = true;

            Debug.WriteLine("Supabase client initialized");
            return _client;
        }

        // ── USER ──────────────────────────────────────────────

        public static async Task<Lock.Models.User?> GetUserByPhoneAsync(string phone)
        {
            try
            {
                var client = await GetClientAsync();
                var result = await client
                    .From<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .Single();
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserByPhoneAsync error: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> UpsertUserAsync(Lock.Models.User user)
        {
            try
            {
                var client = await GetClientAsync();
                await client.From<Lock.Models.User>().Upsert(user);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpsertUserAsync error: {ex.Message}");
                return false;
            }
        }

        // ── COIN BALANCE ──────────────────────────────────────

        public static async Task<int> GetCoinBalanceAsync(string phone)
        {
            try
            {
                var user = await GetUserByPhoneAsync(phone);
                return user?.CoinBalance ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetCoinBalanceAsync error: {ex.Message}");
                return 0;
            }
        }

        public static async Task<bool> UpdateCoinBalanceAsync(string phone, int newBalance)
        {
            try
            {
                var client = await GetClientAsync();
                await client
                    .From<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .Set(u => u.CoinBalance, newBalance)
                    .Update();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCoinBalanceAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> AddCoinTransactionAsync(
            string phone, int amount, string type,
            string reference = "", string description = "")
        {
            try
            {
                var client = await GetClientAsync();
                var tx = new CoinTransaction
                {
                    UserPhone = phone,
                    Amount = amount,
                    Type = type,
                    Reference = reference,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                };
                await client.From<CoinTransaction>().Insert(tx);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddCoinTransactionAsync error: {ex.Message}");
                return false;
            }
        }

        // Called after Paystack confirms payment
        public static async Task<bool> CreditCoinsAsync(
            string phone, int coins, string paystackReference)
        {
            try
            {
                int current = await GetCoinBalanceAsync(phone);
                int newBalance = current + coins;

                bool updated = await UpdateCoinBalanceAsync(phone, newBalance);
                if (!updated) return false;

                await AddCoinTransactionAsync(
                    phone, coins, "deposit",
                    paystackReference,
                    $"Deposited {coins} coins");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreditCoinsAsync error: {ex.Message}");
                return false;
            }
        }

        // Called when a gift is sent
        public static async Task<bool> DeductCoinsForGiftAsync(
            string senderPhone, string recipientPhone,
            string giftName, int cost)
        {
            try
            {
                int balance = await GetCoinBalanceAsync(senderPhone);
                if (balance < cost) return false;

                await UpdateCoinBalanceAsync(senderPhone, balance - cost);

                await AddCoinTransactionAsync(
                    senderPhone, -cost, "gift_sent",
                    description: $"Sent {giftName} gift");

                await AddCoinTransactionAsync(
                    recipientPhone, cost, "gift_received",
                    description: $"Received {giftName} gift");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeductCoinsForGiftAsync error: {ex.Message}");
                return false;
            }
        }
    }
}