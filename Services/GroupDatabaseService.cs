using Lock.Models;
using Lock.Models.Chat;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Chat.Services
{
    public static class GroupDatabaseService
    {
        private static SQLiteAsyncConnection? _db;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static async Task InitializeAsync()
        {
            if (_db != null) return;

            await _semaphore.WaitAsync();
            try
            {
                if (_db != null) return;

                var dbPath = Path.Combine(
                    FileSystem.AppDataDirectory,
                    "lock_groups.db3");

                _db = new SQLiteAsyncConnection(dbPath,
                    SQLiteOpenFlags.ReadWrite |
                    SQLiteOpenFlags.Create |
                    SQLiteOpenFlags.SharedCache);

                await _db.CreateTableAsync<Group>();
                await _db.CreateTableAsync<GroupMember>();
                await _db.CreateTableAsync<GroupMessage>();
                await _db.CreateTableAsync<GroupInvite>();
                await _db.CreateTableAsync<GroupJoinRequest>();
                await _db.CreateTableAsync<GroupEvent>();
                await _db.CreateTableAsync<GroupPinnedMessage>();

                Debug.WriteLine("GroupDatabaseService initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GroupDatabaseService init error: {ex}");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static SQLiteAsyncConnection GetConnection()
        {
            if (_db == null)
                throw new InvalidOperationException(
                    "GroupDatabaseService not initialized. Call InitializeAsync() first.");
            return _db;
        }
    }
}