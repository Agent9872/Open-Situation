using CommunityToolkit.Maui;
using Lock.Chat.Services;
using Lock.Pages.Chat;
using Lock.Services;                    // Keep this for your other services
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using Plugin.Maui.Audio;
using Plugin.Maui.OCR;                  // This brings Plugin.Maui.OCR.IOcrService
using Syncfusion.Maui.Toolkit.Hosting;
using System.Diagnostics;

// Alias to avoid conflict with your own IOcrService
using OcrService = Plugin.Maui.OCR.IOcrService;

namespace Lock
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureSyncfusionToolkit()
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
                            new NotificationAction(100) { Title = "Reply", Android = { LaunchAppWhenTapped = false } },
                            new NotificationAction(101) { Title = "Mark as Read", Android = { LaunchAppWhenTapped = false } }
                        })
                    });
                })
                .AddAudio(recordingOptions =>
                {
#if IOS || MACCATALYST
                    recordingOptions.Category = AVFoundation.AVAudioSessionCategory.Record;
                    recordingOptions.Mode = AVFoundation.AVAudioSessionMode.Default;
                    recordingOptions.CategoryOptions = AVFoundation.AVAudioSessionCategoryOptions.DefaultToSpeaker;
#endif
                })
                .UseOcr()   // This registers the plugin's OCR service
                .ConfigureMauiHandlers(handlers =>
                {
#if IOS || MACCATALYST
                    handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

            // Remove underline handlers (unchanged)
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.Layer.BorderWidth = 0;
                handler.PlatformView.Layer.BorderColor = UIKit.UIColor.Clear.CGColor;
#endif
            });

            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                handler.PlatformView.Layer.BorderWidth = 0;
                handler.PlatformView.Layer.BorderColor = UIKit.UIColor.Clear.CGColor;
#endif
            });

            // SERVICE REGISTRATIONS
            builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
            builder.Services.AddSingleton<IMessageNotificationService, MessageNotificationService>();
            builder.Services.AddSingleton<ISystemNotificationService, SystemNotificationService>();
            builder.Services.AddSingleton<IMessagePollingService, MessagePollingService>();
            builder.Services.AddSingleton<IMessageHubService, MessageHubService>();

            // Register the PLUGIN's IOcrService correctly (using alias)
            builder.Services.AddSingleton<OcrService>(OcrPlugin.Default);

            // Platform-specific FolderPicker (unchanged)
#if ANDROID
            builder.Services.AddSingleton<IFolderPicker, Lock.Platforms.Android.FolderPickerService>();
#elif IOS
            builder.Services.AddSingleton<IFolderPicker, Lock.Platforms.iOS.FolderPickerService>();
#else
            builder.Services.AddSingleton<IFolderPicker, Lock.Services.FolderPicker>();
#endif

            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();

            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<Lock.Pages.Post.CommentsPage>();
            builder.Services.AddTransient<Lock.Pages.Post.NotificationPage>();
            builder.Services.AddTransient<Lock.Pages.Profile.ProfilePage>();
            builder.Services.AddTransient<Lock.Pages.Discover.DiscoverPage>();
            builder.Services.AddTransient<Lock.Pages.Post.HiddenPostsPage>();
            builder.Services.AddTransient<Lock.Pages.Post.SearchPage>();
            builder.Services.AddTransient<Lock.Pages.Post.MatchPage>();

            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            var app = builder.Build();

            // Database initialization (unchanged)
            Task.Run(async () =>
            {
                try
                {
                    await DatabaseService.InitializeAsync();
                    await GroupDatabaseService.InitializeAsync();
                    await GroupRepository.InitializeAsync();

                    // ADD THIS
                    await SupabaseService.GetClientAsync();
                    Debug.WriteLine("Supabase connected successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Initialization error: {ex.Message}");
                }
            });

            return app;
        }
    }
}