using Foundation;
using Lock.Services;
using System;
using System.Threading.Tasks;
using UIKit;

[assembly: Dependency(typeof(Lock.Platforms.iOS.FolderPickerService))]

namespace Lock.Platforms.iOS
{
    public class FolderPickerService : IFolderPicker
    {
        public async Task<string?> PickFolder()
        {
            try
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var appFolder = System.IO.Path.Combine(documents, "LockChat");

                if (!System.IO.Directory.Exists(appFolder))
                {
                    System.IO.Directory.CreateDirectory(appFolder);
                }

                bool useDefault = await Application.Current.MainPage.DisplayAlert(
                    "Folder Selection",
                    "iOS doesn't allow selecting folders directly. Save to app's Documents folder?\n\n" +
                    $"Location: {appFolder}",
                    "Yes, use Documents",
                    "Cancel");

                return useDefault ? appFolder : GetDefaultFolder();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS FolderPicker error: {ex}");
                return GetDefaultFolder();
            }
        }

        private string GetDefaultFolder()
        {
            try
            {
                var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var fallbackFolder = System.IO.Path.Combine(pictures, "LockChat");

                if (!System.IO.Directory.Exists(fallbackFolder))
                {
                    System.IO.Directory.CreateDirectory(fallbackFolder);
                }

                return fallbackFolder;
            }
            catch
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "LockChat");
            }
        }

        public async Task<bool> CanWriteToFolder(string folderIdentifier)
        {
            try
            {
                if (string.IsNullOrEmpty(folderIdentifier))
                    return false;

                string testFile = System.IO.Path.Combine(folderIdentifier, "test.txt");
                await System.IO.File.WriteAllTextAsync(testFile, "test");

                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CanWriteToFolder error on iOS: {ex}");
                return false;
            }
        }
    }
}