using CommunityToolkit.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lock.Services
{
    public class FolderPicker : IFolderPicker
    {
        public Task<string?> PickFolder()
        {
            try
            {
                // Default folder for Windows/Mac
                string defaultFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "LockChat");

                if (!Directory.Exists(defaultFolder))
                {
                    Directory.CreateDirectory(defaultFolder);
                }

                return Task.FromResult<string?>(defaultFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Folder picker error: {ex}");
                return Task.FromResult<string?>(null);
            }
        }

        public Task<bool> CanWriteToFolder(string folderIdentifier)
        {
            try
            {
                if (string.IsNullOrEmpty(folderIdentifier))
                    return Task.FromResult(false);

                string testFile = Path.Combine(folderIdentifier, "test.txt");
                File.WriteAllText(testFile, "test");

                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}