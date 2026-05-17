using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lock.Models.Chat
{
    public class ChatMediaItem : INotifyPropertyChanged
    {
        private string _path = string.Empty;
        private string _type = "image";
        private string? _caption;
        private string? _thumbnailPath;
        private int? _durationSeconds;
        private string? _waveformData;
        private bool _isPlaying;
        private double _playbackProgress;

        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAudio));
                    OnPropertyChanged(nameof(IsImage));
                    OnPropertyChanged(nameof(IsVideo));
                }
            }
        }

        public string? Caption
        {
            get => _caption;
            set
            {
                if (_caption != value)
                {
                    _caption = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? ThumbnailPath
        {
            get => _thumbnailPath;
            set
            {
                if (_thumbnailPath != value)
                {
                    _thumbnailPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? DurationSeconds
        {
            get => _durationSeconds;
            set
            {
                if (_durationSeconds != value)
                {
                    _durationSeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedDuration));
                    OnPropertyChanged(nameof(DisplayDuration));
                }
            }
        }

        public string? WaveformData
        {
            get => _waveformData;
            set
            {
                if (_waveformData != value)
                {
                    _waveformData = value;
                    OnPropertyChanged();
                }
            }
        }

        // UI state (runtime only) - these won't be saved to database
        [Ignore]
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayPauseText));
                    OnPropertyChanged(nameof(DisplayDuration));
                }
            }
        }

        [Ignore]
        public double PlaybackProgress
        {
            get => _playbackProgress;
            set
            {
                if (Math.Abs(_playbackProgress - value) > 0.001)
                {
                    _playbackProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayDuration));
                }
            }
        }



        // Computed properties for UI
        [Ignore]
        public bool IsAudio => string.Equals(Type, "audio", StringComparison.OrdinalIgnoreCase);

        [Ignore]
        public bool IsImage => string.Equals(Type, "image", StringComparison.OrdinalIgnoreCase);

        [Ignore]
        public bool IsVideo => string.Equals(Type, "video", StringComparison.OrdinalIgnoreCase);

        [Ignore]
        public string PlayPauseText => IsPlaying ? "⏸" : "▶";

        private string? _currentDisplayDuration;

public string? CurrentDisplayDuration
{
    get => _currentDisplayDuration;
    set
    {
        _currentDisplayDuration = value;
        OnPropertyChanged(nameof(CurrentDisplayDuration));
        OnPropertyChanged(nameof(DisplayDuration)); // also refresh DisplayDuration
    }
}

        [Ignore]
        public string FormattedDuration
        {
            get
            {
                if (!DurationSeconds.HasValue || DurationSeconds.Value <= 0)
                    return "0:00";

                var ts = TimeSpan.FromSeconds(DurationSeconds.Value);
                return ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                    : $"0:{ts.Seconds:D2}";
            }
        }

        [Ignore]
        public string DisplayDuration
        {
            get
            {
                if (!DurationSeconds.HasValue || DurationSeconds.Value <= 0)
                    return "0:00";

                // ── If a live countdown is being pushed, use it ──
                if (!string.IsNullOrEmpty(_currentDisplayDuration) && IsPlaying)
                    return _currentDisplayDuration;

                // ── If playing, compute from progress ──
                if (IsPlaying && PlaybackProgress > 0)
                {
                    var elapsedSeconds = DurationSeconds.Value * PlaybackProgress;
                    var remainingSeconds = Math.Max(0, DurationSeconds.Value - elapsedSeconds);
                    var ts = TimeSpan.FromSeconds(remainingSeconds);
                    return ts.TotalMinutes >= 1
                        ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                        : $"0:{ts.Seconds:D2}";
                }

                // ── Default: show total duration ──
                return FormattedDuration;
            }
        }


        // Helper to create an audio media item
        public static ChatMediaItem CreateAudio(string path, int durationSeconds, string? waveformData = null, string? caption = null)
        {
            return new ChatMediaItem
            {
                Path = path,
                Type = "audio",
                DurationSeconds = durationSeconds,
                WaveformData = waveformData,
                Caption = caption
            };
        }

        // Helper to create an image media item
        public static ChatMediaItem CreateImage(string path, string? thumbnailPath = null, string? caption = null)
        {
            return new ChatMediaItem
            {
                Path = path,
                Type = "image",
                ThumbnailPath = thumbnailPath,
                Caption = caption
            };
        }

        // Helper to create a video media item
        public static ChatMediaItem CreateVideo(string path, string? thumbnailPath = null, int? durationSeconds = null, string? caption = null)
        {
            return new ChatMediaItem
            {
                Path = path,
                Type = "video",
                ThumbnailPath = thumbnailPath,
                DurationSeconds = durationSeconds,
                Caption = caption
            };
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        // Make this method public so it can be called from other classes
        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
