using System;
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
        /// <param name="dest"></param>
        /// <param name="destLen"></param>
        /// <param name="src"></param>
        /// <param name="srcLen"></param>
        /// <param name="outProps"></param>
        /// <param name="outPropsSize"></param>
        /// <returns></returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaUncompress", CharSet = CharSet.Auto, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaUncompress([In, Out] byte[] dest, [In, Out] IntPtr destLen, [In, Out] byte[] src, [In, Out] IntPtr srcLen, [In, Out] byte[] outProps, [In, Out] IntPtr outPropsSize);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="destFile"></param>
        /// <param name="destLen"></param>
        /// <param name="src"></param>
        /// <param name="srcLen"></param>
        /// <param name="outProps"></param>
        /// <param name="outPropsSize"></param>
        /// <returns></returns>
        [DllImport("LZMA.dll", EntryPoint = "LzmaUncompressBuf2File", CharSet = CharSet.Auto, ExactSpelling = false, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int LzmaUncompressBuf2File([In] string destFile, [In, Out] IntPtr destLen, [In, Out] byte[] src, [In, Out] IntPtr srcLen, byte[] outProps, [In, Out] IntPtr outPropsSize);
    }
}