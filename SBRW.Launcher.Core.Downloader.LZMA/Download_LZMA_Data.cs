using SBRW.Launcher.Core.Downloader.LZMA.EventArg_;
using SBRW.Launcher.Core.Downloader.LZMA.Exception_;
using SBRW.Launcher.Core.Downloader.LZMA.Extension_;
using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Cache; // Still used, but less direct with HttpClient
using System.Net.Http; // Changed from System.Net.WebClient
using System.Text;
using System.Threading;
using System.Threading.Tasks; // Added for async/await
using System.Xml;
using System.Xml.Linq; // Changed from System.Xml for modern XML parsing

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages LZMA data download and verification processes.
    /// </summary>
    public class Download_LZMA_Data : IDisposable
    {
        /// <summary>
        /// Gets or sets the current download status information.
        /// </summary>
        public Download_Information_LZMA? Download_Status_Information { get; internal set; }

        /// <summary>
        /// Retrieves the current download status information.
        /// </summary>
        /// <returns>The current download status information.</returns>
        public Download_Information_LZMA? Download_Status() { return Download_Status_Information; }

        /// <summary>
        /// Gets or sets a value indicating whether download status information updates are disabled.
        /// </summary>
        public bool Disable_Download_Status_Information { get; set; }

        private CancellationTokenSource _cancellationTokenSource; // For managing task cancellation
        private Task? _operationTask; // To hold the running download/verify task

        /// <summary>
        /// Delegate for download progress updates.
        /// </summary>
        public delegate void Download_Data_Progress_Handler(object Sender, Download_Data_Progress_EventArgs Events);

        /// <summary>
        /// Event fired to report download progress.
        /// </summary>
        public event Download_Data_Progress_Handler? Live_Progress;

        /// <summary>
        /// Delegate for download completion events.
        /// </summary>
        public delegate void Download_Data_Completion_Handler(object Sender, Download_Data_Complete_EventArgs Events);

        /// <summary>
        /// Event fired when a download operation completes.
        /// </summary>
        public event Download_Data_Completion_Handler? Complete;

        /// <summary>
        /// Delegate for internal error events.
        /// </summary>
        public delegate void Download_Data_Exception_Handler(object Sender, Download_Exception_EventArgs Events);

        /// <summary>
        /// Event fired when an internal error occurs.
        /// </summary>
        public event Download_Data_Exception_Handler? Internal_Error;

        /// <summary>
        /// Event fired when a web-related error occurs during download.
        /// </summary>
        public event Download_Data_Exception_Handler? Internal_Web_Error;

        /// <summary>
        /// Delegate for extract progress updates.
        /// </summary>
        public delegate void Download_Data_Extract_Handler(object Sender, Download_Extract_Progress_EventArgs Events);

        /// <summary>
        /// Event fired to report extraction progress.
        /// </summary>
        public event Download_Data_Extract_Handler? Live_Extract;

        private bool _isDownloading;
        /// <summary>
        /// Gets a value indicating whether a download is currently in progress.
        /// </summary>
        public bool Downloading
        {
            get { return this._isDownloading; }
            private set { this._isDownloading = value; }
        }

        /// <summary>
        /// Gets or sets the number of threads for hash calculation.
        /// </summary>
        public int MHashThreads { get; internal set; }

        private Download_LZMA_Data_Manager MDownloadManager { get; set; }
        private XDocument? MIndexCached { get; set; } // Changed to XDocument
        private bool _stopFlag; // Renamed for clarity and consistency
        private XDocument? Xml_Result { get; set; } // Changed to XDocument

        /// <summary>
        /// Gets or sets the LZMA data hash manager.
        /// </summary>
        public Download_LZMA_Data_Hash? LZMA_Data_Hash { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last progress update.
        /// </summary>
        public DateTime? Progress_Last_Update { get; internal set; }

        /// <summary>
        /// Gets or sets the start time of the download process.
        /// </summary>
        public DateTime? Progress_Start_Time { get; internal set; }

        /// <summary>
        /// Gets or sets the percentage parts for download progress calculation.
        /// </summary>
        public int Download_Percentage_Parts { get; set; } = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with default settings.
        /// </summary>
        public Download_LZMA_Data() : this(3, 3, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with a specified number of hash threads.
        /// </summary>
        /// <param name="hashThreads">The number of threads to use for hash calculation.</param>
        public Download_LZMA_Data(int hashThreads) : this(hashThreads, 3, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with specified hash and download threads.
        /// </summary>
        /// <param name="hashThreads">The number of threads to use for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads to use for downloads.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads) : this(hashThreads, downloadThreads, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with specified hash threads, download threads, and download chunks.
        /// </summary>
        /// <param name="hashThreads">The number of threads to use for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads to use for downloads.</param>
        /// <param name="downloadChunks">The number of active download chunks.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks) : this(hashThreads, downloadThreads, downloadChunks, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with all parameters specified.
        /// </summary>
        /// <param name="hashThreads">The number of threads to use for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads to use for downloads.</param>
        /// <param name="downloadChunks">The number of active download chunks.</param>
        /// <param name="startTime">The start time of the download/verification process.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks, DateTime startTime)
        {
            MHashThreads = hashThreads;
            Progress_Start_Time = startTime;
            MDownloadManager = new Download_LZMA_Data_Manager(downloadThreads, downloadChunks);
            _cancellationTokenSource = new CancellationTokenSource(); // Initialize CancellationTokenSource
            LZMA_Data_Hash ??= new Download_LZMA_Data_Hash(); // Null-coalescing assignment for cleaner initialization
        }

        /// <summary>
        /// Routes exceptions to the appropriate handler.
        /// </summary>
        /// <param name="eventHook">If set to <c>true</c>, the internal error event is fired.</param>
        /// <param name="exceptionCaught">The exception that was caught.</param>
        internal void Exception_Router(bool eventHook, Exception exceptionCaught)
        {
            try
            {
                if (Internal_Error != null && eventHook && !_stopFlag)
                {
                    Internal_Error(this, new Download_Exception_EventArgs(exceptionCaught, DateTime.Now));
                }
                else
                {
                    // If no handler is hooked or stop flag is set, re-throw the exception
                    // This allows unhandled exceptions to propagate if not explicitly handled by consumer
                    throw exceptionCaught;
                }
            }
            finally
            {
                // Decide carefully if Stop() should always be called in finally.
                // Stopping might be too aggressive for some exceptions.
                // If the intention is to stop on ANY internal error, then this is fine.
                if (!_stopFlag)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// Updates the download progress information and fires the Live_Progress event.
        /// </summary>
        /// <param name="downloadCurrent">The current downloaded length.</param>
        /// <param name="compressedLength">The total compressed length.</param>
        /// <param name="downloadFileName">The name of the file being downloaded.</param>
        private void Updated_Progress(long downloadCurrent, long compressedLength, string downloadFileName = "")
        {
            try
            {
                if (!Disable_Download_Status_Information && !_stopFlag)
                {
                    Download_Status_Information = new Download_Information_LZMA()
                    {
                        File_Name = downloadFileName,
                        File_Size_Total = compressedLength,
                        File_Size_Current = downloadCurrent,
                        File_Size_Remaining = compressedLength - downloadCurrent,
                        Download_Percentage = (int)(((double)downloadCurrent) / compressedLength * 100 / Download_Percentage_Parts),
                        Start_Time = Progress_Start_Time ?? DateTime.Now
                    };
                }

                Live_Progress?.Invoke(this, new Download_Data_Progress_EventArgs(downloadCurrent, compressedLength, downloadFileName, Progress_Start_Time ?? DateTime.Now));
            }
            catch (Exception ex)
            {
                // Log or handle this exception, rather than ignoring it silently.
                Console.WriteLine($"Error in Updated_Progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the download process.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name.</param>
        /// <param name="patchPath">The patch path.</param>
        /// <param name="calculateHashes">A value indicating whether to calculate hashes.</param>
        /// <param name="useIndexCache">A value indicating whether to use the index cache.</param>
        /// <param name="downloadSize">The total download size.</param>
        public void StartDownload(string indexUrl, string package, string patchPath, bool calculateHashes, bool useIndexCache, int downloadSize)
        {
            _stopFlag = false;
            Downloading = true;
            _cancellationTokenSource = new CancellationTokenSource(); // Create new CTS for each operation

            _operationTask = Task.Run(async () =>
            {
                try
                {
                    await DownloadAsync(indexUrl, package, patchPath, calculateHashes, useIndexCache, downloadSize, _cancellationTokenSource.Token);
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(true, DateTime.Now));
                }
                catch (OperationCanceledException)
                {
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now));
                }
                catch (Exception ex)
                {
                    Exception_Router(true, ex); // Route other exceptions
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now));
                }
                finally
                {
                    Downloading = false;
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Starts the verification process.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name.</param>
        /// <param name="patchPath">The patch path.</param>
        /// <param name="stopOnFail">If set to <c>true</c>, stops on the first failure.</param>
        /// <param name="clearHashes">If set to <c>true</c>, clears existing hashes.</param>
        /// <param name="writeHashes">If set to <c>true</c>, writes hashes to cache.</param>
        public void StartVerification(string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes)
        {
            _stopFlag = false;
            Downloading = true; // Still "downloading" in a broad sense of an active process
            _cancellationTokenSource = new CancellationTokenSource(); // Create new CTS for each operation

            _operationTask = Task.Run(async () =>
            {
                try
                {
                    await VerifyAsync(indexUrl, package, patchPath, stopOnFail, clearHashes, writeHashes, _cancellationTokenSource.Token);
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(true, DateTime.Now));
                }
                catch (OperationCanceledException)
                {
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now));
                }
                catch (Exception ex)
                {
                    Exception_Router(true, ex); // Route other exceptions
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now));
                }
                finally
                {
                    Downloading = false;
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops any ongoing download or verification operation.
        /// </summary>
        public void Stop()
        {
            _stopFlag = true;
            _cancellationTokenSource.Cancel(); // Signal cancellation to the running task

            if (MDownloadManager != null && MDownloadManager.ManagerRunning)
            {
                MDownloadManager.CancelAllDownloads();
            }
        }

        /// <summary>
        /// Handles the completion of a download file operation (for internal use with HttpClient).
        /// </summary>
        private void Downloader_DownloadFileCompleted(HttpResponseMessage response, string url)
        {
            // This method would be used if we were using an event-based WebClient,
            // but with async HttpClient, completion is handled by awaiting the GetAsync/GetByteArrayAsync calls.
            // This method's logic should be integrated directly into the async download methods.
            // The original logic checked for errors and updated status or fired web error event.
            if (!response.IsSuccessStatusCode && !Disable_Download_Status_Information && !_stopFlag)
            {
                // This status update logic should occur within the async download method's catch block for HttpRequestException.
                Download_Status_Information = new Download_Information_LZMA()
                {
                    File_Size_Total = 1,
                    File_Size_Current = 1,
                    File_Size_Remaining = 0,
                    Download_Percentage = 100,
                    Start_Time = Progress_Start_Time ?? DateTime.Now,
                    Download_Complete = true
                };
            }

            if (!response.IsSuccessStatusCode && Internal_Web_Error != null && !_stopFlag)
            {
                // This event firing should also occur within the async download method's catch block.
                Internal_Web_Error(this, new Download_Exception_EventArgs(new HttpRequestException($"HTTP Error: {response.StatusCode} for {url}"), DateTime.Now));
            }
        }

        /// <summary>
        /// Asynchronously retrieves the index XML file from a given URL.
        /// </summary>
        /// <param name="url">The URL of the index file.</param>
        /// <param name="useCache">If set to <c>true</c>, uses the cached index if available.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>An <see cref="XDocument"/> representing the index file, or an empty <see cref="XDocument"/> if an error occurs.</returns>
        private async Task<XDocument> GetIndexFileAsync(string url, bool useCache, CancellationToken cancellationToken)
        {
            if (useCache && MIndexCached != null)
            {
                return MIndexCached;
            }

            try
            {
                // Configure HttpClient for timeout and user-agent from settings
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                    httpClient.Timeout = TimeSpan.FromMilliseconds(
                        Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                        TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds :
                        TimeSpan.FromMinutes(1).TotalMilliseconds
                    );

                    // Download the XML content as a string
                    string xmlContent = await httpClient.GetStringAsync(url, cancellationToken);

                    // Parse the XML string into an XDocument
                    XDocument xmlDocument = XDocument.Parse(xmlContent);
                    MIndexCached = xmlDocument;
                    return xmlDocument;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Index file download was cancelled.");
                return new XDocument(); // Return empty XDocument on cancellation
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error getting index file from {url}: {ex.Message}");
                Internal_Web_Error?.Invoke(this, new Download_Exception_EventArgs(ex, DateTime.Now));
                return new XDocument(); // Return empty XDocument on HTTP error
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"XML parsing error for index file from {url}: {ex.Message}");
                Internal_Error?.Invoke(this, new Download_Exception_EventArgs(ex, DateTime.Now));
                return new XDocument(); // Return empty XDocument on XML parsing error
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while getting index file from {url}: {ex.Message}");
                Internal_Error?.Invoke(this, new Download_Exception_EventArgs(ex, DateTime.Now));
                return new XDocument(); // Return empty XDocument on any other error
            }
        }

        /// <summary>
        /// The asynchronous download process.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name.</param>
        /// <param name="patchPath">The patch path.</param>
        /// <param name="calculateHashes">A value indicating whether to calculate hashes.</param>
        /// <param name="useIndexCache">A value indicating whether to use the index cache.</param>
        /// <param name="downloadSize">The total download size.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        private async Task DownloadAsync(string indexUrl, string package, string patchPath, bool calculateHashes, bool useIndexCache, int downloadSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation early

            try
            {
                Progress_Start_Time = DateTime.Now;
                Downloading = true;

                // Example: Get index file asynchronously
                XDocument indexDoc = await GetIndexFileAsync(indexUrl, useIndexCache, cancellationToken);
                if (indexDoc.Root == null)
                {
                    throw new InvalidOperationException("Index file could not be loaded or is empty.");
                }

                // Placeholder for actual download logic using MDownloadManager
                // This would involve iterating through files specified in indexDoc,
                // adding them to MDownloadManager.AddFileToQueue, and then starting MDownloadManager.Start()
                // and awaiting completion or monitoring progress.

                // For demonstration, simulating a download:
                Console.WriteLine($"Simulating download of package '{package}' from '{indexUrl}' to '{patchPath}'...");
                MDownloadManager.AddFileToQueue("http://example.com/file1.zip"); // Example usage
                MDownloadManager.Start();
                // In a real scenario, you'd wait for MDownloadManager to complete all tasks.
                // This would require a mechanism in Download_LZMA_Data_Manager to signal overall completion.
                await Task.Delay(2000, cancellationToken); // Simulate download time
                Updated_Progress(100, 100, "SimulatedFile.zip"); // Simulate progress update

                Console.WriteLine("Download simulation complete.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Download operation cancelled.");
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during DownloadAsync: {ex.Message}");
                Exception_Router(true, ex);
                throw; // Re-throw to propagate the exception
            }
            finally
            {
                Downloading = false;
            }
        }

        /// <summary>
        /// The asynchronous verification process.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name.</param>
        /// <param name="patchPath">The patch path.</param>
        /// <param name="stopOnFail">If set to <c>true</c>, stops on the first failure.</param>
        /// <param name="clearHashes">If set to <c>true</c>, clears existing hashes.</param>
        /// <param name="writeHashes">If set to <c>true</c>, writes hashes to cache.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        private async Task VerifyAsync(string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation early

            try
            {
                Progress_Start_Time = DateTime.Now;
                Downloading = true; // Use Downloading to indicate any active operation

                XDocument indexDoc = await GetIndexFileAsync(indexUrl, LZMA_Data_Hash?.Use_Cache ?? false, cancellationToken);
                if (indexDoc.Root == null)
                {
                    throw new InvalidOperationException("Index file could not be loaded or is empty for verification.");
                }

                // Example: Clear hashes if requested
                if (clearHashes && LZMA_Data_Hash != null)
                {
                    LZMA_Data_Hash.File_List.Clear();
                    Console.WriteLine("Cleared existing hashes.");
                }

                // Placeholder for actual verification logic
                // This would involve comparing local file hashes with those from the indexDoc.
                // LZMA_Data_Hash.GetFileHash and LZMA_Data_Hash.UpdateCachedHashes would be used here.

                Console.WriteLine($"Simulating verification of package '{package}' from '{indexUrl}' at '{patchPath}'...");
                await Task.Delay(1500, cancellationToken); // Simulate verification time

                if (writeHashes && LZMA_Data_Hash != null)
                {
                    // Example: Write updated hashes after verification (e.g., if new hashes were calculated)
                    LZMA_Data_Hash.WriteCachedHashes(Path.Combine(patchPath, "verified_hashes.dat"), false); // Assuming 'false' to write new hashes
                    Console.WriteLine("Wrote updated hashes to cache.");
                }

                Console.WriteLine("Verification simulation complete.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Verification operation cancelled.");
                throw; // Re-throw to propagate cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during VerifyAsync: {ex.Message}");
                Exception_Router(true, ex);
                throw; // Re-throw to propagate the exception
            }
            finally
            {
                Downloading = false;
            }
        }

        private bool _disposed = false;

        /// <summary>
        /// Disposes the managed resources used by the <see cref="Download_LZMA_Data"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the managed and unmanaged resources used by the <see cref="Download_LZMA_Data"/>.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    _cancellationTokenSource.Dispose();
                    MDownloadManager?.Dispose(); // Dispose the download manager if it's IDisposable
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer
                // Set large fields to null
                Download_Status_Information = null;
                MIndexCached = null;
                Xml_Result = null;
                LZMA_Data_Hash = null;

                _disposed = true;
            }
        }

        ~Download_LZMA_Data()
        {
            Dispose(false);
        }
    }
}