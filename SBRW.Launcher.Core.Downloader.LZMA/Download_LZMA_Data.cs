﻿using SBRW.Launcher.Core.Downloader.LZMA.Exception_;
using SBRW.Launcher.Core.Downloader.LZMA.EventArg_;
using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Xml;
using SBRW.Launcher.Core.Downloader.LZMA.Extension_;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// 
    /// </summary>
    public class Download_LZMA_Data
    {
        /// <summary>
        /// 
        /// </summary>
        public Download_Information_LZMA? Download_Status_Information { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public Download_Information_LZMA? Download_Status() { return Download_Status_Information; }
        /// <summary>
        /// 
        /// </summary>
        public bool Disable_Download_Status_Information { get; set; }
        /// <summary>
        /// 
        /// </summary>
        private Thread MThread { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="Events"></param>
        public delegate void Download_Data_Progress_Handler(object Sender, Download_Data_Progress_EventArgs Events);
        /// <summary>
        /// 
        /// </summary>
        public event Download_Data_Progress_Handler? Live_Progress;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="Events"></param>
        public delegate void Download_Data_Completion_Handler(object Sender, Download_Data_Complete_EventArgs Events);
        /// <summary>
        /// 
        /// </summary>
        public event Download_Data_Completion_Handler? Complete;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="Events"></param>
        public delegate void Download_Data_Exception_Handler(object Sender, Download_Exception_EventArgs Events);
        /// <summary>
        /// 
        /// </summary>
        public event Download_Data_Exception_Handler? Internal_Error;
        /// <summary>
        /// .
        /// </summary>
        public event Download_Data_Exception_Handler? Internal_Web_Error;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="Events"></param>
        public delegate void Download_Data_Extract_Handler(object Sender, Download_Extract_Progress_EventArgs Events);
        /// <summary>
        /// 
        /// </summary>
        public event Download_Data_Extract_Handler? Live_Extract;
        private bool MDownloading { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int MHashThreads { get; internal set; }
        private Download_LZMA_Data_Manager MDownloadManager { get; set; }
        private XmlDocument? MIndexCached { get; set; }
        private bool MStopFlag { get; set; }
        private XmlDocument? Xml_Result { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Download_LZMA_Data_Hash? LZMA_Data_Hash { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime? Progress_Last_Update { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime? Progress_Start_Time { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public bool Downloading
        {
            get { return this.MDownloading; }
        }
        /// <summary>
        /// 
        /// </summary>
        public int Download_Percentage_Parts { get; set; } = 1;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="Event_Hook"></param>
        /// <param name="Exception_Caught"></param>
        internal void Exception_Router(bool Event_Hook, Exception Exception_Caught)
        {
            try
            {
                if (this.Internal_Error != null && Event_Hook && !MStopFlag)
                {
                    this.Internal_Error(this, new Download_Exception_EventArgs(Exception_Caught, DateTime.Now));
                }
                else
                {
                    throw Exception_Caught;
                }
            }
            finally
            {
                if (!MStopFlag)
                {
                    Stop();
                }
            }
        }
        //@DavidCarbon and/or @Zacam
        //long downloadLength, long downloadCurrent, long compressedLength, string filename = "", int skiptime = 0
        /// <summary>
        /// long downloadLength, long downloadCurrent, long compressedLength, string filename = "", int skiptime = 0
        /// </summary>
        /// <param name="Download_Current"></param>
        /// <param name="Compressed_Length"></param>
        /// <param name="Download_File_Name"></param>
        private void Updated_Progress(long Download_Current, long Compressed_Length, string Download_File_Name = "")
        {
            try
            {
                if (!Disable_Download_Status_Information && !MStopFlag)
                {
                    Download_Status_Information = new Download_Information_LZMA()
                    {
                        File_Name = Download_File_Name,
                        File_Size_Total = Compressed_Length,
                        File_Size_Current = Download_Current,
                        File_Size_Remaining = Compressed_Length - Download_Current,
                        Download_Percentage = (int)(((double)Download_Current) / Compressed_Length * 100 / Download_Percentage_Parts),
                        Start_Time = Progress_Start_Time ?? DateTime.Now
                    };
                }

                if (this.Live_Progress != null && !MStopFlag)
                {
                    this.Live_Progress(this, new Download_Data_Progress_EventArgs(Download_Current, Compressed_Length, Download_File_Name, Progress_Start_Time??DateTime.Now));
                }
            }
            catch (Exception)
            {
                /* Ignore Exception */
            }
        }
        /// <summary>
        /// 
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Download_LZMA_Data()
        {
            this.MHashThreads = 3;
            this.Progress_Start_Time = DateTime.Now;
            this.MDownloadManager = new Download_LZMA_Data_Manager(3, 16);
            if(LZMA_Data_Hash == default)
            {
                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashThreads"></param>
        public Download_LZMA_Data(int hashThreads)
        {
            this.MHashThreads = hashThreads;
            this.Progress_Start_Time = DateTime.Now;
            this.MDownloadManager = new Download_LZMA_Data_Manager(3, 16);
            if (LZMA_Data_Hash == default)
            {
                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashThreads"></param>
        /// <param name="downloadThreads"></param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads)
        {
            this.MHashThreads = hashThreads;
            this.Progress_Start_Time = DateTime.Now;
            this.MDownloadManager = new Download_LZMA_Data_Manager(downloadThreads, 16);
            if (LZMA_Data_Hash == default)
            {
                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashThreads"></param>
        /// <param name="downloadThreads"></param>
        /// <param name="downloadChunks"></param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks)
        {
            this.MHashThreads = hashThreads;
            this.Progress_Start_Time = DateTime.Now;
            this.MDownloadManager = new Download_LZMA_Data_Manager(downloadThreads, downloadChunks);
            if (LZMA_Data_Hash == default)
            {
                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashThreads"></param>
        /// <param name="downloadThreads"></param>
        /// <param name="downloadChunks"></param>
        /// <param name="Start_Time"></param>
        public Download_LZMA_Data(int hashThreads, int downloadThreads, int downloadChunks, DateTime Start_Time)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.MHashThreads = hashThreads;
            this.Progress_Start_Time = Start_Time;
            this.MDownloadManager = new Download_LZMA_Data_Manager(downloadThreads, downloadChunks);
            if (LZMA_Data_Hash == default)
            {
                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexUrl"></param>
        /// <param name="package"></param>
        /// <param name="patchPath"></param>
        /// <param name="calculateHashes"></param>
        /// <param name="useIndexCache"></param>
        /// <param name="downloadSize"></param>
        public void StartDownload(string indexUrl, string package, string patchPath, bool calculateHashes, bool useIndexCache, int downloadSize)
        {
            MStopFlag = false;
            this.MThread = new Thread(new ParameterizedThreadStart(this.Download));
            string[] parameter = new string[]
            {
                indexUrl,
                package,
                patchPath,
                calculateHashes.ToString(),
                useIndexCache.ToString(),
                downloadSize.ToString()
            };
            this.MThread.IsBackground = true;
            this.MThread.Start(parameter);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexUrl"></param>
        /// <param name="package"></param>
        /// <param name="patchPath"></param>
        /// <param name="stopOnFail"></param>
        /// <param name="clearHashes"></param>
        /// <param name="writeHashes"></param>
        public void StartVerification(string indexUrl, string package, string patchPath, bool stopOnFail, bool clearHashes, bool writeHashes)
        {
            MStopFlag = false;
            this.MThread = new Thread(new ParameterizedThreadStart(this.Verify));
            string[] parameter = new string[]
            {
                indexUrl,
                package,
                patchPath,
                stopOnFail.ToString(),
                clearHashes.ToString(),
                writeHashes.ToString()
            };
            this.MThread.IsBackground = true;
            this.MThread.Start(parameter);
        }
        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            MStopFlag = true;
            if (this.MDownloadManager != null && this.MDownloadManager.ManagerRunning)
            {
                this.MDownloadManager.CancelAllDownloads();
            }
        }

        private void Downloader_DownloadFileCompleted(object sender, DownloadDataCompletedEventArgs Live_Download_Data)
        {
            if (Live_Download_Data.Error != null && !Disable_Download_Status_Information && !MStopFlag)
            {
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

            if (Live_Download_Data.Error != null && this.Internal_Web_Error != null && !MStopFlag)
            {
                this.Internal_Web_Error(this, new Download_Exception_EventArgs(Live_Download_Data.Error, DateTime.Now));
            }
        }

        private XmlDocument GetIndexFile(string url, bool useCache)
        {
            try
            {
                if (useCache && MIndexCached != null)
                {
                    Xml_Result = MIndexCached;
                }
                else
                {
                    Uri URLCall = new Uri(url);


                    ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)(Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                    TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds : TimeSpan.FromMinutes(1).TotalMilliseconds);
                    var Client = new WebClient();

                    if (!Download_LZMA_Settings.Alternative_WebCalls) { Client = new WebClientWithTimeout(); }
                    else
                    {
                        Client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                    }
                    Client.DownloadDataCompleted += new DownloadDataCompletedEventHandler(this.Downloader_DownloadFileCompleted);

                    try
                    {
                        string tempFileName = Path.GetTempFileName();
                        Client.DownloadFileAsync(URLCall, tempFileName);
                        while (Client.IsBusy)
                        {
                            if (MStopFlag)
                            {
                                Client.CancelAsync();
                                Xml_Result = null;
#pragma warning disable CS8603
                                return Xml_Result;
#pragma warning restore CS8603
                            }
                            Thread.Sleep(100);
                        }
                        XmlDocument xmlDocument = new XmlDocument();
                        xmlDocument.Load(tempFileName);
                        MIndexCached = xmlDocument;
                        Xml_Result = xmlDocument;
                    }
                    catch (Exception)
                    {
                        Xml_Result = null;
                    }
                    finally
                    {
                        if (Client != null)
                        {
                            Client.Dispose();
                        }
                    }
                }
            }
            catch (Exception)
            {
                Xml_Result = null;
            }

            return Xml_Result ?? new XmlDocument();
        }

        private void Download(object parameters)
        {
            this.MDownloading = true;
            string[] array = (string[])parameters;
            string text = array[0];
            string text2 = array[1];
            if (!string.IsNullOrWhiteSpace(text2))
            {
                text = text + "/" + text2;
            }
            string text3 = array[2];
            bool flag = bool.Parse(array[3]);
            bool useCache = bool.Parse(array[4]);
            ulong num = ulong.Parse(array[5]);
            byte[]? array2 = default;
            XmlNodeList? xmlNodeList = default;
            try
            {
                XmlDocument indexFile = this.GetIndexFile(text + "/index.xml", useCache);
                if (indexFile == null)
                {
                    Exception_Router(true, new ArgumentNullException("indexFile", "Index File can not be Null"));
                    return;
                }
                else
                {
                    long Header_Length = long.Parse(indexFile.SelectSingleNode("/index/header/length").InnerText);
                    long Sub_Index_Hash_Length = 0L;
                    long Header_Length_Compressed;
                    if (num == 0uL)
                    {
                        Header_Length_Compressed = long.Parse(indexFile.SelectSingleNode("/index/header/compressed").InnerText);
                    }
                    else
                    {
                        Header_Length_Compressed = (long)num;
                    }
                    long num5 = 0L;
                    var Client = new WebClient();

                    if (!Download_LZMA_Settings.Alternative_WebCalls) { Client = new WebClientWithTimeout(); }
                    else
                    {
                        Client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                    }
                    Client.Headers.Add("Accept", "text/html,text/xml,application/xhtml+xml,application/xml,application/*,*/*;q=0.9,*/*;q=0.8");
                    Client.Headers.Add("Accept-Language", "en-us,en;q=0.5");
                    Client.Headers.Add("Accept-Encoding", "gzip,deflate");
                    Client.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7");
                    int num6 = 1;
                    xmlNodeList = indexFile.SelectNodes("/index/fileinfo");
                    this.MDownloadManager.Initialize(indexFile, text);
                    if (flag)
                    {
                        if (LZMA_Data_Hash == default)
                        {
                            LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                        }

                        LZMA_Data_Hash.Clear();
                        LZMA_Data_Hash.Start(indexFile, text3, text2 + ".hsh", this.MHashThreads);
                    }
                    int num7 = 0;
                    List<string> list = new List<string>();
                    int i = 0;
                    bool flag2 = false;
                    int num11;
                    long fileschecked = 0;
                    foreach (XmlNode xmlNode in xmlNodeList)
                    {
                        XmlNodeList xmlNodeList2 = xmlNode.SelectNodes("compressed");
                        int num8;
                        if (xmlNodeList2.Count == 0)
                        {
                            num8 = int.Parse(xmlNode.SelectNodes("length")[0].InnerText);
                        }
                        else
                        {
                            num8 = int.Parse(xmlNodeList2[0].InnerText);
                        }
                        num7 = ((num8 > num7) ? num8 : num7);
                        string text4 = xmlNode.SelectSingleNode("path").InnerText;
                        if (!string.IsNullOrWhiteSpace(text3))
                        {
                            int num9 = text4.IndexOf("/");
                            if (num9 >= 0)
                            {
                                text4 = text4.Replace(text4.Substring(0, num9), text3);
                            }
                            else
                            {
                                text4 = text3;
                            }
                        }
                        string innerText = xmlNode.SelectSingleNode("file").InnerText;
                        string fileName = text4 + "/" + innerText;
                        int num10 = int.Parse(xmlNode.SelectSingleNode("section").InnerText);
                        num11 = int.Parse(xmlNode.SelectSingleNode("offset").InnerText);
                        if (LZMA_Data_Hash == default)
                        {
                            LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                        }
                        if (flag)
                        {
                            if (list.Count == 0)
                            {
                                i = num10;
                            }
                            while (i <= num10)
                            {
                                list.Insert(0, string.Format("{0}/section{1}.dat", text, i));
                                i++;
                            }
                        }
                        else if (!LZMA_Data_Hash.HashesMatch(fileName))
                        {
                            if (i <= num10)
                            {
                                if (list.Count == 0)
                                {
                                    i = num10;
                                }
                                while (i <= num10)
                                {
                                    list.Insert(0, string.Format("{0}/section{1}.dat", text, i));
                                    i++;
                                }
                            }
                            flag2 = true;
                        }
                        else
                        {
                            if (flag2)
                            {
                                int num12 = num10;
                                if (num11 == 0)
                                {
                                    num12--;
                                }
                                while (i <= num12)
                                {
                                    list.Insert(0, string.Format("{0}/section{1}.dat", text, i));
                                    i++;
                                }
                            }
                            if (i < num10)
                            {
                                i = num10;
                            }
                            flag2 = false;
                        }
                    }
                    foreach (string current in list)
                    {
                        this.MDownloadManager.ScheduleFile(current);
                    }
                    list.Clear();
                    num11 = 0;
                    this.MDownloadManager.Start();
                    byte[] array3 = new byte[num7];
                    byte[] array4 = new byte[13];
                    int num13 = 0;
                    foreach (XmlNode xmlNode2 in xmlNodeList)
                    {
                        if (MStopFlag)
                        {
                            break;
                        }
                        else
                        {
                            string text5 = xmlNode2.SelectSingleNode("path").InnerText;
                            string innerText2 = xmlNode2.SelectSingleNode("file").InnerText;
                            if (!string.IsNullOrWhiteSpace(text3))
                            {
                                int num14 = text5.IndexOf("/");
                                if (num14 >= 0)
                                {
                                    text5 = text5.Replace(text5.Substring(0, num14), text3);
                                }
                                else
                                {
                                    text5 = text3;
                                }
                            }
                            string text6 = text5 + "/" + innerText2;
                            int num15 = int.Parse(xmlNode2.SelectSingleNode("length").InnerText);
                            int num16 = 0;
                            XmlNode xmlNode3 = xmlNode2.SelectSingleNode("compressed");
                            if (xmlNode2.SelectSingleNode("section") != null && num6 < int.Parse(xmlNode2.SelectSingleNode("section").InnerText))
                            {
                                num6 = int.Parse(xmlNode2.SelectSingleNode("section").InnerText);
                            }
                            string text7 = string.Empty;
                            if (LZMA_Data_Hash == default)
                            {
                                LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                            }
                            if (xmlNode2.SelectSingleNode("hash") != null && LZMA_Data_Hash.HashesMatch(text6))
                            {
                                num16 += num15;
                                if (xmlNode3 != null)
                                {
                                    if (num == 0uL)
                                    {
                                        Sub_Index_Hash_Length += (long)int.Parse(xmlNode3.InnerText);
                                    }
                                    num5 += (long)int.Parse(xmlNode3.InnerText);
                                    num11 += int.Parse(xmlNode3.InnerText);
                                }
                                else
                                {
                                    if (num == 0uL)
                                    {
                                        Sub_Index_Hash_Length += (long)num15;
                                    }
                                    num5 += (long)num15;
                                    num11 += num15;
                                }

                                Updated_Progress(Sub_Index_Hash_Length, Header_Length_Compressed, text6);

                                int num17 = int.Parse(xmlNode2.SelectSingleNode("section").InnerText);
                                if (num13 != num17)
                                {
                                    for (int j = num13 + 1; j < num17; j++)
                                    {
                                        this.MDownloadManager.CancelDownload(string.Format("{0}/section{1}.dat", text, j));
                                    }
                                    num13 = num17 - 1;
                                }
                            }
                            else
                            {
                                Directory.CreateDirectory(text5);
                                FileStream fileStream = File.Create(text6);
                                int num18 = num15;
                                if (xmlNode3 != null)
                                {
                                    num18 = int.Parse(xmlNode3.InnerText);
                                }
                                int k = 0;
                                bool flag3 = false;
                                int num19 = 13;
                                while (k < num18)
                                {
                                    if (!MStopFlag)
                                    {
                                        if (array2 == null || num11 >= array2.Length)
                                        {
                                            if (xmlNode2.SelectSingleNode("offset") != null && !flag3)
                                            {
                                                num11 = int.Parse(xmlNode2.SelectSingleNode("offset").InnerText);
                                            }
                                            else
                                            {
                                                num11 = 0;
                                            }
                                            text7 = string.Format("{0}/section{1}.dat", text, num6);
                                            for (int l = num13 + 1; l < num6; l++)
                                            {
                                                this.MDownloadManager.CancelDownload(string.Format("{0}/section{1}.dat", text, l));
                                            }
                                            array2 = this.MDownloadManager.GetFile(text7);
                                            if(!MStopFlag)
                                            {
                                                if (array2 == null)
                                                {
                                                    Exception_Router(true, new ArgumentNullException("array2", "DownloadManager returned a null buffer"));
                                                    return;
                                                }
                                                num13 = num6;
                                                num5 += (long)array2.Length;
                                                num6++;
                                                if ((this.MDownloadManager.GetStatus(string.Format("{0}/section{1}.dat", text, num6)) != Download_LZMA_Enumerator.Download_Status.Unknown) &&
                                                    (num5 < Header_Length_Compressed))
                                                {
                                                    this.MDownloadManager.ScheduleFile(string.Format("{0}/section{1}.dat", text, num6));
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (num18 - k > array2.Length - num11)
                                            {
                                                text7 = string.Format("{0}/section{1}.dat", text, num6);
                                                this.MDownloadManager.ScheduleFile(text7);
                                                flag3 = true;
                                            }
                                            int num20 = Math.Min(array2.Length - num11, num18 - k);
                                            if (num19 != 0)
                                            {
                                                if (xmlNode3 != null)
                                                {
                                                    int num21 = Math.Min(num19, num20);
                                                    Buffer.BlockCopy(array2, num11, array4, 13 - num19, num21);
                                                    Buffer.BlockCopy(array2, num11 + num21, array3, 0, num20 - num21);
                                                    num19 -= num21;
                                                }
                                                else
                                                {
                                                    Buffer.BlockCopy(array2, num11, array3, 0, num20);
                                                    num19 = 0;
                                                }
                                            }
                                            else
                                            {
                                                Buffer.BlockCopy(array2, num11, array3, k - ((xmlNode3 != null) ? 13 : 0), num20);
                                            }
                                            num11 += num20;
                                            k += num20;
                                            Sub_Index_Hash_Length += (long)num20;
                                        }

                                        Updated_Progress(Sub_Index_Hash_Length, Header_Length_Compressed, text6);
                                    }
                                }
                                if (xmlNode3 != null)
                                {
                                    if (!IsLzma(array4))
                                    {
                                        Exception_Router(true, new Download_LZMA_Exception("Compression algorithm not recognized: " + text7));
                                        return;
                                    }
                                    fileStream.Close();
                                    fileStream.Dispose();
                                    IntPtr outPropsSize = new IntPtr(5);
                                    byte[] array5 = new byte[5];
                                    for (int m = 0; m < 5; m++)
                                    {
                                        array5[m] = array4[m];
                                    }
                                    long num22 = 0L;
                                    for (int n = 0; n < 8; n++)
                                    {
                                        num22 += (long)((long)array4[n + 5] << 8 * n);
                                    }
                                    if (num22 != (long)num15)
                                    {
                                        Exception_Router(true, new Download_LZMA_Exception("Compression data length in header '" + num22 + "' != than in metadata '" + num15 + "'"));
                                        return;
                                    }
                                    int num23 = num18;
                                    num18 -= 13;
                                    IntPtr intPtr = new IntPtr(num18);
                                    IntPtr value = new IntPtr(num22);
                                    int num24 = Download_LZMA.LzmaUncompressBuf2File(text6, ref value, array3, ref intPtr, array5, outPropsSize);

                                    /* TODO: use total file lenght and extracted file length instead of files checked and total array size. */
                                    fileschecked = +Sub_Index_Hash_Length;

                                    if (this.Live_Extract != null && !MStopFlag)
                                    {
                                        this.Live_Extract(this, new Download_Extract_Progress_EventArgs(text6, Header_Length_Compressed, fileschecked, Progress_Start_Time ?? DateTime.Now));
                                    }

                                    if (num24 != 0)
                                    {
                                        Exception_Router(true, new Download_LZMA_Exception_Uncompression(num24, "Decompression returned " + num24));
                                        return;
                                    }
                                    if (value.ToInt32() != num15)
                                    {
                                        Exception_Router(true, new Download_LZMA_Exception("Decompression returned different size '" + value.ToInt32() + "' than metadata '" + num15 + "'"));
                                        return;
                                    }
                                    num16 += (int)value;
                                }
                                else
                                {
                                    fileStream.Write(array3, 0, num15);
                                    num16 += num15;
                                }
                                if (fileStream != null)
                                {
                                    fileStream.Close();
                                    fileStream.Dispose();
                                }
                            }
                        }
                    }

                    if (!MStopFlag)
                    {
                        if (LZMA_Data_Hash == default)
                        {
                            LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                        }

                        LZMA_Data_Hash.WriteHashCache(text2 + ".hsh", false);
                    }
                    
                    if (this.Complete != null && !MStopFlag)
                    {
                        this.Complete(this, new Download_Data_Complete_EventArgs(true, DateTime.Now));
                    }
                }
            }
            catch (Download_LZMA_Exception Error)
            {
                Exception_Router(true, Error);
                return;
            }
            catch (Exception Error)
            {
                Exception_Router(true, Error);
                return;
            }
            finally
            {
                if (flag)
                {
                    if (LZMA_Data_Hash == default)
                    {
                        LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                    }

                    LZMA_Data_Hash.Clear();
                }
                this.MDownloadManager.Clear();
                this.MDownloading = false;
            }
        }

        private void Verify(object parameters)
        {
            string[] array = (string[])parameters;
            string str = array[0].Trim();
            string text = array[1].Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                str = str + "/" + text;
            }
            string text2 = array[2].Trim();
            bool flag = bool.Parse(array[3]);
            bool flag2 = bool.Parse(array[4]);
            bool flag3 = bool.Parse(array[5]);
            bool flag4 = false;
            try
            {
                XmlDocument indexFile = this.GetIndexFile(str + "/index.xml", false);
                if (indexFile == null)
                {
                    Exception_Router(true, new ArgumentNullException("indexFile", "Index File can not be Null"));
                    return;
                }
                else
                {
                    long Total_Length = long.Parse(indexFile.SelectSingleNode("/index/header/length").InnerText);

                    var Client = new WebClient();

                    if (!Download_LZMA_Settings.Alternative_WebCalls) { Client = new WebClientWithTimeout(); }
                    else
                    {
                        Client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                    }
                    Client.Headers.Add("Accept", "text/html,text/xml,application/xhtml+xml,application/xml,application/*,*/*;q=0.9,*/*;q=0.8");
                    Client.Headers.Add("Accept-Language", "en-us,en;q=0.5");
                    Client.Headers.Add("Accept-Encoding", "gzip,deflate");
                    Client.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7");
                    XmlNodeList xmlNodeList = indexFile.SelectNodes("/index/fileinfo");
                    if (LZMA_Data_Hash == default)
                    {
                        LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                    }
                    LZMA_Data_Hash.Clear();
                    LZMA_Data_Hash.Start(indexFile, text2, text + ".hsh", this.MHashThreads);
                    long Total_Current_Length = 0;
                    ulong num3 = 0;
                    ulong num4 = 0;
                    foreach (XmlNode xmlNode in xmlNodeList)
                    {
                        string text3 = xmlNode.SelectSingleNode("path").InnerText;
                        string File_Name_On_Record = xmlNode.SelectSingleNode("file").InnerText;
                        if (!string.IsNullOrWhiteSpace(text2))
                        {
                            int num5 = text3.IndexOf("/");
                            if (num5 >= 0)
                            {
                                text3 = text3.Replace(text3.Substring(0, num5), text2);
                            }
                            else
                            {
                                text3 = text2;
                            }
                        }
                        string text4 = text3 + "/" + File_Name_On_Record;
                        long Add_Length = long.Parse(xmlNode.SelectSingleNode("length").InnerText);
                        if (xmlNode.SelectSingleNode("hash") != null)
                        {
                            if (!LZMA_Data_Hash.HashesMatch(text4))
                            {
                                num3 += ulong.Parse(xmlNode.SelectSingleNode("length").InnerText);
                                ulong num7;
                                if (xmlNode.SelectSingleNode("compressed") != null)
                                {
                                    num7 = ulong.Parse(xmlNode.SelectSingleNode("compressed").InnerText);
                                }
                                else
                                {
                                    num7 = ulong.Parse(xmlNode.SelectSingleNode("length").InnerText);
                                }
                                num4 += num7;
                                if (flag)
                                {
                                    Exception_Router(true, new ArithmeticException("Hashes do Not Match"));
                                    return;
                                }
                                flag4 = true;
                            }
                        }
                        else
                        {
                            if (flag)
                            {
                                Exception_Router(true, new Download_LZMA_Exception("Without hash in the metadata I cannot verify the download"));
                                return;
                            }
                            flag4 = true;
                        }
                        if (MStopFlag)
                        {
                            return;
                        }

                        Total_Current_Length += Add_Length;

                        Updated_Progress(Total_Current_Length, Total_Length, File_Name_On_Record);
                    }
                    if (flag3)
                    {
                        LZMA_Data_Hash.WriteHashCache(text + ".hsh", true);
                    }
                    if (flag4)
                    {
                        Exception_Router(true, new ArithmeticException("Hashes do Not Match (Late Catch)"));
                        return;
                    }
                    else
                    {
                        Exception_Router(true, new Exception("Unknown Exception was Caught"));
                        return;
                    }
                }
            }
            catch (Download_LZMA_Exception Error)
            {
                Exception_Router(true, Error);
                return;
            }
            catch (Exception Error)
            {
                Exception_Router(true, Error);
                return;
            }
            finally
            {
                if (flag2)
                {
                    if (LZMA_Data_Hash == default)
                    {
                        LZMA_Data_Hash = new Download_LZMA_Data_Hash();
                    }

                    LZMA_Data_Hash.Clear();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetXml(string url)
        {
            byte[] data = GetData(url);
            if (IsLzma(data))
            {
                return DecompressLZMA(data);
            }
            return Encoding.UTF8.GetString(data).Trim();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static byte[] GetData(string url)
        {
            Uri URLCall = new Uri(url);
            ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)(Download_LZMA_Settings.Launcher_WebCall_Timeout_Enable ?
                TimeSpan.FromSeconds(Download_LZMA_Settings.Launcher_WebCall_Timeout_Cache + 1).TotalMilliseconds : TimeSpan.FromMinutes(1).TotalMilliseconds);
            var Client = new WebClient();

            if (!Download_LZMA_Settings.Alternative_WebCalls) { Client = new WebClientWithTimeout(); }
            else
            {
                Client.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
            }
            Client.Headers.Add("Accept", "text/html,text/xml,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            Client.Headers.Add("Accept-Language", "en-us,en;q=0.5");
            Client.Headers.Add("Accept-Encoding", "gzip");
            Client.Headers.Add("Accept-Charset", "utf-8;q=0.7,*;q=0.7");
            Client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            byte[] result = Client.DownloadData(URLCall);
            Client.Dispose();
            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="arr"></param>
        /// <returns></returns>
        public static bool IsLzma(byte[] arr)
        {
            return arr.Length >= 2 && arr[0] == 93 && arr[1] == 0;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="compressedFile"></param>
        /// <returns></returns>
        public static string DecompressLZMA(byte[] compressedFile)
        {
            IntPtr intPtr = new IntPtr(compressedFile.Length - 13);
            byte[] array = new byte[intPtr.ToInt64()];
            IntPtr outPropsSize = new IntPtr(5);
            byte[] array2 = new byte[5];
            compressedFile.CopyTo(array, 13);
            for (int i = 0; i < 5; i++)
            {
                array2[i] = compressedFile[i];
            }
            int num = 0;
            for (int j = 0; j < 8; j++)
            {
                num += (int)compressedFile[j + 5] << 8 * j;
            }
            IntPtr intPtr2 = new IntPtr(num);
            byte[] array3 = new byte[num];
            _ = Download_LZMA.LzmaUncompress(array3, ref intPtr2, array, ref intPtr, array2, outPropsSize);
            return new string(Encoding.UTF8.GetString(array3).ToCharArray());
        }
    }
}