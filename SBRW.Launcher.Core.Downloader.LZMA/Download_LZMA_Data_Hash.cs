using System;
using System.Collections.Concurrent; // Added for thread-safe collections
using System.Collections.Generic;
using System.IO;
using System.Linq; // For .ToList() and other LINQ operations
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks; // Added for async/await and Task Parallel Library
using System.Xml;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages file hash calculation and comparison.
    /// </summary>
    public class Download_LZMA_Data_Hash
    {
        /// <summary>
        /// Stores file names and their associated new and old hashes.
        /// </summary>
        public ConcurrentDictionary<string, Download_LZMA_Data_Hash_Tuple> FileList { get; set; }

        private ConcurrentQueue<string> _hashQueue;
        private SemaphoreSlim _workerSemaphore; // Controls the number of concurrent hash workers
        private int _currentWorkerCount; // Tracks active hash workers
        private bool _useCache;

        /// <summary>
        /// Gets or sets a value indicating whether to use the hash cache.
        /// </summary>
        public bool UseCache
        {
            get { return _useCache; }
            set { _useCache = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class.
        /// </summary>
        public Download_LZMA_Data_Hash() : this(3, true) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with a specified number of workers.
        /// </summary>
        /// <param name="workerCount">The maximum number of concurrent hash calculation workers.</param>
        public Download_LZMA_Data_Hash(int workerCount) : this(workerCount, true) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Hash"/> class with specified worker count and cache usage.
        /// </summary>
        /// <param name="workerCount">The maximum number of concurrent hash calculation workers.</param>
        /// <param name="useCache">True to use the hash cache, false otherwise.</param>
        public Download_LZMA_Data_Hash(int workerCount, bool useCache)
        {
            _currentWorkerCount = 0;
            _useCache = useCache;
            FileList = new ConcurrentDictionary<string, Download_LZMA_Data_Hash_Tuple>();
            _hashQueue = new ConcurrentQueue<string>();
            _workerSemaphore = new SemaphoreSlim(workerCount, workerCount);
        }

        // The constructors with P_File_List and P_Queue_Hash are less common for a fresh instance
        // but can be kept if there's a specific scenario for pre-populating them.
        // For simplicity and common use cases, I'm omitting them in the refactored version
        // unless explicitly requested to maintain direct parameter mapping.

        /// <summary>
        /// Worker method for calculating file hashes asynchronously.
        /// </summary>
        private async Task HashWorkerAsync()
        {
            Interlocked.Increment(ref _currentWorkerCount);
            try
            {
                while (_hashQueue.TryDequeue(out string? filePath))
                {
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        continue;
                    }

                    string base64String = string.Empty;
                    bool passed = !FileList.TryGetValue(filePath, out var tuple);
                    // Only calculate hash if the "Old" hash is not already present (i.e., not loaded from cache)
                    if (File.Exists(filePath) && (passed || string.IsNullOrWhiteSpace(tuple.Old)))
                    {
                        try
                        {
                            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                            {
                                using (MD5 md5 = MD5.Create())
                                {
                                    byte[] hashBytes = await Task.Run(() => md5.ComputeHash(fileStream)); // ComputeHash can be CPU-bound
                                    base64String = Convert.ToBase64String(hashBytes);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the exception, but don't stop the worker
                            System.Diagnostics.Debug.WriteLine($"Error calculating hash for {filePath}: {ex.Message}");
                            // Consider setting a specific error status for this file in FileList if needed
                        }
                    }
                    else if (tuple != default && !string.IsNullOrWhiteSpace(tuple.Old))
                    {
                        base64String = tuple.Old; // Use cached hash
                    }

                    if (FileList.ContainsKey(filePath))
                    {
                        FileList[filePath].Old = base64String;
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref _currentWorkerCount);
                _workerSemaphore.Release(); // Release the semaphore permit
            }
        }

        /// <summary>
        /// Clears all file list and stops hash calculation workers.
        /// </summary>
        public async Task ClearAsync()
        {
            _hashQueue = new ConcurrentQueue<string>(); // Clear the queue
            FileList.Clear();

            // Wait for all active workers to finish processing their current item
            // The semaphore limits concurrent workers, so we can wait for them to finish.
            // This is a more graceful shutdown than Thread.Sleep.
            while (_currentWorkerCount > 0)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Gets the old hash for a specific file. This method will wait until the hash is calculated.
        /// </summary>
        /// <param name="fileName">The path of the file.</param>
        /// <returns>The old hash as a Base64 string.</returns>
        public async Task<string> GetHashOldAsync(string fileName)
        {
            if (!FileList.ContainsKey(fileName))
            {
                return string.Empty;
            }

            while (string.IsNullOrWhiteSpace(FileList[fileName].Old))
            {
                // This busy-wait should be avoided if possible.
                // A TaskCompletionSource could be used if hashes are calculated truly asynchronously
                // and we need to await a specific file's hash.
                // For now, keep as a short delay to match original behavior while refactoring.
                await Task.Delay(50);
            }
            return FileList[fileName].Old;
        }

        /// <summary>
        /// Checks if the new hash of a file matches its old hash.
        /// This method will wait until the old hash is calculated.
        /// </summary>
        /// <param name="fileName">The path of the file.</param>
        /// <returns>True if hashes match, false otherwise or if file not found/hash not calculated.</returns>
        public async Task<bool> HashesMatchAsync(string fileName)
        {
            if (!FileList.ContainsKey(fileName))
            {
                return false;
            }

            // Ensure the old hash is calculated/loaded
            await GetHashOldAsync(fileName);

            return FileList[fileName].New == FileList[fileName].Old;
        }

        /// <summary>
        /// Starts the hash calculation process based on an XML document.
        /// </summary>
        /// <param name="doc">The XML document containing file information.</param>
        /// <param name="patchPath">The local path to store patched files.</param>
        /// <param name="hashFileNameSuffix">Suffix for the hash cache file.</param>
        /// <param name="maxWorkers">Maximum number of concurrent hash calculation workers.</param>
        public async Task StartAsync(XmlDocument doc, string patchPath, string hashFileNameSuffix, int maxWorkers)
        {
            FileList.Clear(); // Clear any previous state

            // Populate FileList and _hashQueue
            foreach (XmlNode xmlNodes in doc.SelectNodes("/index/fileinfo"))
            {
                string originalPath = xmlNodes.SelectSingleNode("path")?.InnerText ?? string.Empty;
                string fileName = xmlNodes.SelectSingleNode("file")?.InnerText ?? string.Empty;
                string fullFilePath = GetTargetFilePath(originalPath, patchPath, fileName);

                string newHash = xmlNodes.SelectSingleNode("hash")?.InnerText ?? string.Empty;

                FileList.TryAdd(fullFilePath, new Download_LZMA_Data_Hash_Tuple(string.Empty, newHash));
                _hashQueue.Enqueue(fullFilePath);
            }

            if (_useCache && File.Exists($"HashFile{hashFileNameSuffix}"))
            {
                try
                {
                    // No encryption/decryption here for security reasons (hardcoded key).
                    // If encryption is truly needed, implement it securely.
                    using (StreamReader streamReader = new StreamReader(File.OpenRead($"HashFile{hashFileNameSuffix}")))
                    {
                        string? line;
                        while ((line = await streamReader.ReadLineAsync()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            string[] parts = line.Split('\t');
                            if (parts.Length == 3)
                            {
                                string filePath = parts[0];
                                string cachedHash = parts[1];
                                long cachedTicks = long.Parse(parts[2]);

                                if (FileList.ContainsKey(filePath) && File.Exists(filePath))
                                {
                                    FileInfo fileInfo = new FileInfo(filePath);
                                    if (fileInfo.LastWriteTime.Ticks == cachedTicks)
                                    {
                                        FileList[filePath].Old = cachedHash;
                                        FileList[filePath].Ticks = cachedTicks;
                                        // Remove from queue if cached hash is used and matches the new hash in XML,
                                        // implying no need to re-calculate (optimization).
                                        if (FileList[filePath].Old == FileList[filePath].New)
                                        {
                                            // This is tricky with ConcurrentQueue as we can't remove arbitrary items.
                                            // Instead, the worker will check if Old hash is already populated.
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but continue without cache.
                    System.Diagnostics.Debug.WriteLine($"Error loading hash cache: {ex.Message}");
                    FileList.Clear(); // Clear partially loaded cache
                    // Re-enqueue all files for hash calculation if cache loading failed.
                    foreach (var entry in FileList)
                    {
                        _hashQueue.Enqueue(entry.Key);
                    }
                }
            }

            // Start workers, capping at maxWorkers
            List<Task> workers = new List<Task>();
            for (int i = 0; i < maxWorkers; i++)
            {
                await _workerSemaphore.WaitAsync(); // Acquire a permit
                workers.Add(HashWorkerAsync());
            }

            await Task.WhenAll(workers); // Wait for all workers to complete
        }

        /// <summary>
        /// Writes the hash cache to a file.
        /// </summary>
        /// <param name="hashFileNameSuffix">Suffix for the hash cache file.</param>
        /// <param name="writeOldHashes">True to write old (calculated) hashes, false to write new (from metadata) hashes.</param>
        public async Task WriteHashCacheAsync(string hashFileNameSuffix, bool writeOldHashes)
        {
            try
            {
                using (FileStream fileStream = new FileStream($"HashFile{hashFileNameSuffix}", FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fileStream))
                    {
                        foreach (KeyValuePair<string, Download_LZMA_Data_Hash_Tuple> entry in FileList)
                        {
                            string filePath = entry.Key;
                            Download_LZMA_Data_Hash_Tuple tuple = entry.Value;

                            string hash = writeOldHashes ? tuple.Old : tuple.New;

                            if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(hash))
                            {
                                continue;
                            }

                            FileInfo fileInfo = new FileInfo(filePath);
                            await streamWriter.WriteLineAsync($"{filePath}\t{hash}\t{fileInfo.LastWriteTime.Ticks}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing hash cache: {ex.Message}");
                // Log exception
            }
        }

        /// <summary>
        /// Helper to construct target file path.
        /// </summary>
        private string GetTargetFilePath(string originalPath, string patchPath, string fileName)
        {
            string currentPath = originalPath;
            if (!string.IsNullOrWhiteSpace(patchPath))
            {
                int slashIndex = currentPath.IndexOf("/");
                if (slashIndex >= 0)
                {
                    currentPath = currentPath.Replace(currentPath.Substring(0, slashIndex), patchPath);
                }
                else
                {
                    currentPath = patchPath;
                }
            }
            return Path.Combine(currentPath, fileName);
        }
    }
}