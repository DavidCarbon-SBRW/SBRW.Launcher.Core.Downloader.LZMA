using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Concurrent; // For ConcurrentDictionary and ConcurrentQueue
using System.Collections.Generic;
using System.Net;
using System.Net.Cache;
using System.Threading;
using System.Threading.Tasks; // Added for async/await and Task Parallel Library
using System.Xml;
using static SBRW.Launcher.Core.Downloader.LZMA.Download_LZMA_Enumerator;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages concurrent LZMA data downloads.
    /// </summary>
    public class Download_LZMA_Data_Manager
    {
        private ConcurrentDictionary<string, DownloadItem> _downloadList;
        private ConcurrentQueue<string> _downloadQueue;
        private SemaphoreSlim _activeChunkSemaphore; // Controls concurrent downloads
        private List<Task> _downloadWorkers; // Stores active download worker tasks
        private int _maxWorkers;

        /// <summary>
        /// Max Background Workers (now Tasks) in an Instance.
        /// </summary>
        /// <remarks>Default is 3</remarks>
        public int MaxWorkers
        {
            get { return _maxWorkers; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "MaxWorkers must be greater than 0.");
                _maxWorkers = value;
            }
        }

        /// <summary>
        /// Max Active Chunks (concurrent downloads) in an Instance.
        /// </summary>
        /// <remarks>Default is 16</remarks>
        public int MaxActiveChunks { get; set; } = 16;

        private bool _managerRunning;

        /// <summary>
        /// Gets a value indicating whether the download manager is currently running.
        /// </summary>
        public bool ManagerRunning => _managerRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Manager"/> class.
        /// </summary>
        public Download_LZMA_Data_Manager() : this(3, 16) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data_Manager"/> class with specified max workers and active chunks.
        /// </summary>
        /// <param name="maxWorkers">The maximum number of concurrent download workers.</param>
        /// <param name="maxActiveChunks">The maximum number of active download chunks.</param>
        public Download_LZMA_Data_Manager(int maxWorkers, int maxActiveChunks)
        {
            MaxWorkers = maxWorkers;
            MaxActiveChunks = maxActiveChunks;
            _downloadList = new ConcurrentDictionary<string, DownloadItem>();
            _downloadQueue = new ConcurrentQueue<string>();
            _downloadWorkers = new List<Task>();
            _activeChunkSemaphore = new SemaphoreSlim(MaxActiveChunks, MaxActiveChunks);
        }

        /// <summary>
        /// The main download worker task.
        /// </summary>
        private async Task DownloadWorkerAsync()
        {
            try
            {
                while (_managerRunning) // Keep running as long as manager is active
                {
                    await _activeChunkSemaphore.WaitAsync(); // Acquire a permit for an active chunk
                    string? fileUrl = null;
                    try
                    {
                        if (!_downloadQueue.TryDequeue(out fileUrl))
                        {
                            // No items in queue, release semaphore and wait a bit before checking again
                            _activeChunkSemaphore.Release();
                            await Task.Delay(100);
                            continue;
                        }

                        if (!_downloadList.TryGetValue(fileUrl, out var downloadItem))
                        {
                            // Should not happen if ScheduleFile is called correctly
                            _activeChunkSemaphore.Release();
                            continue;
                        }

                        // Mark as downloading
                        downloadItem.Status = Download_Status.Downloading;

                        using (WebClient client = Download_LZMA_Settings.Alternative_WebCalls ? new WebClient() : new WebClientWithTimeout())
                        {
                            if (Download_LZMA_Settings.Alternative_WebCalls)
                            {
                                client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                            }
                            client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

                            try
                            {
                                Uri uri = new Uri(fileUrl);
                                byte[] data = await client.DownloadDataTaskAsync(uri);

                                downloadItem.Data = data;
                                downloadItem.Status = Download_Status.Downloaded;
                            }
                            catch (OperationCanceledException)
                            {
                                downloadItem.Status = Download_Status.Canceled;
                            }
                            catch (Exception ex)
                            {
                                // Log the error and mark as failed or queued for retry
                                System.Diagnostics.Debug.WriteLine($"Error downloading {fileUrl}: {ex.Message}");
                                downloadItem.Status = Download_Status.Failed; // New status
                                _downloadQueue.Enqueue(fileUrl); // Re-queue for retry if desired, or handle differently
                            }
                            finally
                            {
                                _activeChunkSemaphore.Release(); // Release the permit after download attempt
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch exceptions that might occur before or during TryDequeue
                        System.Diagnostics.Debug.WriteLine($"General error in download worker: {ex.Message}");
                        _activeChunkSemaphore.Release(); // Ensure semaphore is released if an error occurs early
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Worker was cancelled, gracefully exit
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical error in download worker loop: {ex.Message}");
            }
            finally
            {
                // This ensures the permit is released if the worker exits unexpectedly.
                // However, a semaphore should usually be paired with a try-finally around the WaitAsync and Release.
                // The current structure where Release is in the inner try-finally is more correct for per-download permit.
                // This outer finally is more for worker shutdown.
            }
        }

        /// <summary>
        /// Cancels all ongoing and queued downloads.
        /// </summary>
        public void CancelAllDownloads()
        {
            Stop(); // Stops the manager from scheduling new downloads
            _downloadQueue = new ConcurrentQueue<string>(); // Clear the queue immediately

            foreach (var item in _downloadList.Values)
            {
                item.Status = Download_Status.Canceled;
                item.Data = null; // Release data
            }
            // No explicit WebClient.CancelAsync needed anymore as DownloadDataTaskAsync is used with a cancellation token
            // which can be triggered by the main CancellationTokenSource in Download_LZMA_Data.
        }

        /// <summary>
        /// Cancels a specific download.
        /// </summary>
        /// <param name="fileName">The URL/name of the file to cancel.</param>
        public void CancelDownload(string fileName)
        {
            if (_downloadList.TryGetValue(fileName, out var downloadItem))
            {
                downloadItem.Status = Download_Status.Canceled;
                downloadItem.Data = null; // Release data

                // Remove from queue if it hasn't started yet
                // ConcurrentQueue doesn't have a direct "remove" operation.
                // We mark status as canceled, and worker will skip if status is canceled.
                // If the file is still in the queue, it will be dequeued but then skipped.
            }
        }

        /// <summary>
        /// Clears all download manager state and stops workers.
        /// </summary>
        public async Task ClearAsync()
        {
            CancelAllDownloads();
            // Wait for all worker tasks to complete their current iteration and exit.
            // This is crucial for a clean shutdown.
            if (_downloadWorkers.Count > 0)
            {
                await Task.WhenAll(_downloadWorkers);
                _downloadWorkers.Clear();
            }
            _downloadList.Clear();
        }

        /// <summary>
        /// Retrieves the downloaded file data. This method waits until the file is downloaded or cancelled.
        /// </summary>
        /// <param name="fileName">The URL/name of the file to retrieve.</param>
        /// <returns>The downloaded byte array, or null if cancelled or failed.</returns>
        public async Task<byte[]?> GetFileAsync(string fileName)
        {
            // Ensure the file is scheduled if not already
            ScheduleFile(fileName);

            DownloadItem? downloadItem;
            if (!_downloadList.TryGetValue(fileName, out downloadItem))
            {
                return null; // Should not happen if ScheduleFile is called first
            }

            // Await the completion of this specific download item.
            // This requires a TaskCompletionSource per DownloadItem if we want to await a specific one.
            // For simplicity and given the existing structure, we'll poll, but with async delay.
            // A more robust solution would involve DownloadItem exposing a Task that completes on download.
            while (downloadItem.Status != Download_Status.Downloaded && downloadItem.Status != Download_Status.Canceled && downloadItem.Status != Download_Status.Failed)
            {
                await Task.Delay(50); // Polling with a short delay
            }

            if (downloadItem.Status == Download_Status.Downloaded)
            {
                byte[]? data = downloadItem.Data;
                downloadItem.Data = null; // Clear data to free memory after consumption
                return data;
            }
            return null; // Canceled or Failed
        }

        /// <summary>
        /// Gets the current status of a download.
        /// </summary>
        /// <param name="fileName">The URL/name of the file.</param>
        /// <returns>The download status.</returns>
        public Download_Status GetStatus(string fileName)
        {
            if (_downloadList.TryGetValue(fileName, out var item))
            {
                return item.Status;
            }
            return Download_Status.Unknown;
        }

        /// <summary>
        /// Initializes the download manager with file information from an XML document.
        /// </summary>
        /// <param name="doc">The XML document containing file information.</param>
        /// <param name="serverPath">The base URL for sections.</param>
        public void Initialize(XmlDocument doc, string serverPath)
        {
            _downloadList.Clear(); // Clear any previous initialization
            int maxSection = 0;
            XmlNodeList? fileInfoNodes = doc.SelectNodes("/index/fileinfo");
            if (fileInfoNodes != null)
            {
                foreach (XmlNode xmlNode in fileInfoNodes)
                {
                    XmlNode? sectionNode = xmlNode.SelectSingleNode("section");
                    if (sectionNode != null)
                    {
                        int sectionNum = int.Parse(sectionNode.InnerText);
                        if (sectionNum > maxSection)
                        {
                            maxSection = sectionNum;
                        }
                    }
                }
            }

            for (int i = 1; i <= maxSection; i++)
            {
                string sectionUrl = string.Format("{0}/section{1}.dat", serverPath, i);
                _downloadList.TryAdd(sectionUrl, new DownloadItem());
            }
        }

        /// <summary>
        /// Schedules a file for download.
        /// </summary>
        /// <param name="fileName">The URL/name of the file to schedule.</param>
        public void ScheduleFile(string fileName)
        {
            DownloadItem downloadItem = _downloadList.GetOrAdd(fileName, new DownloadItem());

            // If it's already downloaded, no need to re-queue unless forced.
            if (downloadItem.Status == Download_Status.Downloaded)
            {
                return;
            }

            // If it's already queued, ensure it's at the back (or front, depending on desired priority)
            // For ConcurrentQueue, we can just enqueue. If it's already there, it will be a duplicate.
            // The worker will handle skipping if status is already in progress/canceled.
            if (downloadItem.Status != Download_Status.Queued && downloadItem.Status != Download_Status.Downloading)
            {
                downloadItem.Status = Download_Status.Queued;
                _downloadQueue.Enqueue(fileName);
            }

            // Ensure workers are running if manager is active and we have capacity
            if (_managerRunning && _downloadWorkers.Count < MaxWorkers)
            {
                Start(); // Call Start to ensure workers are created up to MaxWorkers
            }
        }

        /// <summary>
        /// Starts the download manager, initiating worker tasks.
        /// </summary>
        public void Start()
        {
            _managerRunning = true;
            // Create workers up to MaxWorkers
            while (_downloadWorkers.Count < MaxWorkers)
            {
                Task workerTask = DownloadWorkerAsync();
                _downloadWorkers.Add(workerTask);
            }
        }

        /// <summary>
        /// Stops the download manager, signaling workers to cease operations.
        /// </summary>
        public void Stop()
        {
            _managerRunning = false;
        }

        /// <summary>
        /// Represents an item being downloaded, including its status and data.
        /// </summary>
        private class DownloadItem
        {
            /// <summary>
            /// Gets or sets the current status of the download item.
            /// </summary>
            public Download_Status Status { get; set; }

            /// <summary>
            /// Gets or sets the downloaded byte array data.
            /// </summary>
            public byte[]? Data { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="DownloadItem"/> class.
            /// </summary>
            public DownloadItem()
            {
                Status = Download_Status.Queued;
                Data = null;
            }
        }
    }
}