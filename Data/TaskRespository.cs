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
    /// Repository class for managing tasks in the database.
    /// </summary>
    public class TaskRepository
    {
        private SQLiteAsyncConnection _database;
        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public TaskRepository(ILogger<TaskRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Task table if it does not exist.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            try
            {
                await _database.CreateTableAsync<ProjectTask>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating Task table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves a list of all tasks from the database.
        /// </summary>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListAsync()
        {
            await Init();
            return await _database.Table<ProjectTask>().ToListAsync();
        }

        /// <summary>
        /// Retrieves a list of tasks associated with a specific project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListAsync(int projectId)
        {
            await Init();
            return await _database.Table<ProjectTask>()
                .Where(t => t.ProjectID == projectId)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves a specific task by its ID.
        /// </summary>
        /// <param name="id">The ID of the task.</param>
        /// <returns>A <see cref="ProjectTask"/> object if found; otherwise, null.</returns>
        public async Task<ProjectTask?> GetAsync(int id)
        {
            await Init();
            return await _database.Table<ProjectTask>()
                .Where(t => t.ID == id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Saves a task to the database. If the task ID is 0, a new task is created; otherwise, the existing task is updated.
        /// </summary>
        /// <param name="item">The task to save.</param>
        /// <returns>The ID of the saved task.</returns>
        public async Task<int> SaveItemAsync(ProjectTask item)
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
        /// Deletes a task from the database.
        /// </summary>
        /// <param name="item">The task to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(ProjectTask item)
        {
            await Init();
            return await _database.DeleteAsync(item);
        }

        /// <summary>
        /// Drops the Task table from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await _database.DropTableAsync<ProjectTask>();
            _hasBeenInitialized = false;
        }
    }
}