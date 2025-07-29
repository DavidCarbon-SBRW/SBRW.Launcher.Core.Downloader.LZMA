namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Provides enumeration for download statuses.
    /// </summary>
    public static class Download_LZMA_Enumerator
    {
        /// <summary>
        /// Defines the possible statuses of a download operation.
        /// </summary>
        public enum Download_Status
        {
            /// <summary>
            /// The download is queued and awaiting processing.
            /// </summary>
            Queued,
            /// <summary>
            /// The download is currently in progress.
            /// </summary>
            Downloading,
            /// <summary>
            /// The download has been successfully completed.
            /// </summary>
            Downloaded,
            /// <summary>
            /// The download operation was cancelled.
            /// </summary>
            Canceled,
            /// <summary>
            /// The status of the download is unknown or an error occurred.
            /// </summary>
            Unknown
        }
    }
}