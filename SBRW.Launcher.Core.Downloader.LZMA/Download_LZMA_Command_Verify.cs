namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Represents a command to verify LZMA data.
    /// </summary>
    public class Download_LZMA_Command_Verify : Download_LZMA_Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Download_LZMA_Command_Verify"/> class.
        /// </summary>
        /// <param name="cachedData">The cached LZMA data.</param>
        public Download_LZMA_Command_Verify(Download_LZMA_Data cachedData) : base(cachedData)
        {
        }

        /// <summary>
        /// Executes the verification command.
        /// </summary>
        /// <param name="parameters">An array of parameters for the verification process.
        /// Expected parameters: string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes.</param>
        public override void Execute(object[] parameters)
        {
            // Ensure parameters are correctly cast and passed to StartVerification.
            // This still relies on magic indices, which could be improved with a dedicated DTO or named arguments
            // if the StartVerification method was modified to accept them.
            // For now, ensuring safe casting.
            if (parameters.Length >= 6 &&
                parameters[0] is string indexUrl &&
                parameters[1] is string package &&
                parameters[2] is string patchPath &&
                parameters[3] is bool stopOnFail &&
                parameters[4] is bool clearHashes &&
                parameters[5] is bool writeHashes)
            {
                this.Cached_Data.StartVerification(indexUrl, package, patchPath, stopOnFail, clearHashes, writeHashes);
            }
            else
            {
                // Handle invalid parameters, e.g., throw an ArgumentException or log an error.
                throw new System.ArgumentException("Invalid parameters for Download_LZMA_Command_Verify.Execute. Expected: indexUrl (string), package (string), patchPath (string), stopOnFail (bool), clearHashes (bool), writeHashes (bool).");
            }
        }
    }
}