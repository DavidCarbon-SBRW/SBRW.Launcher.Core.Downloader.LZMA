using System;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Provides delegate definitions for various LZMA downloader events.
    /// </summary>
    public static class Download_LZMA_Delegates
    {
        /// <summary>
        /// Represents a delegate for the event that occurs when an LZMA download operation finishes successfully.
        /// </summary>
        public delegate void Download_LZMA_Finished();

        /// <summary>
        /// Represents a delegate for the event that occurs when an LZMA download operation fails.
        /// </summary>
        /// <param name="ex">The exception that caused the failure.</param>
        public delegate void Download_LZMA_Failed(Exception ex);

        /// <summary>
        /// Represents a delegate for the event that provides LZMA download progress updates.
        /// </summary>
        /// <param name="dowloadLength">The total length of the download.</param>
        /// <param name="downloadCurrent">The current downloaded length.</param>
        /// <param name="compressedLength">The total compressed length (if applicable).</param>
        /// <param name="fileName">The name of the file being downloaded.</param>
        /// <param name="skipdownload">An integer indicating if download was skipped (e.g., 0 for not skipped).</param>
        public delegate void Download_LZMA_Progress_Updated(long dowloadLength, long downloadCurrent, long compressedLength, string fileName, int skipdownload = 0);

        /// <summary>
        /// Represents a delegate for the event that provides LZMA extraction progress updates.
        /// </summary>
        /// <param name="filename">The name of the file being extracted.</param>
        /// <param name="currentCount">The current count of extracted files/items.</param>
        /// <param name="allFilesCount">The total count of files/items to extract.</param>
        public delegate void Download_LZMA_Show_Extract(string filename, long currentCount, long allFilesCount);

        /// <summary>
        /// Represents a delegate for the event that requests a message to be displayed to the user.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="header">The header or title of the message.</param>
        public delegate void Download_LZMA_Show_Message(string message, string header);
    }
}