using System;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Represents a tuple for old and new file hashes along with a timestamp.
    /// </summary>
    public class Download_LZMA_Data_Hash_Tuple
    {
        /// <summary>
        /// Gets or sets the old hash value.
        /// </summary>
        public string Old { get; set; }

        /// <summary>
        /// Gets or sets the new hash value.
        /// </summary>
        public string New { get; set; }

        /// <summary>
        /// Gets or sets a timestamp in Ticks format.
        /// </summary>
        public long Ticks { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash_Tuple"/> class with specified old hash, new hash, and ticks.
        /// </summary>
        /// <param name="oldHash">The old hash value.</param>
        /// <param name="newHash">The new hash value.</param>
        /// <param name="ticks">The timestamp in Ticks format.</param>
        public Download_LZMA_Data_Hash_Tuple(string oldHash, string newHash, long ticks)
        {
            this.Old = oldHash;
            this.New = newHash;
            this.Ticks = ticks;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash_Tuple"/> class with specified old hash and new hash,
        /// and sets the Ticks to one year from the current date.
        /// </summary>
        /// <param name="oldHash">The old hash value.</param>
        /// <param name="newHash">The new hash value.</param>
        public Download_LZMA_Data_Hash_Tuple(string oldHash, string newHash) : this(oldHash, newHash, DateTime.Now.AddYears(1).Ticks)
        {
        }
    }
}