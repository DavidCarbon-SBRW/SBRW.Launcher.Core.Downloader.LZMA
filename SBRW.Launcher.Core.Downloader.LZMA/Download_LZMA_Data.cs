using SBRW.Launcher.Core.Downloader.LZMA.Exception_;
using SBRW.Launcher.Core.Downloader.LZMA.EventArg_;
using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Threading.Tasks; // Added for async/await
using System.Xml;
using SBRW.Launcher.Core.Downloader.LZMA.Extension_; // Assuming this contains Download_LZMA

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Manages LZMA data downloads and verification.
    /// </summary>
    public class Download_LZMA_Data
    {
        private const int LzmaPropsSize = 5;
        private const int LzmaHeaderSize = 13; // 5 bytes for props + 8 bytes for uncompressed size

        /// <summary>
        /// Gets the current download status information.
        /// </summary>
        public Download_Information_LZMA? DownloadStatusInformation { get; private set; }

        /// <summary>
        /// Gets the current download status information.
        /// </summary>
        public Download_Information_LZMA? GetDownloadStatus() { return DownloadStatusInformation; }

        /// <summary>
        /// Gets or sets a value indicating whether to disable download status information updates.
        /// </summary>
        public bool DisableDownloadStatusInformation { get; set; }

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _operationTask;

        /// <summary>
        /// Event for live download progress updates.
        /// </summary>
        public event Download_Data_Progress_Handler? LiveProgress;

        /// <summary>
        /// Event for download completion.
        /// </summary>
        public event Download_Data_Completion_Handler? Complete;

        /// <summary>
        /// Event for internal errors during download or processing.
        /// </summary>
        public event Download_Data_Exception_Handler? InternalError;

        /// <summary>
        /// Event for web-related errors during download.
        /// </summary>
        public event Download_Data_Exception_Handler? WebError;

        /// <summary>
        /// Event for live extraction progress updates.
        /// </summary>
        public event Download_Data_Extract_Handler? LiveExtract;

        private bool _isDownloadingOrVerifying;

        /// <summary>
        /// Gets the number of hash calculation threads.
        /// </summary>
        public int HashThreads { get; internal set; }

        private Download_LZMA_Data_Manager _downloadManager;
        private XmlDocument? _indexCached;
        private XmlDocument? _xmlResult; // Used to store the result of GetIndexFile internally before returning.

        /// <summary>
        /// Gets or sets the LZMA data hash manager.
        /// </summary>
        public Download_LZMA_Data_Hash LZMADataHash { get; set; }

        /// <summary>
        /// Gets the last time the progress was updated.
        /// </summary>
        public DateTime? ProgressLastUpdate { get; internal set; }

        /// <summary>
        /// Gets the start time of the current operation.
        /// </summary>
        public DateTime? ProgressStartTime { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether an operation (download or verification) is currently in progress.
        /// </summary>
        public bool IsBusy => _isDownloadingOrVerifying;

        /// <summary>
        /// Gets or sets the total parts for download percentage calculation.
        /// </summary>
        public int DownloadPercentageParts { get; set; } = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class.
        /// </summary>
        public Download_LZMA_Data() : this(3, 3, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with specified hash threads.
        /// </summary>
        /// <param name="hashThreads">The number of threads for hash calculation.</param>
        public Download_LZMA_Data(int hashThreads) : this(hashThreads, 3, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with specified hash and download threads.
        /// </summary>
        /// <param name="hashThreads">The number of threads for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads for downloading.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads) : this(hashThreads, downloadThreads, 16, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with specified hash, download threads, and download chunks.
        /// </summary>
        /// <param name="hashThreads">The number of threads for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads for downloading.</param>
        /// <param name="downloadChunks">The number of download chunks.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks) : this(hashThreads, downloadThreads, downloadChunks, DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Data"/> class with all parameters.
        /// </summary>
        /// <param name="hashThreads">The number of threads for hash calculation.</param>
        /// <param name="downloadThreads">The number of threads for downloading.</param>
        /// <param name="downloadChunks">The number of download chunks.</param>
        /// <param name="startTime">The start time of the operation.</param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks, DateTime startTime)
        {
            HashThreads = hashThreads;
            ProgressStartTime = startTime;
            _downloadManager = new Download_LZMA_Data_Manager(downloadThreads, downloadChunks);
            LZMADataHash = new Download_LZMA_Data_Hash(); // Initialized once here
        }

        /// <summary>
        /// Routes exceptions to the appropriate event handler.
        /// </summary>
        /// <param name="eventHook">True to trigger the event, false to rethrow.</param>
        /// <param name="exceptionCaught">The exception that was caught.</param>
        private void ExceptionRouter(bool eventHook, Exception exceptionCaught)
        {
            if (eventHook && InternalError != null && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                InternalError(this, new Download_Exception_EventArgs(exceptionCaught, DateTime.Now));
            }
            else
            {
                throw exceptionCaught;
            }

            // Always stop the operation if an exception occurs and it's not already cancelled
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                Stop();
            }
        }

        /// <summary>
        /// Updates the download progress information and raises the LiveProgress event.
        /// </summary>
        /// <param name="downloadCurrent">The current downloaded size.</param>
        /// <param name="compressedLength">The total compressed length.</param>
        /// <param name="downloadFileName">The name of the file being downloaded.</param>
        private void UpdateProgress(long downloadCurrent, long compressedLength, string downloadFileName = "")
        {
            try
            {
                if (!DisableDownloadStatusInformation && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DownloadStatusInformation = new Download_Information_LZMA()
                    {
                        File_Name = downloadFileName,
                        File_Size_Total = compressedLength,
                        File_Size_Current = downloadCurrent,
                        File_Size_Remaining = compressedLength - downloadCurrent,
                        Download_Percentage = (int)(((double)downloadCurrent) / compressedLength * 100 / DownloadPercentageParts),
                        Start_Time = ProgressStartTime ?? DateTime.Now
                    };
                }

                LiveProgress?.Invoke(this, new Download_Data_Progress_EventArgs(downloadCurrent, compressedLength, downloadFileName, ProgressStartTime ?? DateTime.Now));
            }
            catch (Exception)
            {
                /* Ignore Exception */
            }
        }

        /// <summary>
        /// Starts the download operation asynchronously.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name (e.g., a subfolder).</param>
        /// <param name="patchPath">The local path to store patched files.</param>
        /// <param name="calculateHashes">True to calculate hashes during download.</param>
        /// <param name="useIndexCache">True to use cached index file if available.</param>
        /// <param name="downloadSize">The total download size if known, otherwise 0.</param>
        public void StartDownload(string indexUrl, string package, string patchPath, bool calculateHashes, bool useIndexCache, ulong downloadSize)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _isDownloadingOrVerifying = true;
            ProgressStartTime = DateTime.Now; // Set start time when operation begins

            _operationTask = Task.Run(() => DownloadAsync(indexUrl, package, patchPath, calculateHashes, useIndexCache, downloadSize, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Starts the verification operation asynchronously.
        /// </summary>
        /// <param name="indexUrl">The URL of the index file.</param>
        /// <param name="package">The package name (e.g., a subfolder).</param>
        /// <param name="patchPath">The local path to store patched files.</param>
        /// <param name="stopOnFail">True to stop verification on the first failed hash check.</param>
        /// <param name="clearHashes">True to clear existing hashes before verification.</param>
        /// <param name="writeHashes">True to write updated hash cache after verification.</param>
        public void StartVerification(string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _isDownloadingOrVerifying = true;
            ProgressStartTime = DateTime.Now; // Set start time when operation begins

            _operationTask = Task.Run(() => VerifyAsync(indexUrl, package, patchPath, stopOnFail, clearHashes, writeHashes, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops any ongoing download or verification operation.
        /// </summary>
        public void Stop()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_downloadManager.ManagerRunning)
            {
                _downloadManager.CancelAllDownloads();
            }

            _isDownloadingOrVerifying = false;
        }

        private void Downloader_DownloadFileCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            // This event handler is for the internal WebClient used in GetIndexFile, not the main download manager.
            if (e.Error != null)
            {
                // This error is handled by the try-catch block in GetIndexFile.
                // However, if we need to expose this specific web error for the index file,
                // we can raise an event here.
                if (WebError != null && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    WebError(this, new Download_Exception_EventArgs(e.Error, DateTime.Now));
                }

                if (!DisableDownloadStatusInformation && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    DownloadStatusInformation = new Download_Information_LZMA()
                    {
                        File_Size_Total = 1, // Indicate an error state
                        File_Size_Current = 0,
                        File_Size_Remaining = 1,
                        Download_Percentage = 0,
                        Start_Time = ProgressStartTime ?? DateTime.Now,
                        Download_Complete = false // It's not complete on error
                    };
                }
            }
        }

        /// <summary>
        /// Retrieves the index XML file from the specified URL.
        /// </summary>
        /// <param name="url">The URL of the index file.</param>
        /// <param name="useCache">True to use the cached index file if available.</param>
        /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
        /// <returns>The XML document of the index file, or null if an error occurs or cancelled.</returns>
        private async Task<XmlDocument?> GetIndexFileAsync(string url, bool useCache, CancellationToken cancellationToken)
        {
            if (useCache && _indexCached != null)
            {
                return _indexCached;
            }

            try
            {
                Uri urlCall = new Uri(url);

                // ServicePointManager settings should be done once or carefully.
                // Setting ConnectionLeaseTimeout per call can be problematic in high-concurrency.
                // This is a global setting.
                ServicePointManager.FindServicePoint(urlCall).ConnectionLeaseTimeout = (int)(Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                    TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds : TimeSpan.FromMinutes(1).TotalMilliseconds);

                using (WebClient client = Download_LZMA_Settings.Alternative_WebCalls ? new WebClient() : new WebClientWithTimeout())
                {
                    if (Download_LZMA_Settings.Alternative_WebCalls)
                    {
                        client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                    }
                    client.DownloadDataCompleted += Downloader_DownloadFileCompleted; // This is for async non-blocking.

                    string tempFileName = Path.GetTempFileName();

                    try
                    {
                        // Await the asynchronous download
                        await client.DownloadFileTaskAsync(urlCall, tempFileName);

                        cancellationToken.ThrowIfCancellationRequested();

                        XmlDocument xmlDocument = new XmlDocument();
                        xmlDocument.Load(tempFileName);
                        _indexCached = xmlDocument;
                        _xmlResult = xmlDocument;
                    }
                    catch (OperationCanceledException)
                    {
                        _xmlResult = null;
                        // Propagate cancellation
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the exception specifically for index file download/load
                        ExceptionRouter(false, new Download_LZMA_Exception($"Failed to get or load index file from {url}. Error: {ex.Message}", ex));
                        _xmlResult = null;
                    }
                    finally
                    {
                        // Clean up the temporary file
                        if (File.Exists(tempFileName))
                        {
                            File.Delete(tempFileName);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _xmlResult = null;
                // Propagate cancellation
                throw;
            }
            catch (Exception ex)
            {
                ExceptionRouter(false, new Download_LZMA_Exception($"An error occurred while preparing to get index file from {url}. Error: {ex.Message}", ex));
                _xmlResult = null;
            }

            return _xmlResult; // Will return null on error
        }

        /// <summary>
        /// Performs the actual download operation.
        /// </summary>
        /// <param name="parameters">An array of parameters for the download.</param>
        /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
        private async Task DownloadAsync(string indexUrl, string package, string patchPath, bool calculateHashes, bool useIndexCache, ulong downloadSize, CancellationToken cancellationToken)
        {
            _isDownloadingOrVerifying = true;
            string fullIndexUrl = indexUrl;
            if (!string.IsNullOrWhiteSpace(package))
            {
                fullIndexUrl += "/" + package;
            }

            try
            {
                XmlDocument? indexFile = await GetIndexFileAsync(fullIndexUrl + "/index.xml", useIndexCache, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (indexFile == null)
                {
                    ExceptionRouter(true, new ArgumentNullException(nameof(indexFile), "Index File cannot be Null."));
                    return;
                }

                long headerLength = long.Parse(indexFile.SelectSingleNode("/index/header/length")?.InnerText ?? "0");
                long subIndexHashLength = 0L;
                long headerLengthCompressed = (downloadSize == 0uL) ?
                                              long.Parse(indexFile.SelectSingleNode("/index/header/compressed")?.InnerText ?? "0") :
                                              (long)downloadSize;

                long currentDownloadedBytes = 0L; // Represents total bytes downloaded so far

                // Headers added to WebClient here are for the main download manager.
                // The Download_LZMA_Data_Manager will handle its own WebClient instances and headers.
                // So this part can be removed or moved to Download_LZMA_Data_Manager initialization.

                // This part initializes Download_LZMA_Data_Manager based on index file info.
                // Initialize the download manager once at the beginning
                _downloadManager.Initialize(indexFile, fullIndexUrl);

                if (calculateHashes)
                {
                    LZMADataHash.Clear();
                    LZMADataHash.Start(indexFile, patchPath, package + ".hsh", HashThreads);
                }

                XmlNodeList? fileInfoNodes = indexFile.SelectNodes("/index/fileinfo");
                if (fileInfoNodes == null)
                {
                    ExceptionRouter(true, new InvalidOperationException("No file information found in the index file."));
                    return;
                }

                List<string> sectionsToDownload = new List<string>();
                int currentSectionIndex = 0; // Tracks the highest section number encountered or processed
                bool fileNeedsDownload = false; // Flag to indicate if a file was found that needs download/verification

                foreach (XmlNode xmlNode in fileInfoNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string originalPath = xmlNode.SelectSingleNode("path")?.InnerText ?? string.Empty;
                    string fileName = xmlNode.SelectSingleNode("file")?.InnerText ?? string.Empty;
                    string targetFilePath = GetTargetFilePath(originalPath, patchPath, fileName);
                    int sectionNumber = int.Parse(xmlNode.SelectSingleNode("section")?.InnerText ?? "0");
                    int offset = int.Parse(xmlNode.SelectSingleNode("offset")?.InnerText ?? "0");

                    // Check if hash matches, if not, mark for download
                    if (calculateHashes && !LZMADataHash.HashesMatch(targetFilePath))
                    {
                        fileNeedsDownload = true;
                    }
                    else if (!calculateHashes && xmlNode.SelectSingleNode("hash") != null && !LZMADataHash.HashesMatch(targetFilePath))
                    {
                        // If not calculating hashes, but hash exists in XML and doesn't match, also download
                        fileNeedsDownload = true;
                    }

                    if (fileNeedsDownload)
                    {
                        // Schedule all sections from currentSectionIndex up to the section of the current file
                        for (int i = currentSectionIndex; i <= sectionNumber; i++)
                        {
                            string sectionUrl = string.Format("{0}/section{1}.dat", fullIndexUrl, i);
                            if (!sectionsToDownload.Contains(sectionUrl))
                            {
                                sectionsToDownload.Add(sectionUrl);
                            }
                        }
                        currentSectionIndex = sectionNumber + 1; // Move to the next section after this file's section
                    }
                    else if (sectionNumber >= currentSectionIndex) // If hash matches, advance currentSectionIndex if current file's section is higher
                    {
                        currentSectionIndex = sectionNumber + 1;
                    }
                }

                foreach (string sectionUrl in sectionsToDownload)
                {
                    _downloadManager.ScheduleFile(sectionUrl);
                }
                sectionsToDownload.Clear();

                _downloadManager.Start(); // Start download workers

                byte[]? currentSectionBuffer = null;
                long currentSectionBufferOffset = 0;
                int lastProcessedSection = 0;

                foreach (XmlNode xmlNode in fileInfoNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string originalPath = xmlNode.SelectSingleNode("path")?.InnerText ?? string.Empty;
                    string fileName = xmlNode.SelectSingleNode("file")?.InnerText ?? string.Empty;
                    string targetFilePath = GetTargetFilePath(originalPath, patchPath, fileName);

                    int fileLength = int.Parse(xmlNode.SelectSingleNode("length")?.InnerText ?? "0");
                    XmlNode? compressedNode = xmlNode.SelectSingleNode("compressed");
                    int compressedFileLength = compressedNode != null ? int.Parse(compressedNode.InnerText) : fileLength;
                    int sectionNumber = int.Parse(xmlNode.SelectSingleNode("section")?.InnerText ?? "0");
                    int offsetInSection = int.Parse(xmlNode.SelectSingleNode("offset")?.InnerText ?? "0");

                    // If hash matches and not calculating hashes, skip actual download/decompression for this file
                    if (xmlNode.SelectSingleNode("hash") != null && LZMADataHash.HashesMatch(targetFilePath))
                    {
                        if (downloadSize == 0uL)
                        {
                            subIndexHashLength += compressedFileLength;
                        }
                        currentDownloadedBytes += compressedFileLength; // Accumulate "downloaded" bytes for skipped files
                        UpdateProgress(currentDownloadedBytes, headerLengthCompressed, targetFilePath);

                        // Cancel downloads for sections between the last processed and current if they are not needed
                        if (lastProcessedSection != sectionNumber)
                        {
                            for (int j = lastProcessedSection + 1; j < sectionNumber; j++)
                            {
                                _downloadManager.CancelDownload(string.Format("{0}/section{1}.dat", fullIndexUrl, j));
                            }
                            lastProcessedSection = sectionNumber;
                        }
                        continue; // Skip to next file
                    }

                    // Ensure target directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));

                    // Open file stream
                    using (FileStream fileStream = File.Create(targetFilePath))
                    {
                        int bytesWrittenToFile = 0;

                        while (bytesWrittenToFile < compressedFileLength)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // If current section buffer is null or exhausted, get the next section
                            if (currentSectionBuffer == null || currentSectionBufferOffset >= currentSectionBuffer.Length)
                            {
                                // Cancel downloads for sections between the last processed and current if they are not needed
                                if (lastProcessedSection != sectionNumber)
                                {
                                    for (int j = lastProcessedSection + 1; j < sectionNumber; j++)
                                    {
                                        _downloadManager.CancelDownload(string.Format("{0}/section{1}.dat", fullIndexUrl, j));
                                    }
                                }

                                string sectionUrl = string.Format("{0}/section{1}.dat", fullIndexUrl, sectionNumber);

                                currentSectionBuffer = await Task.Run(() => _downloadManager.GetFile(sectionUrl), cancellationToken); // Blocking call
                                cancellationToken.ThrowIfCancellationRequested();

                                if (currentSectionBuffer == null)
                                {
                                    ExceptionRouter(true, new ArgumentNullException(nameof(currentSectionBuffer), $"DownloadManager returned a null buffer for {sectionUrl}."));
                                    return;
                                }

                                currentSectionBufferOffset = 0; // Reset offset for the new section
                                lastProcessedSection = sectionNumber;
                                // Schedule next section proactively
                                if (currentDownloadedBytes + currentSectionBuffer.Length < headerLengthCompressed)
                                {
                                    _downloadManager.ScheduleFile(string.Format("{0}/section{1}.dat", fullIndexUrl, sectionNumber + 1));
                                }
                            }

                            // Determine how many bytes to copy from the current section buffer to the file
                            int bytesToCopyFromBuffer = Math.Min(compressedFileLength - bytesWrittenToFile, (int)(currentSectionBuffer.Length - currentSectionBufferOffset));

                            // If it's an LZMA compressed file, handle the 13-byte header
                            if (compressedNode != null) // Implies LZMA compression
                            {
                                byte[] lzmaHeader = new byte[LzmaHeaderSize];
                                int bytesCopiedToLzmaHeader = 0;

                                // Copy the LZMA header first if needed
                                if (bytesWrittenToFile == 0 && currentSectionBufferOffset == offsetInSection)
                                {
                                    int headerBytesRemaining = LzmaHeaderSize;
                                    int currentOffset = (int)currentSectionBufferOffset;

                                    // Check if the entire header is within the current buffer and starts at the correct offset
                                    if (currentSectionBuffer.Length - currentOffset >= LzmaHeaderSize)
                                    {
                                        Buffer.BlockCopy(currentSectionBuffer, currentOffset, lzmaHeader, 0, LzmaHeaderSize);
                                        currentOffset += LzmaHeaderSize;
                                        bytesWrittenToFile += LzmaHeaderSize; // Consider header as part of written bytes
                                        currentSectionBufferOffset += LzmaHeaderSize;
                                    }
                                    else // Header spans across section boundaries (unlikely for typical LZMA streams)
                                    {
                                        ExceptionRouter(true, new Download_LZMA_Exception($"LZMA header spans section boundaries for {targetFilePath}. This scenario is not fully supported."));
                                        return;
                                    }

                                    if (!IsLzma(lzmaHeader))
                                    {
                                        ExceptionRouter(true, new Download_LZMA_Exception("Compression algorithm not recognized for: " + targetFilePath));
                                        return;
                                    }

                                    // Extract properties and uncompressed size from header
                                    byte[] props = new byte[LzmaPropsSize];
                                    Buffer.BlockCopy(lzmaHeader, 0, props, 0, LzmaPropsSize);

                                    long uncompressedSizeFromHeader = 0L;
                                    for (int n = 0; n < 8; n++)
                                    {
                                        uncompressedSizeFromHeader |= (long)((long)lzmaHeader[n + LzmaPropsSize] << (8 * n));
                                    }

                                    if (uncompressedSizeFromHeader != fileLength)
                                    {
                                        ExceptionRouter(true, new Download_LZMA_Exception($"Decompression size in header '{uncompressedSizeFromHeader}' != than in metadata '{fileLength}' for {targetFilePath}."));
                                        return;
                                    }

                                    // Write the LZMA properties and size to the file (this is part of the compressed data)
                                    fileStream.Write(lzmaHeader, 0, LzmaHeaderSize);
                                }

                                // Copy remaining compressed data (after header if applicable)
                                int actualBytesToCopy = Math.Min(compressedFileLength - bytesWrittenToFile, (int)(currentSectionBuffer.Length - currentSectionBufferOffset));
                                fileStream.Write(currentSectionBuffer, (int)currentSectionBufferOffset, actualBytesToCopy);
                                currentSectionBufferOffset += actualBytesToCopy;
                                bytesWrittenToFile += actualBytesToCopy;
                            }
                            else // Not LZMA compressed (regular file copy)
                            {
                                fileStream.Write(currentSectionBuffer, (int)currentSectionBufferOffset, bytesToCopyFromBuffer);
                                currentSectionBufferOffset += bytesToCopyFromBuffer;
                                bytesWrittenToFile += bytesToCopyFromBuffer;
                            }

                            currentDownloadedBytes += bytesToCopyFromBuffer; // Update total downloaded bytes
                            UpdateProgress(currentDownloadedBytes, headerLengthCompressed, targetFilePath);
                        }

                        // Decompress if LZMA
                        if (compressedNode != null)
                        {
                            fileStream.Close(); // Close to allow re-opening for decompression
                            fileStream.Dispose();

                            // Read the file for decompression
                            byte[] fileContent = File.ReadAllBytes(targetFilePath);

                            if (fileContent.Length < LzmaHeaderSize)
                            {
                                ExceptionRouter(true, new Download_LZMA_Exception($"Downloaded file {targetFilePath} is too small to be a valid LZMA file."));
                                return;
                            }

                            byte[] lzmaProps = new byte[LzmaPropsSize];
                            Buffer.BlockCopy(fileContent, 0, lzmaProps, 0, LzmaPropsSize);

                            long uncompressedSizeFromHeader = 0L;
                            for (int n = 0; n < 8; n++)
                            {
                                uncompressedSizeFromHeader |= (long)((long)fileContent[n + LzmaPropsSize] << (8 * n));
                            }

                            byte[] compressedDataWithoutHeader = new byte[fileContent.Length - LzmaHeaderSize];
                            Buffer.BlockCopy(fileContent, LzmaHeaderSize, compressedDataWithoutHeader, 0, compressedDataWithoutHeader.Length);

                            IntPtr actualCompressedSizePtr = new IntPtr(compressedDataWithoutHeader.Length);
                            IntPtr expectedUncompressedSizePtr = new IntPtr((int)uncompressedSizeFromHeader);

                            int decompressResult = Download_LZMA.LzmaUncompressBuf2File(targetFilePath, ref expectedUncompressedSizePtr, compressedDataWithoutHeader, ref actualCompressedSizePtr, lzmaProps, new IntPtr(LzmaPropsSize));

                            LiveExtract?.Invoke(this, new Download_Extract_Progress_EventArgs(targetFilePath, headerLengthCompressed, currentDownloadedBytes, ProgressStartTime ?? DateTime.Now));

                            if (decompressResult != 0)
                            {
                                ExceptionRouter(true, new Download_LZMA_Exception_Uncompression(decompressResult, $"Decompression of {targetFilePath} returned error code: {decompressResult}"));
                                return;
                            }
                            if (expectedUncompressedSizePtr.ToInt32() != fileLength)
                            {
                                ExceptionRouter(true, new Download_LZMA_Exception($"Decompression of {targetFilePath} returned different size '{expectedUncompressedSizePtr.ToInt32()}' than metadata '{fileLength}'"));
                                return;
                            }
                        }
                    } // FileStream is disposed here
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    LZMADataHash.WriteHashCache(package + ".hsh", false);
                }

                if (Complete != null && !cancellationToken.IsCancellationRequested)
                {
                    Complete(this, new Download_Data_Complete_EventArgs(true, DateTime.Now));
                }
            }
            catch (OperationCanceledException)
            {
                Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now, "Operation was cancelled."));
            }
            catch (Download_LZMA_Exception ex)
            {
                ExceptionRouter(true, ex);
            }
            catch (Exception ex)
            {
                ExceptionRouter(true, ex);
            }
            finally
            {
                if (calculateHashes)
                {
                    LZMADataHash.Clear(); // Clear hashes regardless of success/failure if they were calculated for this operation.
                }
                _downloadManager.Clear();
                _isDownloadingOrVerifying = false;
            }
        }

        /// <summary>
        /// Performs the actual verification operation.
        /// </summary>
        /// <param name="parameters">An array of parameters for the verification.</param>
        /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
        private async Task VerifyAsync(string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes, CancellationToken cancellationToken)
        {
            _isDownloadingOrVerifying = true;
            string fullIndexUrl = indexUrl;
            if (!string.IsNullOrWhiteSpace(package))
            {
                fullIndexUrl += "/" + package;
            }

            try
            {
                XmlDocument? indexFile = await GetIndexFileAsync(fullIndexUrl + "/index.xml", false, cancellationToken); // Verification usually doesn't cache index file
                cancellationToken.ThrowIfCancellationRequested();

                if (indexFile == null)
                {
                    ExceptionRouter(true, new ArgumentNullException(nameof(indexFile), "Index File cannot be Null."));
                    return;
                }

                long totalLength = long.Parse(indexFile.SelectSingleNode("/index/header/length")?.InnerText ?? "0");

                XmlNodeList? fileInfoNodes = indexFile.SelectNodes("/index/fileinfo");
                if (fileInfoNodes == null)
                {
                    ExceptionRouter(true, new InvalidOperationException("No file information found in the index file."));
                    return;
                }

                LZMADataHash.Clear(); // Ensure a clean state for hash calculations
                LZMADataHash.Start(indexFile, patchPath, package + ".hsh", HashThreads);

                long totalCurrentLength = 0; // Tracks total bytes verified
                bool verificationFailed = false;

                foreach (XmlNode xmlNode in fileInfoNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string originalPath = xmlNode.SelectSingleNode("path")?.InnerText ?? string.Empty;
                    string fileNameOnRecord = xmlNode.SelectSingleNode("file")?.InnerText ?? string.Empty;
                    string targetFilePath = GetTargetFilePath(originalPath, patchPath, fileNameOnRecord);

                    long addLength = long.Parse(xmlNode.SelectSingleNode("length")?.InnerText ?? "0");

                    XmlNode? hashNode = xmlNode.SelectSingleNode("hash");
                    if (hashNode != null)
                    {
                        if (!LZMADataHash.HashesMatch(targetFilePath))
                        {
                            verificationFailed = true;
                            if (stopOnFail)
                            {
                                ExceptionRouter(true, new ArithmeticException($"Hashes do not match for {targetFilePath}. Stopping verification."));
                                return;
                            }
                        }
                    }
                    else
                    {
                        // File has no hash in metadata. Cannot verify.
                        // Depending on requirements, this might be an error or just a warning.
                        // For now, treat as failure if stopOnFail is true.
                        if (stopOnFail)
                        {
                            ExceptionRouter(true, new Download_LZMA_Exception($"File '{targetFilePath}' has no hash in metadata. Cannot verify."));
                            return;
                        }
                        verificationFailed = true; // Mark as failed as it can't be verified fully
                    }

                    totalCurrentLength += addLength;
                    UpdateProgress(totalCurrentLength, totalLength, fileNameOnRecord);
                }

                if (writeHashes && !cancellationToken.IsCancellationRequested)
                {
                    LZMADataHash.WriteHashCache(package + ".hsh", true);
                }

                if (verificationFailed)
                {
                    ExceptionRouter(true, new ArithmeticException("Verification completed with mismatches."));
                    // Do not call Complete(true) if verification failed
                }
                else
                {
                    Complete?.Invoke(this, new Download_Data_Complete_EventArgs(true, DateTime.Now));
                }
            }
            catch (OperationCanceledException)
            {
                Complete?.Invoke(this, new Download_Data_Complete_EventArgs(false, DateTime.Now, "Operation was cancelled."));
            }
            catch (Download_LZMA_Exception ex)
            {
                ExceptionRouter(true, ex);
            }
            catch (Exception ex)
            {
                ExceptionRouter(true, ex);
            }
            finally
            {
                if (clearHashes)
                {
                    LZMADataHash.Clear();
                }
                _isDownloadingOrVerifying = false;
            }
        }

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

        /// <summary>
        /// Retrieves XML data from a URL.
        /// </summary>
        /// <param name="url">The URL to retrieve data from.</param>
        /// <returns>The XML content as a string.</returns>
        public static async Task<string> GetXmlAsync(string url)
        {
            byte[] data = await GetDataAsync(url);
            if (IsLzma(data))
            {
                return DecompressLZMA(data);
            }
            return Encoding.UTF8.GetString(data).Trim();
        }

        /// <summary>
        /// Retrieves raw data from a URL.
        /// </summary>
        /// <param name="url">The URL to retrieve data from.</param>
        /// <returns>The raw data as a byte array.</returns>
        public static async Task<byte[]> GetDataAsync(string url)
        {
            Uri urlCall = new Uri(url);
            ServicePointManager.FindServicePoint(urlCall).ConnectionLeaseTimeout = (int)(Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds : TimeSpan.FromMinutes(1).TotalMilliseconds);

            using (WebClient client = Download_LZMA_Settings.Alternative_WebCalls ? new WebClient() : new WebClientWithTimeout())
            {
                if (Download_LZMA_Settings.Alternative_WebCalls)
                {
                    client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                }
                client.Headers.Add("Accept", "text/html,text/xml,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.Headers.Add("Accept-Language", "en-us,en;q=0.5");
                client.Headers.Add("Accept-Encoding", "gzip");
                client.Headers.Add("Accept-Charset", "utf-8;q=0.7,*;q=0.7");
                client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

                return await client.DownloadDataTaskAsync(urlCall);
            }
        }

        /// <summary>
        /// Checks if a byte array represents LZMA compressed data.
        /// </summary>
        /// <param name="arr">The byte array to check.</param>
        /// <returns>True if it's LZMA, false otherwise.</returns>
        public static bool IsLzma(byte[] arr)
        {
            return arr.Length >= 2 && arr[0] == 93 && arr[1] == 0;
        }

        /// <summary>
        /// Decompresses LZMA compressed data.
        /// </summary>
        /// <param name="compressedFile">The LZMA compressed byte array.</param>
        /// <returns>The decompressed string.</returns>
        public static string DecompressLZMA(byte[] compressedFile)
        {
            if (compressedFile.Length < LzmaHeaderSize)
            {
                throw new ArgumentException("Compressed file is too small to contain LZMA header.", nameof(compressedFile));
            }

            byte[] props = new byte[LzmaPropsSize];
            Buffer.BlockCopy(compressedFile, 0, props, 0, LzmaPropsSize);

            long uncompressedSize = 0L;
            for (int j = 0; j < 8; j++)
            {
                uncompressedSize |= (long)((long)compressedFile[j + LzmaPropsSize] << (8 * j));
            }

            byte[] compressedDataWithoutHeader = new byte[compressedFile.Length - LzmaHeaderSize];
            Buffer.BlockCopy(compressedFile, LzmaHeaderSize, compressedDataWithoutHeader, 0, compressedDataWithoutHeader.Length);

            IntPtr actualCompressedSizePtr = new IntPtr(compressedDataWithoutHeader.Length);
            IntPtr expectedUncompressedSizePtr = new IntPtr((int)uncompressedSize);
            byte[] decompressedBuffer = new byte[(int)uncompressedSize]; // Allocate buffer for decompressed data

            int result = Download_LZMA.LzmaUncompress(decompressedBuffer, ref expectedUncompressedSizePtr, compressedDataWithoutHeader, ref actualCompressedSizePtr, props, new IntPtr(LzmaPropsSize));

            if (result != 0)
            {
                throw new Download_LZMA_Exception_Uncompression(result, $"LZMA decompression failed with error code: {result}");
            }

            return Encoding.UTF8.GetString(decompressedBuffer, 0, expectedUncompressedSizePtr.ToInt32());
        }
    }
}