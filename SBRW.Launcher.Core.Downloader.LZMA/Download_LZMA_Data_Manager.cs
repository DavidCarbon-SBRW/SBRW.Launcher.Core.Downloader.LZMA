using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Generic;
using System.ComponentModel; // Still needed for BackgroundWorker if not fully migrated to Task.Run
using System.Net.Http; // Changed from System.Net.WebClient
using System.Net.Cache; // Still used for RequestCachePolicy, though less relevant with HttpClient's default caching
using System.Threading;
using System.Threading.Tasks; // Added for async/await and Task-based operations
using System.Xml; // Still used, though XDocument is preferred
using static SBRW.Launcher.Core.Downloader.LZMA.Download_LZMA_Enumerator;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages concurrent LZMA data downloads.
    /// </summary>
    public class Download_LZMA_Data_Manager : IDisposable
    {
        private int _workerCount;
        /// <summary>
        /// Gets or sets the current count of active background workers.
        /// </summary>
        private int Worker_Count
        {
            get { return _workerCount; }
            set { _workerCount = value; }
        }

        /// <summary>
        /// Max Background Workers in an Instance
        /// </summary>
        /// <remarks>Default is 3</remarks>
        public int Workers_Max { get; set; } = 3;

        private Dictionary<string, DownloadItem> Download_List { get; set; }
        private LinkedList<string> Download_Queue { get; set; }
        private List<BackgroundWorker> Workers_Live { get; set; } // Can be removed if fully migrating to Task.Run

        /// <summary>
        /// Max Active Chunks in an Instance
        /// </summary>
        /// <remarks>Default is 16</remarks>
        public int Active_Chunks_Max { get; set; } = 16;

        private SemaphoreSlim _activeChunkSemaphore; // Replaces Free_ChunksLock for better concurrency control

        private bool _managerRunning;
        /// <summary>
        /// Gets a value indicating whether the download manager is currently running.
        /// </summary>
        public bool ManagerRunning
        {
            get { return this._managerRunning; }
            private set { this._managerRunning = value; }
        }

        private readonly HttpClient _httpClient; // Use HttpClient for all web calls

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Manager"/> class with default settings.
        /// </summary>
        public Download_LZMA_Data_Manager() : this(3, 16) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Manager"/> class with specified worker and chunk limits.
        /// </summary>
        /// <param name="maxWorkers">The maximum number of concurrent workers (download threads).</param>
        /// <param name="maxActiveChunks">The maximum number of active download chunks.</param>
        public Download_LZMA_Data_Manager(int maxWorkers, int maxActiveChunks)
        {
            Workers_Max = maxWorkers;
            Active_Chunks_Max = maxActiveChunks;
            Download_List = new Dictionary<string, DownloadItem>();
            Download_Queue = new LinkedList<string>();
            Workers_Live = new List<BackgroundWorker>(); // Keep for now if BackgroundWorker is still used
            _activeChunkSemaphore = new SemaphoreSlim(maxActiveChunks); // Initialize semaphore
            _httpClient = new HttpClient(); // Initialize HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Download_LZMA_Settings.Header_LZMA);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(
                Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds :
                TimeSpan.FromMinutes(1).TotalMilliseconds
            );
        }

        /// <summary>
        /// Adds a file to the download queue.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        public void AddFileToQueue(string url)
        {
            lock (Download_Queue)
            {
                if (!Download_List.ContainsKey(url))
                {
                    Download_List.Add(url, new DownloadItem());
                    Download_Queue.AddLast(url);
                }
            }
        }

        /// <summary>
        /// Background worker's DoWork event handler for managing downloads.
        /// This method has been significantly refactored to use async/await and HttpClient.
        /// </summary>
        private async Task BackgroundWorker_DoWork_Async(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && ManagerRunning)
            {
                string url = string.Empty;

                // Wait for an available chunk slot
                await _activeChunkSemaphore.WaitAsync(cancellationToken);

                try
                {
                    lock (Download_Queue)
                    {
                        if (Download_Queue.Count == 0)
                        {
                            // If queue is empty, release the semaphore and exit the worker if no more work
                            _activeChunkSemaphore.Release();
                            break;
                        }
                        // Dequeue from the front to maintain FIFO order
                        url = Download_Queue.First.Value;
                        Download_Queue.RemoveFirst();
                    }

                    if (Download_List.TryGetValue(url, out DownloadItem item))
                    {
                        lock (item) // Lock on item to prevent race conditions during status update
                        {
                            if (item.Status == Download_Status.Canceled)
                            {
                                Console.WriteLine($"Download for {url} was cancelled before starting.");
                                continue; // Skip to next iteration
                            }
                            item.Status = Download_Status.Downloading;
                        }

                        Console.WriteLine($"Starting download for: {url}");

                        // Use HttpClient for download
                        byte[] downloadedData = await _httpClient.GetByteArrayAsync(url, cancellationToken);
                        item.Data = downloadedData;
                        lock (item)
                        {
                            item.Status = Download_Status.Downloaded;
                        }
                        Console.WriteLine($"Finished download for: {url}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Download for {url} was cancelled.");
                    if (Download_List.TryGetValue(url, out DownloadItem item))
                    {
                        lock (item)
                        {
                            item.Status = Download_Status.Canceled;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"HTTP error downloading {url}: {ex.Message}");
                    if (Download_List.TryGetValue(url, out DownloadItem item))
                    {
                        lock (item)
                        {
                            // Mark as unknown or failed if an error occurs
                            item.Status = Download_Status.Unknown;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred downloading {url}: {ex.Message}");
                    if (Download_List.TryGetValue(url, out DownloadItem item))
                    {
                        lock (item)
                        {
                            item.Status = Download_Status.Unknown;
                        }
                    }
                }
                finally
                {
                    _activeChunkSemaphore.Release(); // Release the chunk slot
                }
            }
        }


        /// <summary>
        /// Starts the download manager, initiating background workers.
        /// </summary>
        public void Start()
        {
            if (ManagerRunning) return;

            ManagerRunning = true;
            _workerCount = 0; // Reset worker count

            for (int i = 0; i < Workers_Max; i++)
            {
                // Start tasks directly for async operations
                // In a real application, you might want to store these Tasks
                // to await their completion or handle exceptions more gracefully.
                // For demonstration, fire and forget for now.
                Task.Run(() => BackgroundWorker_DoWork_Async(CancellationToken.None)); // CancellationToken can be managed externally
                _workerCount++;
            }
        }

        /// <summary>
        /// Stops the download manager, signaling workers to terminate.
        /// </summary>
        public void Stop()
        {
            ManagerRunning = false;
            // Additional cancellation logic should be implemented if CancellationToken is used
        }

        /// <summary>
        /// Cancels all active and queued downloads.
        /// </summary>
        public void CancelAllDownloads()
        {
            lock (Download_Queue)
            {
                Download_Queue.Clear();
            }
            lock (Download_List) // Clear all items and mark them as cancelled
            {
                foreach (var item in Download_List.Values)
                {
                    lock (item)
                    {
                        item.Status = Download_Status.Canceled;
                    }
                }
                Download_List.Clear();
            }
            Stop(); // Stop the manager itself
        }

        /// <summary>
        /// Represents an item in the download list with its status and downloaded data.
        /// </summary>
        private class DownloadItem
        {
            /// <summary>
            /// Gets or sets the status of the download item.
            /// </summary>
            public Download_Status Status { get; set; }

            private byte[]? _data;
            /// <summary>
            /// Gets or sets the downloaded data.
            /// </summary>
            public byte[]? Data
            {
                get { return this._data; }
                set { this._data = value; }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="DownloadItem"/> class.
            /// </summary>
            public DownloadItem()
            {
                this.Status = Download_Status.Queued;
                this.Data = default;
            }
        }

        private bool _disposed = false;

        /// <summary>
        /// Disposes the managed resources used by the <see cref="Download_LZMA_Data_Manager"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the managed and unmanaged resources used by the <see cref="Download_LZMA_Data_Manager"/>.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    _httpClient.Dispose();
                    _activeChunkSemaphore.Dispose();
                    // If BackgroundWorker instances are stored and need explicit disposal
                    // foreach (var worker in Workers_Live)
                    // {
                    //     worker.Dispose();
                    // }
                    Workers_Live.Clear();
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer
                // Set large fields to null
                Download_List = null;
                Download_Queue = null;

                _disposed = true;
            }
        }

        ~Download_LZMA_Data_Manager()
        {
            Dispose(false);
        }
    }
}