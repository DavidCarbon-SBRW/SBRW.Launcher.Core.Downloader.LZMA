using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// 
    /// </summary>
    public static class Download_LZMA
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="destFile"></param>
        /// <param name="destLen"></param>
        /// <param name="src"></param>
        /// <param name="srcLen"></param>
        /// <returns></returns>
        public static int LzmaUncompressBuf2File(string destFile, IntPtr destLen, byte[] src, IntPtr srcLen, byte[] outProps, IntPtr outPropsSize)
        {
            // Create a MemoryStream containing the compressed data
            using (var inputStream = new MemoryStream(src, 0, srcLen.ToInt32()))
            {
                if (inputStream != default)
                {
                    // Create a FileStream for the output file
                    using (var outputStream = new FileStream(destFile, FileMode.Create))
                    {
                        if (outputStream != default)
                        {
                            // Create an instance of the SevenZip.Compression.LZMA.Decoder
                            // class to decompress the data
                            // Decompress the data and write it to the output file
                            new SevenZip.Compression.LZMA.Decoder().Code(inputStream, outputStream, inputStream.Length, (long)outPropsSize, null);

                            // Set the value of destLen to the size of the decompressed data
                            destLen = new IntPtr((int)outputStream.Length);
                        }
                        else
                        {
                            return -3;
                        }
                    }
                }
                else
                {
                    return -2;
                }
            }

            // Return a value indicating the success of the decompression operation
            return 0;
        }
    }
}