using System;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Represents a tuple for storing old and new hashes of a file, along with its last write time ticks.
    /// </summary>
    public class Download_LZMA_Data_Hash_Tuple
    {
        /// <summary>
        /// Gets or sets the old hash of the file (e.g., calculated from disk).
        /// </summary>
        public string Old { get; set; }

        /// <summary>
        /// Gets or sets the new hash of the file (e.g., from manifest/metadata).
        /// </summary>
        public string New { get; set; }

        /// <summary>
        /// Gets or sets the last write time ticks of the file when the hash was recorded.
        /// </summary>
        public long Ticks { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash_Tuple"/> class.
        /// </summary>
        /// <param name="oldHash">The old hash.</param>
        /// <param name="newHash">The new hash.</param>
        public Download_LZMA_Data_Hash_Tuple(string oldHash, string newHash)
        {
            Old = oldHash;
            New = newHash;
            Ticks = 0; // Default
        }
    }
}
