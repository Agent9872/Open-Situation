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
    /// Repository class for managing projects in the database.
    /// </summary>
    public class ProjectRepository
    {
        private SQLiteAsyncConnection _database;
        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;
        private readonly TaskRepository _taskRepository;
        private readonly TagRepository _tagRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectRepository"/> class.
        /// </summary>
        public ProjectRepository(TaskRepository taskRepository, TagRepository tagRepository, ILogger<ProjectRepository> logger)
        {
            _taskRepository = taskRepository;
            _tagRepository = tagRepository;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Project table if it does not exist.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            try
            {
                await _database.CreateTableAsync<Project>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating Project table");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves a list of all projects from the database.
        /// </summary>
        public async Task<List<Project>> ListAsync()
        {
            await Init();

            var projects = await _database.Table<Project>().ToListAsync();

            foreach (var project in projects)
            {
                project.Tags = await _tagRepository.ListAsync(project.ID);
                project.Tasks = await _taskRepository.ListAsync(project.ID);
            }

            return projects;
        }

        /// <summary>
        /// Retrieves a specific project by its ID.
        /// </summary>
        public async Task<Project?> GetAsync(int id)
        {
            await Init();

            var project = await _database.Table<Project>()
                .Where(p => p.ID == id)
                .FirstOrDefaultAsync();

            if (project != null)
            {
                project.Tags = await _tagRepository.ListAsync(project.ID);
                project.Tasks = await _taskRepository.ListAsync(project.ID);
            }

            return project;
        }

        /// <summary>
        /// Saves a project to the database.
        /// </summary>
        public async Task<int> SaveItemAsync(Project item)
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
        /// Deletes a project from the database.
        /// </summary>
        public async Task<int> DeleteItemAsync(Project item)
        {
            await Init();
            return await _database.DeleteAsync(item);
        }

        /// <summary>
        /// Drops the Project table from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await _database.DropTableAsync<Project>();
            await _taskRepository.DropTableAsync();
            await _tagRepository.DropTableAsync();
            _hasBeenInitialized = false;
        }
    }
}