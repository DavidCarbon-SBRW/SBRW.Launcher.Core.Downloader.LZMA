using System;
using System.Diagnostics;
using System.IO;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Provides static settings for the LZMA Downloader.
    /// </summary>
    public static class Download_LZMA_Settings
    {
        private static string _versionCache = "0.0.2.4";
        private static bool _versionCheckCompleted = false;
        /// <summary>
        /// Gets the current version of the LZMA Downloader library.
        /// Attempts to read from the assembly file, otherwise defaults to a cached value.
        /// </summary>
        public static string Version
        {
            get
            {
                if (!_versionCheckCompleted)
                {
                    try
                    {
                        string assemblyPath = "SBRW.Launcher.Core.Downloader.LZMA.dll";
                        if (File.Exists(assemblyPath))
                        {
                            // Using FileVersionInfo to get the file version
                            _versionCache = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception to understand why version info couldn't be retrieved.
                        // Do not rethrow, as a default version will be used.
                        Console.WriteLine($"Warning: Could not retrieve assembly version info. Error: {ex.Message}");
                    }
                    finally
                    {
                        _versionCheckCompleted = true; // Mark as checked regardless of success or failure
                    }
                }

                // Ensure a non-empty version is always returned
                if (string.IsNullOrWhiteSpace(_versionCache))
                {
                    _versionCache = "0.0.2.0"; // Fallback default if file version is empty or null
                }
                return _versionCache;
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether the system is Unix-based.
        /// </summary>
        public static bool System_Unix { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether alternative web calls (e.g., specific WebClient implementation) are used.
        /// With HttpClient, this might become less relevant unless it refers to a different HttpClient configuration.
        /// </summary>
        public static bool Alternative_WebCalls { get; set; }
        /// <summary>
        /// Internal user-agent header string for web requests.
        /// </summary>
        internal static string Header_LZMA { get; } = $"SBRW.Launcher.Core.Downloader.LZMA Version {Version} (+https://github.com/DavidCarbon-SBRW/SBRW.Launcher.Core.Downloader.LZMA)";
        /// <summary>
        /// Global Boolean for Web Call Timeout before terminating the connection.
        /// </summary>
        public static bool Launcher_WebCall_Timeout_Enable { get; set; }
        /// <summary>
        /// Cached Internal Value for web call timeout in seconds.
        /// </summary>
        internal static int Launcher_WebCall_Timeout_Cache { get; set; } = 30;
        /// <summary>
        /// Gets the global web call timeout in seconds.
        /// </summary>
        /// <returns>The web call timeout in seconds.</returns>
        public static int Launcher_WebCall_Timeout() => Launcher_WebCall_Timeout_Cache;
        /// <summary>
        /// Sets the global web call timeout in seconds.
        /// Validates the provided seconds to be within a reasonable range (1-179 seconds),
        /// otherwise defaults to 30 seconds.
        /// </summary>
        /// <param name="providedSeconds">The timeout in seconds.</param>
        /// <returns>The actual timeout value set (either provided or default).</returns>
        public static int Launcher_WebCall_Timeout(int providedSeconds)
        {
            try
            {
                // Enforce minimum of 1 second for timeout
                if (providedSeconds <= 0 || providedSeconds >= 180) // Max 179 seconds to fit within 3 minutes (180s) total interval
                {
                    return Launcher_WebCall_Timeout_Cache = 30; // Default to 30 seconds
                }
                else
                {
                    return Launcher_WebCall_Timeout_Cache = providedSeconds;
                }
            }
            catch (Exception ex)
            {
                // Log the exception, but return default value
                Console.WriteLine($"Error setting Launcher_WebCall_Timeout. Error: {ex.Message}");
                return Launcher_WebCall_Timeout_Cache = 30; // Default on exception
            }
        }
    }
}