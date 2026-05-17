using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Lock.Services;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;
using AndroidX.DocumentFile.Provider;
using AndroidUri = Android.Net.Uri;

[assembly: Dependency(typeof(Lock.Platforms.Android.FolderPickerService))]

namespace Lock.Platforms.Android
{
    public class FolderPickerService : IFolderPicker
    {
        private TaskCompletionSource<string?> _tcs;
        private Activity? _activity;
        private const int PickFolderRequestCode = 1001;

        public async Task<string?> PickFolder()
        {
            try
            {
                _activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No current activity found");
                _tcs = new TaskCompletionSource<string?>();

                var intent = new Intent(Intent.ActionOpenDocumentTree);
                intent.AddCategory(Intent.CategoryDefault);
                intent.PutExtra("android.content.extra.SHOW_ADVANCED", true);

                _activity.StartActivityForResult(
                    Intent.CreateChooser(intent, "Select folder to save images"),
                    PickFolderRequestCode
                );

                return await _tcs.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Folder picker error: {ex}");
                return GetDefaultFolder();
            }
        }

        public void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            try
            {
                if (requestCode == PickFolderRequestCode && _tcs != null)
                {
                    if (resultCode == Result.Ok && data?.Data != null)
                    {
                        // Take persistable URI permission
                        if (_activity != null)
                        {
                            var takeFlags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                            _activity.ContentResolver.TakePersistableUriPermission(data.Data, takeFlags);
                        }

                        var folderPath = GetFullFolderPath(data.Data);
                        _tcs.TrySetResult(folderPath ?? data.Data.ToString());
                    }
                    else
                    {
                        _tcs.TrySetResult(GetDefaultFolder());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnActivityResult error: {ex}");
                _tcs?.TrySetResult(GetDefaultFolder());
            }
        }

        private string? GetFullFolderPath(AndroidUri uri)
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    var documentFile = DocumentFile.FromTreeUri(_activity, uri);
                    if (documentFile != null && documentFile.CanWrite())
                    {
                        var filePath = GetPathFromDocumentTreeUri(uri);
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            return filePath;
                        }
                        return uri.ToString();
                    }
                }
                return GetPathFromUri(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFullFolderPath error: {ex}");
                return uri.ToString();
            }
        }

        private string? GetPathFromDocumentTreeUri(AndroidUri uri)
        {
            try
            {
                if (DocumentsContract.IsDocumentUri(_activity, uri))
                {
                    var docId = DocumentsContract.GetDocumentId(uri);
                    if (!string.IsNullOrEmpty(docId))
                    {
                        var split = docId.Split(':');
                        if (split.Length > 1)
                        {
                            var storageType = split[0];
                            var path = split[1];

                            if (string.Equals(storageType, "primary", StringComparison.OrdinalIgnoreCase))
                            {
                                return System.IO.Path.Combine(
                                    global::Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath,
                                    path);
                            }
                            else
                            {
                                return $"/storage/{storageType}/{path}";
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPathFromDocumentTreeUri error: {ex}");
                return null;
            }
        }

        private string? GetPathFromUri(AndroidUri uri)
        {
            try
            {
                if (uri == null) return null;

                string? path = null;

                if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
                {
                    var projection = new[] { MediaStore.MediaColumns.Data };
                    using (var cursor = _activity?.ContentResolver?.Query(uri, projection, null, null, null))
                    {
                        if (cursor != null && cursor.MoveToFirst())
                        {
                            var columnIndex = cursor.GetColumnIndexOrThrow(MediaStore.MediaColumns.Data);
                            path = cursor.GetString(columnIndex);
                        }
                    }
                }

                return path ?? uri.Path ?? uri.ToString();
            }
            catch
            {
                return uri.ToString();
            }
        }

        private string GetDefaultFolder()
        {
            string defaultFolder = System.IO.Path.Combine(
                global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ??
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "LockChat");

            if (!System.IO.Directory.Exists(defaultFolder))
            {
                System.IO.Directory.CreateDirectory(defaultFolder);
            }

            return defaultFolder;
        }

        public async Task<bool> CanWriteToFolder(string folderIdentifier)
        {
            try
            {
                if (string.IsNullOrEmpty(folderIdentifier))
                    return false;

                if (folderIdentifier.StartsWith("content://"))
                {
                    // Parse URI manually without using TryParse
                    AndroidUri? uri = null;
                    try
                    {
                        uri = AndroidUri.Parse(folderIdentifier);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to parse URI: {ex}");
                        return false;
                    }

                    if (uri != null)
                    {
                        var documentFile = DocumentFile.FromTreeUri(_activity, uri);
                        if (documentFile != null && documentFile.CanWrite())
                        {
                            var testFile = documentFile.CreateFile("text/plain", "test.txt");
                            if (testFile != null)
                            {
                                testFile.Delete();
                                return true;
                            }
                        }
                    }
                    return false;
                }
                else
                {
                    string testFile = System.IO.Path.Combine(folderIdentifier, "test.txt");
                    await System.IO.File.WriteAllTextAsync(testFile, "test");
                    if (System.IO.File.Exists(testFile))
                    {
                        System.IO.File.Delete(testFile);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CanWriteToFolder error: {ex}");
                return false;
            }
        }
    }
}