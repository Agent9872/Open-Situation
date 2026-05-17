using SQLite;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Lock.Chat.Services
{
    public static class DatabaseMigration
    {
        private const string CurrentDbVersionKey = "database_version";
        private const int CurrentDbVersion = 2; // Increment this when schema changes

        public static async Task EnsureDatabaseSchemaAsync(SQLiteAsyncConnection db)
        {
            try
            {
                // Check current database version from preferences
                int currentVersion = Preferences.Get(CurrentDbVersionKey, 1);

                if (currentVersion < CurrentDbVersion)
                {
                    Debug.WriteLine($"Migrating database from version {currentVersion} to {CurrentDbVersion}");

                    // Apply migrations based on version
                    if (currentVersion < 2)
                    {
                        await MigrateToVersion2(db);
                    }

                    // Add more migrations here as needed
                    // if (currentVersion < 3) { await MigrateToVersion3(db); }

                    // Update version in preferences
                    Preferences.Set(CurrentDbVersionKey, CurrentDbVersion);
                    Debug.WriteLine($"Database migration to version {CurrentDbVersion} completed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database migration error: {ex.Message}");
                // If migration fails, we'll just continue with existing schema
                // New features won't be available but app won't crash
            }
        }

        private static async Task MigrateToVersion2(SQLiteAsyncConnection db)
        {
            try
            {
                // Check if IsEncrypted column exists
                bool hasIsEncrypted = await ColumnExists(db, "ChatMessages", "IsEncrypted");
                if (!hasIsEncrypted)
                {
                    await db.ExecuteAsync("ALTER TABLE ChatMessages ADD COLUMN IsEncrypted INTEGER DEFAULT 0");
                    Debug.WriteLine("Added IsEncrypted column to ChatMessages");
                }

                // Check if EncryptionIV column exists
                bool hasEncryptionIV = await ColumnExists(db, "ChatMessages", "EncryptionIV");
                if (!hasEncryptionIV)
                {
                    await db.ExecuteAsync("ALTER TABLE ChatMessages ADD COLUMN EncryptionIV TEXT");
                    Debug.WriteLine("Added EncryptionIV column to ChatMessages");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error migrating to version 2: {ex.Message}");
                // If columns already exist, SQLite will throw an error - we can ignore it
            }
        }

        private static async Task<bool> ColumnExists(SQLiteAsyncConnection db, string tableName, string columnName)
        {
            try
            {
                var tableInfo = await db.GetTableInfoAsync(tableName);
                foreach (var column in tableInfo)
                {
                    if (column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}