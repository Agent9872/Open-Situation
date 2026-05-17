using Lock.Models;
using SQLite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing categories in the database.
    /// </summary>
    public class CategoryRepository
    {
        private SQLiteAsyncConnection _database;
        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CategoryRepository"/> class.
        /// </summary>
        public CategoryRepository(ILogger<CategoryRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Category table if it does not exist.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            try
            {
                await _database.CreateTableAsync<Category>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating Category table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves a list of all categories from the database.
        /// </summary>
        public async Task<List<Category>> ListAsync()
        {
            await Init();
            return await _database.Table<Category>().ToListAsync();
        }

        /// <summary>
        /// Retrieves a specific category by its ID.
        /// </summary>
        public async Task<Category?> GetAsync(int id)
        {
            await Init();
            return await _database.Table<Category>()
                .Where(c => c.ID == id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Saves a category to the database.
        /// </summary>
        public async Task<int> SaveItemAsync(Category item)
        {
            await Init();

            if (item.ID == 0)
            {
                return await _database.InsertAsync(item);
            }
            else
            {
                await _database.UpdateAsync(item);
                return item.ID;
            }
        }

        /// <summary>
        /// Deletes a category from the database.
        /// </summary>
        public async Task<int> DeleteItemAsync(Category item)
        {
            await Init();
            return await _database.DeleteAsync(item);
        }

        /// <summary>
        /// Drops the Category table from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await _database.DropTableAsync<Category>();
            _hasBeenInitialized = false;
        }
    }
}