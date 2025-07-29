using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography; // Changed from DES to SHA for hashing
using System.Text;
using System.Threading;
using System.Linq; // Added for OrderByDescending

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages LZMA data hashes, including caching, reading, writing, and hash calculation.
    /// </summary>
    public class Download_LZMA_Data_Hash
    {
        /// <summary>
        /// Gets or sets the dictionary of file paths to their old and new hash tuples.
        /// </summary>
        public Dictionary<string, Download_LZMA_Data_Hash_Tuple> File_List { get; set; }

        /// <summary>
        /// Gets or sets the queue of file paths to be hashed.
        /// </summary>
        public Queue<string> Queue_Hash { get; set; }

        /// <summary>
        /// Gets or sets the lock object for thread-safe access to <see cref="Queue_Hash"/>.
        /// </summary>
        public object Queue_Hash_Lock { get; set; }

        /// <summary>
        /// Gets or sets the count of active worker threads.
        /// </summary>
        public int Worker_Count { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether caching is enabled.
        /// </summary>
        public bool Use_Cache { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with default settings.
        /// </summary>
        public Download_LZMA_Data_Hash() : this(0, true, new Dictionary<string, Download_LZMA_Data_Hash_Tuple>(), new Queue<string>()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with a specified worker count.
        /// </summary>
        /// <param name="workerCount">The number of worker threads to use for hashing.</param>
        public Download_LZMA_Data_Hash(int workerCount) : this(workerCount, true, new Dictionary<string, Download_LZMA_Data_Hash_Tuple>(), new Queue<string>()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with specified worker count and cache usage.
        /// </summary>
        /// <param name="workerCount">The number of worker threads to use for hashing.</param>
        /// <param name="useCache">A value indicating whether caching is enabled.</param>
        public Download_LZMA_Data_Hash(int workerCount, bool useCache) : this(workerCount, useCache, new Dictionary<string, Download_LZMA_Data_Hash_Tuple>(), new Queue<string>()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with specified worker count, cache usage, and file list.
        /// </summary>
        /// <param name="workerCount">The number of worker threads to use for hashing.</param>
        /// <param name="useCache">A value indicating whether caching is enabled.</param>
        /// <param name="fileList">The initial dictionary of file paths and hash tuples.</param>
        public Download_LZMA_Data_Hash(int workerCount, bool useCache, Dictionary<string, Download_LZMA_Data_Hash_Tuple> fileList) : this(workerCount, useCache, fileList, new Queue<string>()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with all parameters specified.
        /// </summary>
        /// <param name="workerCount">The number of worker threads to use for hashing.</param>
        /// <param name="useCache">A value indicating whether caching is enabled.</param>
        /// <param name="fileList">The initial dictionary of file paths and hash tuples.</param>
        /// <param name="queueHash">The initial queue of file paths to be hashed.</param>
        public Download_LZMA_Data_Hash(int workerCount, bool useCache, Dictionary<string, Download_LZMA_Data_Hash_Tuple> fileList, Queue<string> queueHash)
        {
            this.Queue_Hash_Lock = new object();
            this.Worker_Count = workerCount;
            this.Use_Cache = useCache;
            this.File_List = fileList;
            this.Queue_Hash = queueHash;
        }

        /// <summary>
        /// The background worker's DoWork event handler for calculating file hashes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">A <see cref="DoWorkEventArgs"/> that contains the event data.</param>
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs args)
        {
            // Use a CancellationToken to allow for graceful shutdown instead of just breaking from a while(true)
            // For simplicity, keeping the existing loop structure, but in a real-world scenario, a CancellationTokenSource
            // would be passed down to allow cancellation from the main thread.
            while (true)
            {
                string filePath = string.Empty;
                lock (Queue_Hash_Lock)
                {
                    if (this.Queue_Hash.Count == 0)
                    {
                        // No more items, decrement worker count and exit
                        Worker_Count--;
                        break;
                    }
                    filePath = this.Queue_Hash.Dequeue();
                }

                // Skip if file doesn't exist or hash is not needed
                if (!File.Exists(filePath))
                {
                    continue;
                }

                try
                {
                    // Calculate the new hash
                    string newHash = GetFileHash(filePath);

                    // Update or add the hash tuple
                    lock (File_List) // Ensure thread-safe access to File_List
                    {
                        if (this.File_List.TryGetValue(filePath, out Download_LZMA_Data_Hash_Tuple existingTuple))
                        {
                            existingTuple.New = newHash;
                            existingTuple.Ticks = DateTime.Now.Ticks; // Update timestamp
                        }
                        else
                        {
                            this.File_List.Add(filePath, new Download_LZMA_Data_Hash_Tuple(string.Empty, newHash));
                        }
                    }
                }
                catch (IOException ex)
                {
                    // Log specific I/O exceptions, e.g., file in use
                    Console.WriteLine($"IOException during hashing of {filePath}: {ex.Message}");
                    // Optionally re-queue the item or mark it as failed
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Log access denied issues
                    Console.WriteLine($"UnauthorizedAccessException during hashing of {filePath}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Catch other unexpected errors and log them
                    Console.WriteLine($"An unexpected error occurred during hashing of {filePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calculates the SHA256 hash of a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>The SHA256 hash as a Base64 string.</returns>
        private string GetFileHash(string filePath)
        {
            // Using SHA256 for better security and integrity checking than MD5.
            // Using a using statement for proper disposal of the hash algorithm.
            using (SHA256 sha256Hash = SHA256.Create())
            using (FileStream fileStream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256Hash.ComputeHash(fileStream);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Reads cached hashes from a file.
        /// </summary>
        /// <param name="filePath">The path to the cache file.</param>
        /// <returns><c>true</c> if hashes were successfully read, <c>false</c> otherwise.</returns>
        public bool ReadCachedHashes(string filePath)
        {
            if (!this.Use_Cache || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                // Reading content directly. Removed insecure DES decryption.
                // If sensitive data needs protection, a robust encryption strategy with secure key management is required.
                // For hash files, strong hashing (SHA256/512) is primary for integrity,
                // and local storage typically doesn't involve strong encryption unless explicitly required for confidentiality.
                using (StreamReader streamReader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length == 3)
                        {
                            string file = parts[0];
                            string hash = parts[1];
                            long ticks = long.Parse(parts[2]);

                            if (this.File_List.TryGetValue(file, out Download_LZMA_Data_Hash_Tuple existingTuple))
                            {
                                existingTuple.Old = hash;
                                existingTuple.Ticks = ticks;
                            }
                            else
                            {
                                this.File_List.Add(file, new Download_LZMA_Data_Hash_Tuple(hash, string.Empty, ticks));
                            }
                        }
                    }
                }
                return true;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error parsing cached hash file: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error reading cached hash file: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while reading cached hashes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Writes cached hashes to a file.
        /// </summary>
        /// <param name="filePath">The path to the cache file.</param>
        /// <param name="writeOldHashes">If set to <c>true</c>, old hashes are written; otherwise, new hashes are written.</param>
        public void WriteCachedHashes(string filePath, bool writeOldHashes)
        {
            if (!this.Use_Cache)
            {
                return;
            }

            try
            {
                // Writing content directly. Removed insecure DES encryption.
                // Ensuring directory exists before writing the file
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter streamWriter = new StreamWriter(filePath, false, Encoding.UTF8)) // Ensure UTF8 encoding
                {
                    foreach (string key in this.File_List.Keys)
                    {
                        string hash = writeOldHashes ? this.File_List[key].Old : this.File_List[key].New;

                        // Check for file existence and non-empty hash before writing
                        if (!File.Exists(key) || string.IsNullOrWhiteSpace(hash))
                        {
                            continue;
                        }

                        DateTime lastWriteTime = new FileInfo(key).LastWriteTime;
                        streamWriter.WriteLine($"{key}\t{hash}\t{lastWriteTime.Ticks}");
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error writing cached hash file: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Unauthorized access when writing cached hash file: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while writing cached hashes: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates existing hashes in the cache or adds new ones.
        /// </summary>
        /// <param name="filePath">The path to the hash file.</param>
        /// <param name="oldHash">The old hash value.</param>
        /// <param name="newHash">The new hash value.</param>
        public void UpdateCachedHashes(string filePath, string oldHash, string newHash)
        {
            if (this.File_List.TryGetValue(filePath, out Download_LZMA_Data_Hash_Tuple existingTuple))
            {
                existingTuple.Old = oldHash;
                existingTuple.New = newHash;
                existingTuple.Ticks = DateTime.Now.Ticks;
            }
            else
            {
                this.File_List.Add(filePath, new Download_LZMA_Data_Hash_Tuple(oldHash, newHash, DateTime.Now.Ticks));
            }
        }

        /// <summary>
        /// Removes old hashes from the file list based on a maximum count.
        /// </summary>
        public void RemoveOldHashes()
        {
            // Optimize by sorting once and taking the required number of elements
            // The magic number 1000 should be a configurable constant.
            const int MaxFilesToKeep = 1000;

            if (this.File_List.Count > MaxFilesToKeep)
            {
                // Order by Ticks (timestamp) in ascending order to get the oldest entries first
                var oldestFiles = this.File_List.OrderBy(x => x.Value.Ticks)
                                                 .Take(this.File_List.Count - MaxFilesToKeep)
                                                 .ToList(); // Materialize to avoid modifying collection while enumerating

                foreach (var entry in oldestFiles)
                {
                    this.File_List.Remove(entry.Key);
                }
            }
        }
    }
}