using Lock.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lock.Data
{
    /// <summary>
    /// Repository class for managing categories in Supabase.
    /// </summary>
    public class CategoryRepository
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CategoryRepository"/> class.
        /// </summary>
        public CategoryRepository(ILogger<CategoryRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of all categories from Supabase.
        /// </summary>
        public async Task<List<Category>> ListAsync()
        {
            try
            {
                return await SupabaseService.GetAsync<Category>("Categories", "order=ID.asc");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching categories from Supabase");
                return new List<Category>();
            }
        }

        /// <summary>
        /// Retrieves a specific category by its ID.
        /// </summary>
        public async Task<Category?> GetAsync(int id)
        {
            try
            {
                var categories = await SupabaseService.GetAsync<Category>("Categories", $"ID=eq.{id}&limit=1");
                return categories.Count > 0 ? categories[0] : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching category with ID {id} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Retrieves a specific category by its name.
        /// </summary>
        public async Task<Category?> GetByNameAsync(string name)
        {
            try
            {
                var categories = await SupabaseService.GetAsync<Category>("Categories", $"Name=eq.{Uri.EscapeDataString(name)}&limit=1");
                return categories.Count > 0 ? categories[0] : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching category with name {name} from Supabase");
                return null;
            }
        }

        /// <summary>
        /// Saves a category to Supabase (inserts if new, updates if existing).
        /// </summary>
        public async Task<int> SaveItemAsync(Category item)
        {
            try
            {
                if (item.ID == 0)
                {
                    // Insert new category
                    var inserted = await SupabaseService.InsertAndReturnAsync<Category>("Categories", item);
                    return inserted?.ID ?? 0;
                }
                else
                {
                    // Update existing category
                    var success = await SupabaseService.UpdateAsync("Categories", $"ID=eq.{item.ID}", item);
                    return success ? item.ID : 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving category with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a category from Supabase.
        /// </summary>
        public async Task<int> DeleteItemAsync(Category item)
        {
            try
            {
                var success = await SupabaseService.DeleteAsync("Categories", $"ID=eq.{item.ID}");
                return success ? item.ID : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting category with ID {item.ID}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        public async Task<int> DeleteByIdAsync(int id)
        {
            try
            {
                var success = await SupabaseService.DeleteAsync("Categories", $"ID=eq.{id}");
                return success ? id : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting category with ID {id}");
                return 0;
            }
        }

        /// <summary>
        /// Counts the total number of categories.
        /// </summary>
        public async Task<int> CountAsync()
        {
            try
            {
                var categories = await SupabaseService.GetAsync<Category>("Categories", "select=ID");
                return categories.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting categories");
                return 0;
            }
        }

        /// <summary>
        /// Checks if a category exists with the given ID.
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                var categories = await SupabaseService.GetAsync<Category>("Categories", $"ID=eq.{id}&limit=1");
                return categories.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking existence of category with ID {id}");
                return false;
            }
        }

        /// <summary>
        /// Searches categories by name (partial match).
        /// </summary>
        public async Task<List<Category>> SearchAsync(string searchTerm)
        {
            try
            {
                return await SupabaseService.GetAsync<Category>("Categories", $"Name=ilike.*{Uri.EscapeDataString(searchTerm)}*");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching categories with term '{searchTerm}'");
                return new List<Category>();
            }
        }

        /// <summary>
        /// Gets categories with pagination.
        /// </summary>
        public async Task<List<Category>> GetPagedAsync(int page, int pageSize)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                return await SupabaseService.GetAsync<Category>("Categories", $"order=ID.asc&limit={pageSize}&offset={offset}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching paged categories (page {page}, size {pageSize})");
                return new List<Category>();
            }
        }
    }
}