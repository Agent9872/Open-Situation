using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;
#if ANDROID
using Android.Content;
using AndroidX.Biometric;
using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
#endif

namespace Lock.Services
{
    public class BiometricService
    {
        public static async Task<bool> IsBiometricAvailableAsync()
        {
            try
            {
#if ANDROID
                var context = Android.App.Application.Context;
                var biometricManager = BiometricManager.From(context);
                int canAuthenticate = biometricManager.CanAuthenticate();
                return canAuthenticate == (int)BiometricManager.BiometricSuccess;
#else
                return await Task.FromResult(true);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Biometric availability check error: {ex}");
                return false;
            }
        }

        public static async Task<bool> AuthenticateAsync(string reason = "Authenticate to unlock chat")
        {
            try
            {
#if ANDROID
                return await AuthenticateAndroidAsync(reason);
#else
                var result = await Application.Current.MainPage.DisplayAlert(
                    "Biometric Authentication",
                    reason + "\n\nWould you like to authenticate?",
                    "Authenticate",
                    "Cancel");
                return result;
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Biometric auth error: {ex}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Biometric authentication failed: {ex.Message}",
                    "OK");
                return false;
            }
        }

#if ANDROID
        private static async Task<bool> AuthenticateAndroidAsync(string reason)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                return false;
            }

            var executor = ContextCompat.GetMainExecutor(activity);
            
            var biometricPrompt = new BiometricPrompt(activity as FragmentActivity, executor, new BiometricCallback(tcs));
            
            var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Chat Lock")
                .SetSubtitle(reason)
                .SetDescription("Use fingerprint to unlock this chat")
                .SetNegativeButtonText("Cancel")
                .Build();
            
            biometricPrompt.Authenticate(promptInfo);
            
            return await tcs.Task;
        }

        private class BiometricCallback : BiometricPrompt.AuthenticationCallback
        {
            private readonly TaskCompletionSource<bool> _tcs;
            
            public BiometricCallback(TaskCompletionSource<bool> tcs)
            {
                _tcs = tcs;
            }
            
            public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            {
                _tcs.TrySetResult(true);
                base.OnAuthenticationSucceeded(result);
            }
            
            public override void OnAuthenticationFailed()
            {
                _tcs.TrySetResult(false);
                base.OnAuthenticationFailed();
            }
            
            public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence errString)
            {
                _tcs.TrySetResult(false);
                base.OnAuthenticationError(errorCode, errString);
            }
        }
#endif

        public static async Task<bool> SetupAndVerifyBiometricAsync()
        {
            try
            {
                bool isAvailable = await IsBiometricAvailableAsync();
                if (!isAvailable)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Not Available",
                        "Biometric authentication is not available on this device. Please use PIN or Pattern lock instead.",
                        "OK");
                    return false;
                }

                bool authenticated = await AuthenticateAsync("Set up biometric lock for this chat");

                if (authenticated)
                {
                    return true;
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Setup Failed",
                        "Biometric authentication failed. Please try again or use PIN/Pattern lock.",
                        "OK");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupAndVerifyBiometricAsync error: {ex}");
                return false;
            }
        }
    }
}