using Lock.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing tasks in Supabase.
    /// </summary>
    public class TaskRepository
    {
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
        /// Retrieves a list of all tasks from Supabase.
        /// </summary>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListAsync()
        {
            try
            {
                return await SupabaseService.GetAsync<ProjectTask>("ProjectTasks", "order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks from Supabase");
                return new List<ProjectTask>();
            }
        }

        /// <summary>
        /// Retrieves a list of tasks associated with a specific project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListAsync(int projectId)
        {
            try
            {
                return await SupabaseService.GetAsync<ProjectTask>("ProjectTasks",
                    $"ProjectID=eq.{projectId}&order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tasks for project {projectId} from Supabase");
                return new List<ProjectTask>();
            }
        }

        /// <summary>
        /// Retrieves a list of incomplete tasks for a specific project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListIncompleteAsync(int projectId)
        {
            try
            {
                return await SupabaseService.GetAsync<ProjectTask>("ProjectTasks",
                    $"ProjectID=eq.{projectId}&IsCompleted=eq.false&order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching incomplete tasks for project {projectId} from Supabase");
                return new List<ProjectTask>();
            }
        }

        /// <summary>
        /// Retrieves a list of completed tasks for a specific project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>A list of <see cref="ProjectTask"/> objects.</returns>
        public async Task<List<ProjectTask>> ListCompletedAsync(int projectId)
        {
            try
            {
                return await SupabaseService.GetAsync<ProjectTask>("ProjectTasks",
                    $"ProjectID=eq.{projectId}&IsCompleted=eq.true&order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching completed tasks for project {projectId} from Supabase");
                return new List<ProjectTask>();
            }
        }

        /// <summary>
        /// Retrieves a specific task by its ID.
        /// </summary>
        /// <param name="id">The ID of the task.</param>
        /// <returns>A <see cref="ProjectTask"/> object if found; otherwise, null.</returns>
        public async Task<ProjectTask?> GetAsync(int id)
        {
            try
            {
                var tasks = await SupabaseService.GetAsync<ProjectTask>("ProjectTasks", $"ID=eq.{id}&limit=1");
                return tasks.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching task with ID {id} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Saves a task to Supabase. If the task ID is 0, a new task is created; otherwise, the existing task is updated.
        /// </summary>
        /// <param name="item">The task to save.</param>
        /// <returns>The ID of the saved task.</returns>
        public async Task<int> SaveItemAsync(ProjectTask item)
        {
            try
            {
                if (item.ID == 0)
                {
                    // Insert new task
                    var inserted = await SupabaseService.InsertAndReturnAsync<ProjectTask>("ProjectTasks", item);
                    return inserted?.ID ?? 0;
                }
                else
                {
                    // Update existing task
                    var success = await SupabaseService.UpdateAsync("ProjectTasks", $"ID=eq.{item.ID}", item);
                    return success ? item.ID : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving task with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Toggles the completion status of a task.
        /// </summary>
        /// <param name="taskId">The ID of the task.</param>
        /// <param name="isCompleted">The new completion status.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> ToggleTaskCompletionAsync(int taskId, bool isCompleted)
        {
            try
            {
                return await SupabaseService.UpdateAsync("ProjectTasks", $"ID=eq.{taskId}",
                    new { IsCompleted = isCompleted, CompletedAt = isCompleted ? DateTime.UtcNow : (DateTime?)null });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling completion for task {taskId}");
                return false;
            }
        }

        /// <summary>
        /// Updates the priority of a task.
        /// </summary>
        /// <param name="taskId">The ID of the task.</param>
        /// <param name="priority">The new priority level.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> UpdateTaskPriorityAsync(int taskId, int priority)
        {
            try
            {
                return await SupabaseService.UpdateAsync("ProjectTasks", $"ID=eq.{taskId}", new { Priority = priority });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating priority for task {taskId}");
                return false;
            }
        }

        /// <summary>
        /// Updates the order/position of a task.
        /// </summary>
        /// <param name="taskId">The ID of the task.</param>
        /// <param name="order">The new order position.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> UpdateTaskOrderAsync(int taskId, int order)
        {
            try
            {
                return await SupabaseService.UpdateAsync("ProjectTasks", $"ID=eq.{taskId}", new { TaskOrder = order });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating order for task {taskId}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a task from Supabase.
        /// </summary>
        /// <param name="item">The task to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteItemAsync(ProjectTask item)
        {
            try
            {
                var success = await SupabaseService.DeleteAsync("ProjectTasks", $"ID=eq.{item.ID}");
                return success ? item.ID : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting task with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a task by its ID.
        /// </summary>
        /// <param name="taskId">The ID of the task to delete.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> DeleteByIdAsync(int taskId)
        {
            try
            {
                return await SupabaseService.DeleteAsync("ProjectTasks", $"ID=eq.{taskId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting task with ID {taskId}");
                return false;
            }
        }

        /// <summary>
        /// Deletes all tasks for a specific project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> DeleteByProjectIdAsync(int projectId)
        {
            try
            {
                return await SupabaseService.DeleteAsync("ProjectTasks", $"ProjectID=eq.{projectId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting tasks for project {projectId}");
                return false;
            }
        }

        /// <summary>
        /// Gets the count of tasks for a project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>The number of tasks.</returns>
        public async Task<int> GetTaskCountAsync(int projectId)
        {
            try
            {
                var tasks = await SupabaseService.GetAsync<ProjectTask>("ProjectTasks",
                    $"ProjectID=eq.{projectId}&select=ID");
                return tasks.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting tasks for project {projectId}");
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of completed tasks for a project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>The number of completed tasks.</returns>
        public async Task<int> GetCompletedTaskCountAsync(int projectId)
        {
            try
            {
                var tasks = await SupabaseService.GetAsync<ProjectTask>("ProjectTasks",
                    $"ProjectID=eq.{projectId}&IsCompleted=eq.true&select=ID");
                return tasks.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting completed tasks for project {projectId}");
                return 0;
            }
        }

        /// <summary>
        /// Gets the completion percentage for a project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <returns>The completion percentage (0-100).</returns>
        public async Task<int> GetCompletionPercentageAsync(int projectId)
        {
            try
            {
                var total = await GetTaskCountAsync(projectId);
                if (total == 0) return 0;

                var completed = await GetCompletedTaskCountAsync(projectId);
                return (int)((double)completed / total * 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating completion percentage for project {projectId}");
                return 0;
            }
        }

        /// <summary>
        /// Searches tasks by title or description.
        /// </summary>
        /// <param name="searchTerm">The search term.</param>
        /// <param name="projectId">Optional project ID to filter by.</param>
        /// <returns>A list of matching tasks.</returns>
        public async Task<List<ProjectTask>> SearchAsync(string searchTerm, int? projectId = null)
        {
            try
            {
                var query = projectId.HasValue
                    ? $"ProjectID=eq.{projectId.Value}&or=(Title.ilike.*{Uri.EscapeDataString(searchTerm)}*,Description.ilike.*{Uri.EscapeDataString(searchTerm)}*)"
                    : $"or=(Title.ilike.*{Uri.EscapeDataString(searchTerm)}*,Description.ilike.*{Uri.EscapeDataString(searchTerm)}*)";

                return await SupabaseService.GetAsync<ProjectTask>("ProjectTasks", query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tasks with term '{searchTerm}'");
                return new List<ProjectTask>();
            }
        }
    }
}