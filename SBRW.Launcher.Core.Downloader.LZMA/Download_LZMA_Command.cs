namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Abstract base class for LZMA download commands.
    /// </summary>
    public abstract class Download_LZMA_Command
    {
        /// <summary>
        /// Gets or sets the cached LZMA data.
        /// </summary>
        public Download_LZMA_Data Cached_Data { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Command"/> class.
        /// </summary>
        /// <param name="downloader">The LZMA downloader data instance.</param>
        protected Download_LZMA_Command(Download_LZMA_Data downloader)
        {
            this.Cached_Data = downloader;
        }

        /// <summary>
        /// Executes the command with the specified parameters.
        /// </summary>
        /// <param name="parameters">An array of parameters for the command execution.</param>
        public abstract void Execute(object[] parameters);
    }
}