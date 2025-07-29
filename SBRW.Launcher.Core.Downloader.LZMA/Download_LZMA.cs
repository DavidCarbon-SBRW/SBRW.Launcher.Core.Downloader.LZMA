using System;
using System.Runtime.InteropServices;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// Provides static methods for LZMA compression and decompression through native library interop.
    /// </summary>
    public static class Download_LZMA
    {
        /// <summary>
        /// Uncompresses LZMA data from a source byte array to a destination byte array.
        /// </summary>
        /// <param name="dest">The destination byte array to write the uncompressed data to.</param>
        /// <param name="destLen">A reference to the length of the destination buffer on input, and the actual uncompressed length on output.</param>
        /// <param name="src">The source byte array containing the compressed data.</param>
        /// <param name="srcLen">A reference to the length of the source buffer on input, and the actual consumed compressed length on output.</param>
        /// <param name="outProps">The LZMA properties byte array (typically 5 bytes).</param>
        /// <param name="outPropsSize">The size of the LZMA properties byte array.</param>
        /// <returns>An integer representing the success or failure code of the uncompression operation.</returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaUncompress", CharSet = CharSet.Ansi, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);

        /// <summary>
        /// Uncompresses LZMA data from a source byte array directly to a destination file.
        /// </summary>
        /// <param name="destFile">The path to the destination file to write the uncompressed data to.</param>
        /// <param name="destLen">A reference to the expected length of the uncompressed data on input, and the actual uncompressed length on output.</param>
        /// <param name="src">The source byte array containing the compressed data.</param>
        /// <param name="srcLen">A reference to the length of the source buffer on input, and the actual consumed compressed length on output.</param>
        /// <param name="outProps">The LZMA properties byte array (typically 5 bytes).</param>
        /// <param name="outPropsSize">The size of the LZMA properties byte array.</param>
        /// <returns>An integer representing the success or failure code of the uncompression operation.</returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaUncompressBuf2File", CharSet = CharSet.Ansi, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaUncompressBuf2File(string destFile, ref IntPtr destLen, byte[] src, ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);

        /// <summary>
        /// Uncompresses LZMA data from a source file directly to a destination file.
        /// </summary>
        /// <param name="destFile">The path to the destination file to write the uncompressed data to.</param>
        /// <param name="destLen">A reference to the expected length of the uncompressed data on input, and the actual uncompressed length on output.</param>
        /// <param name="srcFile">The path to the source file containing the compressed data.</param>
        /// <param name="srcLen">A reference to the expected length of the compressed data on input, and the actual consumed compressed length on output.</param>
        /// <param name="outProps">The LZMA properties byte array (typically 5 bytes).</param>
        /// <param name="outPropsSize">The size of the LZMA properties byte array.</param>
        /// <returns>An integer representing the success or failure code of the uncompression operation.</returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaUncompressFile2File", CharSet = CharSet.Ansi, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaUncompressFile2File(string destFile, ref IntPtr destLen, string srcFile, ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);

        /// <summary>
        /// Compresses data from a source byte array to a destination byte array using LZMA.
        /// </summary>
        /// <param name="dest">The destination byte array to write the compressed data to.</param>
        /// <param name="destLen">A reference to the length of the destination buffer on input, and the actual compressed length on output.</param>
        /// <param name="src">The source byte array containing the uncompressed data.</param>
        /// <param name="srcLen">A reference to the length of the source buffer on input, and the actual consumed uncompressed length on output.</param>
        /// <param name="outProps">The LZMA properties byte array (output, typically 5 bytes).</param>
        /// <param name="outPropsSize">A reference to the size of the LZMA properties byte array.</param>
        /// <param name="level">The compression level (0-9).</param>
        /// <param name="lc">Literal context bits (0-8).</param>
        /// <param name="lp">Literal position bits (0-4).</param>
        /// <param name="pb">Position bits (0-4).</param>
        /// <param name="fb">Fast bytes (5-273).</param>
        /// <param name="numThreads">Number of threads to use for compression.</param>
        /// <returns>An integer representing the success or failure code of the compression operation.</returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaCompress", CharSet = CharSet.Ansi, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaCompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen, byte[] outProps, ref IntPtr outPropsSize, int level, int lc, int lp, int pb, int fb, int numThreads);

        /// <summary>
        /// Compresses data from a source file to a destination file using LZMA.
        /// </summary>
        /// <param name="destFile">The path to the destination file to write the compressed data to.</param>
        /// <param name="destLen">A reference to the expected length of the compressed data on input, and the actual compressed length on output.</param>
        /// <param name="srcFile">The path to the source file containing the uncompressed data.</param>
        /// <param name="srcLen">A reference to the expected length of the uncompressed data on input, and the actual consumed uncompressed length on output.</param>
        /// <param name="outProps">The LZMA properties byte array (output, typically 5 bytes).</param>
        /// <param name="outPropsSize">A reference to the size of the LZMA properties byte array.</param>
        /// <param name="level">The compression level (0-9).</param>
        /// <param name="lc">Literal context bits (0-8).</param>
        /// <param name="lp">Literal position bits (0-4).</param>
        /// <param name="pb">Position bits (0-4).</param>
        /// <param name="fb">Fast bytes (5-273).</param>
        /// <param name="numThreads">Number of threads to use for compression.</param>
        /// <returns>An integer representing the success or failure code of the compression operation.</returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaCompressFile2File", CharSet = CharSet.Ansi, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaCompressFile2File(string destFile, ref IntPtr destLen, string srcFile, ref IntPtr srcLen, byte[] outProps, ref IntPtr outPropsSize, int level, int lc, int lp, int pb, int fb, int numThreads);
    }
}