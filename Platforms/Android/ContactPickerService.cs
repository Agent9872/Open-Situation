#if ANDROID
using Android.App;
using Android.Content;

namespace Lock.Platforms.Android
{
    public static class ContactPickerService
    {
        public const int RequestCode = 9001;
        private static TaskCompletionSource<string?>? _tcs;
        private static MainActivity? _mainActivity;

        public static void Initialize(MainActivity activity)
        {
            _mainActivity = activity;
        }

        public static Task<string?> PickContactPhoneAsync()
        {
            _tcs = new TaskCompletionSource<string?>();

            try
            {
                if (_mainActivity == null)
                {
                    _tcs.TrySetResult(null);
                    return _tcs.Task;
                }

                var intent = new Intent(Intent.ActionPick,
                    global::Android.Provider.ContactsContract.CommonDataKinds.Phone.ContentUri);

                _mainActivity.StartActivityForResult(intent, RequestCode);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }

            return _tcs.Task;
        }

        public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode != RequestCode) return;

            if (resultCode != Result.Ok || data?.Data == null)
            {
                _tcs?.TrySetResult(null);
                return;
            }

            try
            {
                var resolver = _mainActivity?.ContentResolver;
                if (resolver == null)
                {
                    _tcs?.TrySetResult(null);
                    return;
                }

                // The URI already points to a specific phone row
                string[] projection = new string[]
                {
                    global::Android.Provider.ContactsContract.CommonDataKinds.Phone.Number,
                    global::Android.Provider.ContactsContract.ContactsColumns.DisplayName
                };

                using var cursor = resolver.Query(data.Data, projection, null, null, null);

                if (cursor != null && cursor.MoveToFirst())
                {
                    var phoneIdx = cursor.GetColumnIndex(
                        global::Android.Provider.ContactsContract.CommonDataKinds.Phone.Number);

                    var phone = phoneIdx >= 0 ? cursor.GetString(phoneIdx) : null;
                    _tcs?.TrySetResult(phone);
                }
                else
                {
                    _tcs?.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContactPickerService.OnActivityResult error: {ex}");
                _tcs?.TrySetException(ex);
            }
        }
    }
}
#endif