using Lock.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing projects in Supabase.
    /// </summary>
    public class ProjectRepository
    {
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
        /// Retrieves a list of all projects from Supabase.
        /// </summary>
        public async Task<List<Project>> ListAsync()
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects", "order=ID.asc");

                foreach (var project in projects)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return projects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching projects from Supabase");
                return new List<Project>();
            }
        }

        /// <summary>
        /// Retrieves a list of projects by user phone number.
        /// </summary>
        public async Task<List<Project>> ListByUserAsync(string userPhone)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=ID.asc");

                foreach (var project in projects)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return projects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching projects for user {userPhone} from Supabase");
                return new List<Project>();
            }
        }

        /// <summary>
        /// Retrieves a specific project by its ID.
        /// </summary>
        public async Task<Project?> GetAsync(int id)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects", $"ID=eq.{id}&limit=1");
                var project = projects.FirstOrDefault();

                if (project != null)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching project with ID {id} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Retrieves a project by its unique project ID (string).
        /// </summary>
        public async Task<Project?> GetByProjectIdAsync(string projectId)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects",
                    $"ProjectId=eq.{Uri.EscapeDataString(projectId)}&limit=1");
                var project = projects.FirstOrDefault();

                if (project != null)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching project with ProjectId {projectId} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Saves a project to Supabase.
        /// </summary>
        public async Task<int> SaveItemAsync(Project item)
        {
            try
            {
                if (item.ID == 0)
                {
                    // Insert new project
                    var inserted = await SupabaseService.InsertAndReturnAsync<Project>("Projects", item);
                    return inserted?.ID ?? 0;
                }
                else
                {
                    // Update existing project
                    var success = await SupabaseService.UpdateAsync("Projects", $"ID=eq.{item.ID}", item);
                    return success ? item.ID : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving project with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Saves a project with its associated tags and tasks.
        /// </summary>
        public async Task<int> SaveProjectWithDetailsAsync(Project item)
        {
            try
            {
                // Save the project first
                var projectId = await SaveItemAsync(item);
                if (projectId == 0) return 0;

                item.ID = projectId;

                // Save associated tags
                if (item.Tags != null && item.Tags.Any())
                {
                    foreach (var tag in item.Tags)
                    {
                        await _tagRepository.SaveItemAsync(tag, item.ID);
                    }
                }

                // Save associated tasks
                if (item.Tasks != null && item.Tasks.Any())
                {
                    foreach (var task in item.Tasks)
                    {
                        task.ProjectID = item.ID;
                        await _taskRepository.SaveItemAsync(task);
                    }
                }

                return projectId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving project with details for ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a project from Supabase.
        /// </summary>
        public async Task<int> DeleteItemAsync(Project item)
        {
            try
            {
                // First delete all associated tags
                if (item.Tags != null && item.Tags.Any())
                {
                    foreach (var tag in item.Tags)
                    {
                        await _tagRepository.DeleteItemAsync(tag, item.ID);
                    }
                }

                // Delete all associated tasks
                if (item.Tasks != null && item.Tasks.Any())
                {
                    foreach (var task in item.Tasks)
                    {
                        await _taskRepository.DeleteItemAsync(task);
                    }
                }
                else
                {
                    // If tasks weren't loaded, delete them by project ID
                    var tasks = await _taskRepository.ListAsync(item.ID);
                    foreach (var task in tasks)
                    {
                        await _taskRepository.DeleteItemAsync(task);
                    }
                }

                // Then delete the project itself
                var success = await SupabaseService.DeleteAsync("Projects", $"ID=eq.{item.ID}");
                return success ? item.ID : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting project with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a project by its ID.
        /// </summary>
        public async Task<int> DeleteByIdAsync(int id)
        {
            try
            {
                // First delete all associated tags
                var tags = await _tagRepository.ListAsync(id);
                foreach (var tag in tags)
                {
                    await _tagRepository.DeleteItemAsync(tag, id);
                }

                // Delete all associated tasks
                var tasks = await _taskRepository.ListAsync(id);
                foreach (var task in tasks)
                {
                    await _taskRepository.DeleteItemAsync(task);
                }

                // Then delete the project itself
                var success = await SupabaseService.DeleteAsync("Projects", $"ID=eq.{id}");
                return success ? id : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting project with ID {id}");
                return 0;
            }
        }

        /// <summary>
        /// Updates just the project status (without loading all relations).
        /// </summary>
        public async Task<bool> UpdateProjectStatusAsync(int projectId, string status)
        {
            try
            {
                return await SupabaseService.UpdateAsync("Projects", $"ID=eq.{projectId}", new { Status = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for project {projectId}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a project exists with the given ID.
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects", $"ID=eq.{id}&select=ID&limit=1");
                return projects.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking existence of project with ID {id}");
                return false;
            }
        }

        /// <summary>
        /// Counts total projects for a user.
        /// </summary>
        public async Task<int> CountByUserAsync(string userPhone)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&select=ID");
                return projects.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting projects for user {userPhone}");
                return 0;
            }
        }

        /// <summary>
        /// Searches projects by title or description.
        /// </summary>
        public async Task<List<Project>> SearchAsync(string searchTerm, string userPhone)
        {
            try
            {
                var projects = await SupabaseService.GetAsync<Project>("Projects",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&or=(Title.ilike.*{Uri.EscapeDataString(searchTerm)}*,Description.ilike.*{Uri.EscapeDataString(searchTerm)}*)");

                foreach (var project in projects)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return projects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching projects with term '{searchTerm}'");
                return new List<Project>();
            }
        }

        /// <summary>
        /// Gets projects with pagination.
        /// </summary>
        public async Task<List<Project>> GetPagedAsync(string userPhone, int page, int pageSize)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                var projects = await SupabaseService.GetAsync<Project>("Projects",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=CreatedAt.desc&limit={pageSize}&offset={offset}");

                foreach (var project in projects)
                {
                    project.Tags = await _tagRepository.ListAsync(project.ID);
                    project.Tasks = await _taskRepository.ListAsync(project.ID);
                }

                return projects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching paged projects for user {userPhone}");
                return new List<Project>();
            }
        }
    }
}