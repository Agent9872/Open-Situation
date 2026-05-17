using Lock.Models;
using SQLite;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing tags in the database.
    /// </summary>
    public class TagRepository
    {
        private SQLiteAsyncConnection _database;
        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public TagRepository(ILogger<TagRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database connection and creates the Tag and ProjectsTags tables if they do not exist.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            try
            {
                await _database.CreateTableAsync<Tag>();
                await _database.CreateTableAsync<ProjectsTags>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating tables");
                throw;
            }

            _hasBeenInitialized = true;
        }

        /// <summary>
        /// Retrieves a list of all tags from the database.
        /// </summary>
        /// <returns>A list of <see cref="Tag"/> objects.</returns>
        public async Task<List<Tag>> ListAsync()
        {
            await Init();
            return await _database.Table<Tag>().ToListAsync();
        }

        /// <summary>
        /// Retrieves a list of tags associated with a specific project.
        /// </summary>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>A list of <see cref="Tag"/> objects.</returns>
        public async Task<List<Tag>> ListAsync(int projectID)
        {
            await Init();

            // Get all tag IDs for this project
            var projectTags = await _database.Table<ProjectsTags>()
                .Where(pt => pt.ProjectID == projectID)
                .ToListAsync();

            var tagIds = projectTags.Select(pt => pt.TagID).ToList();

            if (!tagIds.Any())
                return new List<Tag>();

            // Get all tags with those IDs
            var tags = await _database.Table<Tag>()
                .Where(t => tagIds.Contains(t.ID))
                .ToListAsync();

            return tags;
        }

        /// <summary>
        /// Retrieves a specific tag by its ID.
        /// </summary>
        /// <param name="id">The ID of the tag.</param>
        /// <returns>A <see cref="Tag"/> object if found; otherwise, null.</returns>
        public async Task<Tag?> GetAsync(int id)
        {
            await Init();
            return await _database.Table<Tag>()
                .Where(t => t.ID == id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Saves a tag to the database. If the tag ID is 0, a new tag is created; otherwise, the existing tag is updated.
        /// </summary>
        /// <param name="item">The tag to save.</param>
        /// <returns>The ID of the saved tag.</returns>
        public async Task<int> SaveItemAsync(Tag item)
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
        /// Saves a tag to the database and associates it with a specific project.
        /// </summary>
        /// <param name="item">The tag to save.</param>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> SaveItemAsync(Tag item, int projectID)
        {
            await Init();

            // Save the tag first if it's new
            if (item.ID == 0)
            {
                await SaveItemAsync(item);
            }

            // Check if association already exists
            var existing = await _database.Table<ProjectsTags>()
                .Where(pt => pt.ProjectID == projectID && pt.TagID == item.ID)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                var projectTag = new ProjectsTags
                {
                    ProjectID = projectID,
                    TagID = item.ID
                };
                return await _database.InsertAsync(projectTag);
            }

            return existing.ID; // Return existing ID
        }

        /// <summary>
        /// Deletes a tag from the database.
        /// </summary>
        /// <param name="item">The tag to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Tag item)
        {
            await Init();

            // First delete all associations for this tag
            var associations = await _database.Table<ProjectsTags>()
                .Where(pt => pt.TagID == item.ID)
                .ToListAsync();

            foreach (var assoc in associations)
            {
                await _database.DeleteAsync(assoc);
            }

            // Then delete the tag itself
            return await _database.DeleteAsync(item);
        }

        /// <summary>
        /// Deletes a tag from a specific project in the database.
        /// </summary>
        /// <param name="item">The tag to delete.</param>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Tag item, int projectID)
        {
            await Init();

            var projectTag = await _database.Table<ProjectsTags>()
                .Where(pt => pt.ProjectID == projectID && pt.TagID == item.ID)
                .FirstOrDefaultAsync();

            if (projectTag != null)
            {
                return await _database.DeleteAsync(projectTag);
            }

            return 0;
        }

        /// <summary>
        /// Drops the Tag and ProjectsTags tables from the database.
        /// </summary>
        public async Task DropTableAsync()
        {
            await Init();
            await _database.DropTableAsync<ProjectsTags>();
            await _database.DropTableAsync<Tag>();
            _hasBeenInitialized = false;
        }
    }
}