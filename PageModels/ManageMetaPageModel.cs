using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lock.Models;
using Lock.Services;

namespace Lock.PageModels
{
    public partial class ManageMetaPageModel : ObservableObject
    {
        // Remove these old SQLite-based repositories:
        // private readonly CategoryRepository _categoryRepository;
        // private readonly TagRepository _tagRepository;
        // private readonly SeedDataService _seedDataService;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = [];

        [ObservableProperty]
        private ObservableCollection<Tag> _tags = [];

        // Update constructor
        public ManageMetaPageModel()
        {
            // Initialize empty collections
            Categories = new ObservableCollection<Category>();
            Tags = new ObservableCollection<Tag>();
        }

        private async Task LoadData()
        {
            try
            {
                // TODO: Load categories and tags from Supabase if needed
                // For now, just initialize empty collections
                Categories = new ObservableCollection<Category>();
                Tags = new ObservableCollection<Tag>();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadData error: {ex}");
            }
        }

        [RelayCommand]
        private Task Appearing()
            => LoadData();

        [RelayCommand]
        private async Task SaveCategories()
        {
            // TODO: Save categories to Supabase
            await AppShell.DisplayToastAsync("Categories saved");
        }

        [RelayCommand]
        private async Task DeleteCategory(Category category)
        {
            Categories.Remove(category);
            // TODO: Delete category from Supabase
            await AppShell.DisplayToastAsync("Category deleted");
        }

        [RelayCommand]
        private async Task AddCategory()
        {
            var category = new Category();
            Categories.Add(category);
            // TODO: Save category to Supabase
            await AppShell.DisplayToastAsync("Category added");
        }

        [RelayCommand]
        private async Task SaveTags()
        {
            // TODO: Save tags to Supabase
            await AppShell.DisplayToastAsync("Tags saved");
        }

        [RelayCommand]
        private async Task DeleteTag(Tag tag)
        {
            Tags.Remove(tag);
            // TODO: Delete tag from Supabase
            await AppShell.DisplayToastAsync("Tag deleted");
        }

        [RelayCommand]
        private async Task AddTag()
        {
            var tag = new Tag();
            Tags.Add(tag);
            // TODO: Save tag to Supabase
            await AppShell.DisplayToastAsync("Tag added");
        }

        [RelayCommand]
        private async Task Reset()
        {
            // Remove seed data logic since it's no longer needed with Supabase
            // Just navigate back to main page
            await Shell.Current.GoToAsync("//main");
        }
    }
}