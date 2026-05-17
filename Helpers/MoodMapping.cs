// Lock/Helpers/MoodMapping.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Lock.Helpers
{
    public static class MoodMapping
    {
        // Use Lazy<T> for thread-safe lazy initialization
        private static readonly Lazy<Dictionary<string, string>> _displayToKeyMapLazy =
            new Lazy<Dictionary<string, string>>(InitializeDisplayToKeyMap);

        private static readonly Lazy<Dictionary<string, string>> _keyToDisplayMapLazy =
            new Lazy<Dictionary<string, string>>(InitializeKeyToDisplayMap);

        private static readonly Lazy<List<string>> _allKeysLazy =
            new Lazy<List<string>>(InitializeAllKeys);

        private static readonly Lazy<List<string>> _allDisplayOptionsLazy =
            new Lazy<List<string>>(InitializeAllDisplayOptions);

        // Properties to access the lazy-initialized values
        private static Dictionary<string, string> _displayToKeyMap => _displayToKeyMapLazy.Value;
        private static Dictionary<string, string> _keyToDisplayMap => _keyToDisplayMapLazy.Value;
        private static List<string> _allKeys => _allKeysLazy.Value;
        private static List<string> _allDisplayOptions => _allDisplayOptionsLazy.Value;

        // Initializer methods
        private static Dictionary<string, string> InitializeDisplayToKeyMap()
        {
            try
            {
                Debug.WriteLine("Initializing DisplayToKeyMap");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // From ProfilePage.xaml mood picker - exact matches
                    { "Serious relationship", "Serious" },
                    { "Long-term potential", "LongTerm" },
                    { "Just vibes / casual fun", "CasualFun" },
                    { "Hook-up / FWB", "HookUp" },
                    { "ENM / Open to non-monogamy", "ENM" },
                    { "Deep talks and connection", "DeepTalks" },
                    { "Let's see where it goes", "SeeWhereGoes" },
                    { "Networking / collabs / friends first", "Networking" },
                    { "OS (open situationship)", "OpenSit" },
                    { "Chalance (all-in effort)", "Chalance" },
                    
                    // Also map shorter versions that might be in the database from older versions
                    { "Serious", "Serious" },
                    { "LongTerm", "LongTerm" },
                    { "CasualFun", "CasualFun" },
                    { "HookUp", "HookUp" },
                    { "ENM", "ENM" },
                    { "DeepTalks", "DeepTalks" },
                    { "SeeWhereGoes", "SeeWhereGoes" },
                    { "Networking", "Networking" },
                    { "OpenSit", "OpenSit" },
                    { "Chalance", "Chalance" },
                    
                    // Map the original image text to ensure compatibility
                    { "ENM / Non-monogamy", "ENM" },
                    { "Let's see where it goes", "SeeWhereGoes" },
                    { "Networking / collabs", "Networking" }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing DisplayToKeyMap: {ex}");
                // Return empty dictionary as fallback
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, string> InitializeKeyToDisplayMap()
        {
            try
            {
                Debug.WriteLine("Initializing KeyToDisplayMap");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Serious", "Serious relationship" },
                    { "LongTerm", "Long-term potential" },
                    { "CasualFun", "Just vibes / casual fun" },
                    { "HookUp", "Hook-up / FWB" },
                    { "ENM", "ENM / Open to non-monogamy" },
                    { "DeepTalks", "Deep talks and connection" },
                    { "SeeWhereGoes", "Let's see where it goes" },
                    { "Networking", "Networking / collabs / friends first" },
                    { "OpenSit", "OS (open situationship)" },
                    { "Chalance", "Chalance (all-in effort)" }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing KeyToDisplayMap: {ex}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static List<string> InitializeAllKeys()
        {
            try
            {
                Debug.WriteLine("Initializing AllKeys");
                return new List<string>
                {
                    "Serious", "LongTerm", "CasualFun", "HookUp", "ENM",
                    "DeepTalks", "SeeWhereGoes", "Networking", "OpenSit", "Chalance"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing AllKeys: {ex}");
                return new List<string>();
            }
        }

        private static List<string> InitializeAllDisplayOptions()
        {
            try
            {
                Debug.WriteLine("Initializing AllDisplayOptions");
                return new List<string>
                {
                    "Serious relationship",
                    "Long-term potential",
                    "Just vibes / casual fun",
                    "Hook-up / FWB",
                    "ENM / Open to non-monogamy",
                    "Deep talks and connection",
                    "Let's see where it goes",
                    "Networking / collabs / friends first",
                    "OS (open situationship)",
                    "Chalance (all-in effort)"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing AllDisplayOptions: {ex}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Maps a display mood text (from User.Mood or Post.Mood) to an internal notification key
        /// </summary>
        public static string MapDisplayToKey(string displayMood)
        {
            if (string.IsNullOrWhiteSpace(displayMood))
                return string.Empty;

            try
            {
                var map = _displayToKeyMap; // This triggers lazy initialization if needed

                // Try exact match first (case-insensitive due to dictionary constructor)
                if (map.TryGetValue(displayMood, out var key))
                    return key;

                // Try to find a match by checking if the display mood contains any of our keys
                foreach (var kvp in map)
                {
                    if (displayMood.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Contains(displayMood, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value;
                    }
                }

                // Return as-is if no match found (fallback)
                return displayMood;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MapDisplayToKey for '{displayMood}': {ex}");
                return displayMood; // Fallback to original on error
            }
        }

        /// <summary>
        /// Maps an internal notification key to a display text (for UI)
        /// </summary>
        public static string MapKeyToDisplay(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            try
            {
                var map = _keyToDisplayMap; // This triggers lazy initialization if needed
                return map.TryGetValue(key, out var display) ? display : key;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MapKeyToDisplay for '{key}': {ex}");
                return key; // Fallback to original on error
            }
        }

        /// <summary>
        /// Gets all valid notification keys
        /// </summary>
        public static List<string> GetAllKeys()
        {
            try
            {
                return new List<string>(_allKeys); // This triggers lazy initialization if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetAllKeys: {ex}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets all display options (for ProfilePage mood picker)
        /// </summary>
        public static List<string> GetAllDisplayOptions()
        {
            try
            {
                return new List<string>(_allDisplayOptions); // This triggers lazy initialization if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetAllDisplayOptions: {ex}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks if a mood key is valid
        /// </summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var keys = _allKeys; // This triggers lazy initialization if needed
                return keys.Contains(key, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in IsValidKey for '{key}': {ex}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a display mood is valid
        /// </summary>
        public static bool IsValidDisplayMood(string displayMood)
        {
            if (string.IsNullOrEmpty(displayMood))
                return false;

            try
            {
                var displayOptions = _allDisplayOptions; // This triggers lazy initialization if needed
                var map = _displayToKeyMap; // This triggers lazy initialization if needed

                return displayOptions.Contains(displayMood, StringComparer.OrdinalIgnoreCase) ||
                       map.ContainsKey(displayMood);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in IsValidDisplayMood for '{displayMood}': {ex}");
                return false;
            }
        }

        /// <summary>
        /// Gets the display name for a given key (or returns the key if not found)
        /// </summary>
        public static string GetDisplayName(string key)
        {
            return MapKeyToDisplay(key);
        }

        /// <summary>
        /// Gets all keys that are currently enabled in a notification preferences dictionary
        /// </summary>
        public static List<string> GetEnabledKeys(Dictionary<string, bool>? preferences)
        {
            if (preferences == null)
                return new List<string>();

            try
            {
                var validKeys = _allKeys; // This triggers lazy initialization if needed

                return preferences
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .Where(k => validKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetEnabledKeys: {ex}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a default notification preferences dictionary with all values set to false
        /// </summary>
        public static Dictionary<string, bool> GetDefaultNotificationPreferences()
        {
            try
            {
                var keys = _allKeys; // This triggers lazy initialization if needed
                return keys.ToDictionary(k => k, v => false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetDefaultNotificationPreferences: {ex}");
                return new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Gets a safe version of a mood string (ensures it's a valid key)
        /// </summary>
        public static string GetSafeMoodKey(string mood)
        {
            if (string.IsNullOrWhiteSpace(mood))
                return string.Empty;

            try
            {
                string key = MapDisplayToKey(mood);
                return IsValidKey(key) ? key : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetSafeMoodKey for '{mood}': {ex}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Ensures the MoodMapping is initialized and returns true if successful
        /// </summary>
        public static bool EnsureInitialized()
        {
            try
            {
                Debug.WriteLine("Ensuring MoodMapping is initialized...");

                // Force initialization by accessing each lazy property
                var keys = _allKeys;
                var displays = _allDisplayOptions;
                var displayMap = _displayToKeyMap;
                var keyMap = _keyToDisplayMap;

                // Verify we have data
                bool success = keys.Count > 0 && displays.Count > 0 &&
                               displayMap.Count > 0 && keyMap.Count > 0;

                Debug.WriteLine($"MoodMapping initialized successfully. " +
                    $"Keys: {keys.Count}, Displays: {displays.Count}, " +
                    $"DisplayMap: {displayMap.Count}, KeyMap: {keyMap.Count}");

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize MoodMapping: {ex}");
                Debug.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets the lazy initialization (useful for testing)
        /// </summary>
        internal static void Reset()
        {
            // This is mainly for testing purposes
            // In production, we don't want to reset
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(MoodMapping).TypeHandle);
        }
    }
}