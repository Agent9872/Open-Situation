using Lock.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing tags in Supabase.
    /// </summary>
    public class TagRepository
    {
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
        /// Retrieves a list of all tags from Supabase.
        /// </summary>
        /// <returns>A list of <see cref="Tag"/> objects.</returns>
        public async Task<List<Tag>> ListAsync()
        {
            try
            {
                return await SupabaseService.GetAsync<Tag>("Tags", "order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tags from Supabase");
                return new List<Tag>();
            }
        }

        /// <summary>
        /// Retrieves a list of tags associated with a specific project.
        /// </summary>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>A list of <see cref="Tag"/> objects.</returns>
        public async Task<List<Tag>> ListAsync(int projectID)
        {
            try
            {
                // Get all project-tag associations for this project
                var projectTags = await SupabaseService.GetAsync<ProjectsTags>("ProjectsTags", $"ProjectID=eq.{projectID}");

                var tagIds = projectTags.Select(pt => pt.TagID).ToList();

                if (!tagIds.Any())
                    return new List<Tag>();

                if (tagIds.Count == 1)
                {
                    return await SupabaseService.GetAsync<Tag>("Tags", $"ID=eq.{tagIds.First()}");
                }
                else
                {
                    var tagIdList = string.Join(",", tagIds);
                    return await SupabaseService.GetAsync<Tag>("Tags", $"ID=in.({tagIdList})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tags for project {projectID} from Supabase");
                return new List<Tag>();
            }
        }

        /// <summary>
        /// Retrieves a specific tag by its ID.
        /// </summary>
        /// <param name="id">The ID of the tag.</param>
        /// <returns>A <see cref="Tag"/> object if found; otherwise, null.</returns>
        public async Task<Tag?> GetAsync(int id)
        {
            try
            {
                var tags = await SupabaseService.GetAsync<Tag>("Tags", $"ID=eq.{id}&limit=1");
                return tags.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tag with ID {id} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Retrieves a tag by its name.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <returns>A <see cref="Tag"/> object if found; otherwise, null.</returns>
        public async Task<Tag?> GetByNameAsync(string name)
        {
            try
            {
                var tags = await SupabaseService.GetAsync<Tag>("Tags", $"Name=eq.{Uri.EscapeDataString(name)}&limit=1");
                return tags.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tag with name {name} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Saves a tag to Supabase. If the tag ID is 0, a new tag is created; otherwise, the existing tag is updated.
        /// </summary>
        /// <param name="item">The tag to save.</param>
        /// <returns>The ID of the saved tag.</returns>
        public async Task<int> SaveItemAsync(Tag item)
        {
            try
            {
                if (item.ID == 0)
                {
                    // Insert new tag
                    var inserted = await SupabaseService.InsertAndReturnAsync<Tag>("Tags", item);
                    return inserted?.ID ?? 0;
                }
                else
                {
                    // Update existing tag
                    var success = await SupabaseService.UpdateAsync("Tags", $"ID=eq.{item.ID}", item);
                    return success ? item.ID : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving tag with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Saves a tag to Supabase and associates it with a specific project.
        /// </summary>
        /// <param name="item">The tag to save.</param>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>The number of rows affected or the association ID.</returns>
        public async Task<int> SaveItemAsync(Tag item, int projectID)
        {
            try
            {
                // Save the tag first if it's new
                if (item.ID == 0)
                {
                    var saved = await SaveItemAsync(item);
                    if (saved == 0) return 0;
                    item.ID = saved;
                }

                // Check if association already exists
                var existing = await SupabaseService.GetAsync<ProjectsTags>("ProjectsTags",
                    $"ProjectID=eq.{projectID}&TagID=eq.{item.ID}&limit=1");

                if (!existing.Any())
                {
                    var projectTag = new ProjectsTags
                    {
                        ProjectID = projectID,
                        TagID = item.ID
                    };
                    var inserted = await SupabaseService.InsertAndReturnAsync<ProjectsTags>("ProjectsTags", projectTag);
                    return inserted?.ID ?? 0;
                }

                return existing.First().ID; // Return existing ID
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving tag with ID {item.ID} for project {projectID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a tag from Supabase.
        /// </summary>
        /// <param name="item">The tag to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Tag item)
        {
            try
            {
                // First delete all associations for this tag
                var associations = await SupabaseService.GetAsync<ProjectsTags>("ProjectsTags", $"TagID=eq.{item.ID}");

                foreach (var assoc in associations)
                {
                    await SupabaseService.DeleteAsync("ProjectsTags", $"ID=eq.{assoc.ID}");
                }

                // Then delete the tag itself
                var success = await SupabaseService.DeleteAsync("Tags", $"ID=eq.{item.ID}");
                return success ? item.ID : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting tag with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a tag from a specific project in Supabase.
        /// </summary>
        /// <param name="item">The tag to delete.</param>
        /// <param name="projectID">The ID of the project.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(Tag item, int projectID)
        {
            try
            {
                var projectTags = await SupabaseService.GetAsync<ProjectsTags>("ProjectsTags",
                    $"ProjectID=eq.{projectID}&TagID=eq.{item.ID}&limit=1");

                if (projectTags.Any())
                {
                    var success = await SupabaseService.DeleteAsync("ProjectsTags", $"ID=eq.{projectTags.First().ID}");
                    return success ? 1 : 0;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting tag with ID {item.ID} from project {projectID}");
                return 0;
            }
        }

        /// <summary>
        /// Gets all tags for multiple projects (useful for batch operations).
        /// </summary>
        /// <param name="projectIds">List of project IDs.</param>
        /// <returns>A dictionary mapping project ID to list of tags.</returns>
        public async Task<Dictionary<int, List<Tag>>> GetTagsForProjectsAsync(List<int> projectIds)
        {
            try
            {
                if (!projectIds.Any())
                    return new Dictionary<int, List<Tag>>();

                var projectIdList = string.Join(",", projectIds);
                var projectTags = await SupabaseService.GetAsync<ProjectsTags>("ProjectsTags", $"ProjectID=in.({projectIdList})");
                var allTags = await SupabaseService.GetAsync<Tag>("Tags", "");

                var tagDict = allTags.ToDictionary(t => t.ID, t => t);

                var result = new Dictionary<int, List<Tag>>();

                foreach (var pt in projectTags)
                {
                    if (!result.ContainsKey(pt.ProjectID))
                        result[pt.ProjectID] = new List<Tag>();

                    if (tagDict.ContainsKey(pt.TagID))
                        result[pt.ProjectID].Add(tagDict[pt.TagID]);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags for multiple projects");
                return new Dictionary<int, List<Tag>>();
            }
        }
    }
}