using CommunityToolkit.Maui;
using FFImageLoading.Maui;
using Lock.Pages.Chat;
using Lock.Services;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.Maui.Audio;
using Plugin.Maui.OCR;
using Syncfusion.Maui.Toolkit.Hosting;
using System.Diagnostics;

using OcrService = Plugin.Maui.OCR.IOcrService;

namespace Lock
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
#if DEBUG
                // Toggle this to true temporarily to perform a minimal startup that
                // excludes third-party extensions/registrations. Useful to isolate
                // TypeInitializationException thrown during UseMauiApp().
                bool minimalStartup = false; // <-- set to true to debug
#else
                bool minimalStartup = false;
#endif

                if (minimalStartup)
                {
                    var simpleBuilder = MauiApp.CreateBuilder();

                    simpleBuilder
                        .UseMauiApp<App>()
                        .ConfigureFonts(fonts =>
                        {
                            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                            fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                            fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                        });

#if DEBUG
                    simpleBuilder.Logging.AddDebug();
                    simpleBuilder.Services.AddLogging(configure => configure.AddDebug());
#endif

                    return simpleBuilder.Build();
                }

                var builder = MauiApp.CreateBuilder();

                builder
                    .UseMauiApp<App>()
                    .UseMauiCommunityToolkit()
                    .UseMauiCommunityToolkitMediaElement()
                    .ConfigureSyncfusionToolkit()
                    .UseFFImageLoading()
                    .UseLocalNotification(config =>
                    {
#if ANDROID
                        config.AddAndroid(android =>
                        {
                            android.AddChannel(new NotificationChannelRequest
                            {
                                Id = "lock_chat_channel",
                                Name = "Lock Chat Messages",
                                Description = "New chat messages from Lock",
                                EnableSound = true,
                                EnableVibration = true
                            });
                        });
#endif
                        config.AddCategory(new NotificationCategory(NotificationCategoryType.Status)
                        {
                            ActionList = new HashSet<NotificationAction>(new List<NotificationAction>
                            {
                                new NotificationAction(100)
                                {
                                    Title = "Reply",
                                    Android = { LaunchAppWhenTapped = false }
                                },
                                new NotificationAction(101)
                                {
                                    Title = "Mark as Read",
                                    Android = { LaunchAppWhenTapped = false }
                                }
                            })
                        });
                    })
                    .AddAudio(recordingOptions =>
                    {
#if IOS || MACCATALYST
                        recordingOptions.Category =
                            AVFoundation.AVAudioSessionCategory.Record;
                        recordingOptions.Mode =
                            AVFoundation.AVAudioSessionMode.Default;
                        recordingOptions.CategoryOptions =
                            AVFoundation.AVAudioSessionCategoryOptions.DefaultToSpeaker;
#endif
                    })
                    .UseOcr()
                    .ConfigureMauiHandlers(handlers =>
                    {
#if IOS || MACCATALYST
                        handlers.AddHandler(
                            typeof(Microsoft.Maui.Controls.CollectionView),
                            typeof(Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2));
#endif
                    })
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                        fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                        fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                    });

                // ── Handler mappings ────────────────────────────────────────────
                try
                {
                    Microsoft.Maui.Handlers.EditorHandler.Mapper
                        .AppendToMapping("NoUnderline", (handler, view) =>
                        {
#if ANDROID
                            handler.PlatformView.SetBackgroundColor(
                                Android.Graphics.Color.Transparent);
                            handler.PlatformView.BackgroundTintList =
                                Android.Content.Res.ColorStateList.ValueOf(
                                    Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                            handler.PlatformView.Layer.BorderWidth = 0;
                            handler.PlatformView.Layer.BorderColor =
                                UIKit.UIColor.Clear.CGColor;
#endif
                        });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[MAUIPROGRAM] EditorHandler mapping failed: {ex.Message}");
                }

                try
                {
                    Microsoft.Maui.Handlers.EntryHandler.Mapper
                        .AppendToMapping("NoUnderline", (handler, view) =>
                        {
#if ANDROID
                            handler.PlatformView.SetBackgroundColor(
                                Android.Graphics.Color.Transparent);
                            handler.PlatformView.BackgroundTintList =
                                Android.Content.Res.ColorStateList.ValueOf(
                                    Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                            handler.PlatformView.Layer.BorderWidth = 0;
                            handler.PlatformView.Layer.BorderColor =
                                UIKit.UIColor.Clear.CGColor;
#endif
                        });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[MAUIPROGRAM] EntryHandler mapping failed: {ex.Message}");
                }

                // ── Service registrations ───────────────────────────────────────
                try
                {
                    builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
                    builder.Services.AddSingleton<IMessageNotificationService,
                        MessageNotificationService>();
                    builder.Services.AddSingleton<ISystemNotificationService,
                        SystemNotificationService>();
                    builder.Services.AddSingleton<IMessagePollingService,
                        MessagePollingService>();
                    builder.Services.AddSingleton<IMessageHubService,
                        MessageHubService>();
                    builder.Services.AddSingleton<OcrService>(OcrPlugin.Default);
                    builder.Services.AddSingleton<ModalErrorHandler>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[MAUIPROGRAM] Service registration failed: {ex.Message}");
                    throw;
                }

                // ── Platform folder picker ──────────────────────────────────────
#if ANDROID
                builder.Services.AddSingleton<IFolderPicker,
                    Lock.Platforms.Android.FolderPickerService>();
#elif IOS
                builder.Services.AddSingleton<IFolderPicker,
                    Lock.Platforms.iOS.FolderPickerService>();
#else
                builder.Services.AddSingleton<IFolderPicker,
                    Lock.Services.FolderPicker>();
#endif

                // ── Page registrations ──────────────────────────────────────────
                try
                {
                    builder.Services.AddTransient<ChatPage>();
                    builder.Services.AddTransient<Lock.Pages.Post.CommentsPage>();
                    builder.Services.AddTransient<Lock.Pages.Post.NotificationPage>();
                    builder.Services.AddTransient<Lock.Pages.Profile.ProfilePage>();
                    builder.Services.AddTransient<Lock.Pages.Discover.DiscoverPage>();
                    builder.Services.AddTransient<Lock.Pages.Post.HiddenPostsPage>();
                    builder.Services.AddTransient<Lock.Pages.Post.SearchPage>();
                    builder.Services.AddTransient<Lock.Pages.Post.MatchPage>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[MAUIPROGRAM] Page registration failed: {ex.Message}");
                    throw;
                }

#if DEBUG
                builder.Logging.AddDebug();
                builder.Services.AddLogging(configure => configure.AddDebug());
#endif

                var app = builder.Build();

                // ── Supabase connectivity check ─────────────────────────────────
                Task.Run(async () =>
                {
                    try
                    {
                        await SupabaseService.GetAsync<object>("Users", "limit=1");
                        Debug.WriteLine("Supabase connected successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Supabase connection error: {ex.Message}");
                    }
                });

                return app;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAUIPROGRAM] FATAL CreateMauiApp: {ex}");
                Debug.WriteLine($"[MAUIPROGRAM] Inner: {ex.InnerException}");
#if ANDROID
                try
                {
                    Android.Util.Log.Error("MAUIPROGRAM", $"FATAL: {ex}");
                    Android.Util.Log.Error("MAUIPROGRAM", $"Inner: {ex.InnerException}");
                }
                catch { }
#endif
                throw;
            }
        }
    }
}