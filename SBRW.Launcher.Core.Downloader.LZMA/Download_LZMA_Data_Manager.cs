﻿using SBRW.Launcher.Core.Downloader.LZMA.Web_;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Cache;
using System.Threading;
using System.Xml;
using static SBRW.Launcher.Core.Downloader.LZMA.Download_LZMA_Enumerator;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// 
    /// </summary>
    public class Download_LZMA_Data_Manager
    {
        private int Worker_Count { get; set; }
        /// <summary>
        /// Max Background Workers in an Instance
        /// </summary>
        /// <remarks>Default is 3</remarks>
        public int Workers_Max { get; set; } = 3;
        private Dictionary<string, DownloadItem> Download_List { get; set; }
        private LinkedList<string> Download_Queue { get; set; }
        private List<BackgroundWorker> Workers_Live { get; set; }
        /// <summary>
        /// Max Active Chunks in an Instance
        /// </summary>
        /// <remarks>Default is 16</remarks>
        public int Active_Chunks_Max { get; set; } = 16;
        private object Free_ChunksLock { get; set; }
        private bool Manager_Running { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool ManagerRunning
        {
            get { return this.Manager_Running; }
        }
        /// <summary>
        /// 
        /// </summary>
        public Download_LZMA_Data_Manager()
        {
            this.Workers_Max = 3;
            this.Active_Chunks_Max = 16;
            this.Download_List = new Dictionary<string, DownloadItem>();
            this.Download_Queue = new LinkedList<string>();
            this.Workers_Live = new List<BackgroundWorker>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxWorkers"></param>
        /// <param name="maxActiveChunks"></param>
        public Download_LZMA_Data_Manager(int maxWorkers, int maxActiveChunks)
        {
            this.Workers_Max = maxWorkers;
            this.Active_Chunks_Max = maxActiveChunks;
            this.Download_List = new Dictionary<string, DownloadItem>();
            this.Download_Queue = new LinkedList<string>();
            this.Workers_Live = new List<BackgroundWorker>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs args)
        {
            try
            {
                if (Download_LZMA_Settings.Alternative_WebCalls)
                {
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.Headers.Add("user-agent", Download_LZMA_Settings.Header_LZMA);
                        webClient.DownloadDataCompleted += new DownloadDataCompletedEventHandler(this.DownloadManager_DownloadDataCompleted);
                        webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                        while (true)
                        {
                            if (this.Active_Chunks_Max <= 0)
                            {
                                Thread.Sleep(100);
                            }
                            else
                            {
                                lock (this.Download_Queue)
                                {
                                    if (this.Download_Queue.Count == 0)
                                    {
                                        lock (this.Workers_Live)
                                        {
                                            this.Workers_Live.Remove((BackgroundWorker)sender);
                                        }
                                        this.Worker_Count--;
                                        break;
                                    }
                                }
                                string value = string.Empty;
                                lock (this.Download_Queue)
                                {
                                    value = this.Download_Queue.Last.Value;
                                    this.Download_Queue.RemoveLast();
                                    lock (this.Free_ChunksLock)
                                    {
                                        this.Active_Chunks_Max--;
                                    }
                                }
                                lock (this.Download_List[value])
                                {
                                    if (this.Download_List[value].Status != Download_Status.Canceled)
                                    {
                                        this.Download_List[value].Status = Download_Status.Downloading;
                                    }
                                }
                                while (webClient.IsBusy)
                                {
                                    Thread.Sleep(100);
                                }
                                webClient.DownloadDataAsync(new Uri(value), value);
                                Download_Status status = Download_Status.Downloading;
                                while (status == Download_Status.Downloading)
                                {
                                    status = this.Download_List[value].Status;
                                    if (status == Download_Status.Canceled)
                                    {
                                        break;
                                    }
                                    Thread.Sleep(100);
                                }
                                if (status == Download_Status.Canceled)
                                {
                                    webClient.CancelAsync();
                                }
                                lock (this.Workers_Live)
                                {
                                    if (Worker_Count > this.Workers_Max || !this.Manager_Running)
                                    {
                                        this.Workers_Live.Remove((BackgroundWorker)sender);
                                        Worker_Count--;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (WebClientWithTimeout webClient = new WebClientWithTimeout())
                    {
                        webClient.DownloadDataCompleted += new DownloadDataCompletedEventHandler(this.DownloadManager_DownloadDataCompleted);
                        webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                        while (true)
                        {
                            if (this.Active_Chunks_Max <= 0)
                            {
                                Thread.Sleep(100);
                            }
                            else
                            {
                                lock (this.Download_Queue)
                                {
                                    if (this.Download_Queue.Count == 0)
                                    {
                                        lock (this.Workers_Live)
                                        {
                                            this.Workers_Live.Remove((BackgroundWorker)sender);
                                        }
                                        Worker_Count--;
                                        break;
                                    }
                                }
                                string value = string.Empty;
                                lock (this.Download_Queue)
                                {
                                    value = this.Download_Queue.Last.Value;
                                    this.Download_Queue.RemoveLast();
                                    lock (this.Free_ChunksLock)
                                    {
                                        this.Active_Chunks_Max--;
                                    }
                                }
                                lock (this.Download_List[value])
                                {
                                    if (this.Download_List[value].Status != Download_Status.Canceled)
                                    {
                                        this.Download_List[value].Status = Download_Status.Downloading;
                                    }
                                }
                                while (webClient.IsBusy)
                                {
                                    Thread.Sleep(100);
                                }
                                webClient.DownloadDataAsync(new Uri(value), value);
                                Download_Status status = Download_Status.Downloading;
                                while (status == Download_Status.Downloading)
                                {
                                    status = this.Download_List[value].Status;
                                    if (status == Download_Status.Canceled)
                                    {
                                        break;
                                    }
                                    Thread.Sleep(100);
                                }
                                if (status == Download_Status.Canceled)
                                {
                                    webClient.CancelAsync();
                                }
                                lock (this.Workers_Live)
                                {
                                    if (Worker_Count > this.Workers_Max || !this.Manager_Running)
                                    {
                                        this.Workers_Live.Remove((BackgroundWorker)sender);
                                        Worker_Count--;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                lock (this.Workers_Live)
                {
                    this.Workers_Live.Remove((BackgroundWorker)sender);
                    Worker_Count--;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void CancelAllDownloads()
        {
            this.Stop();
            lock (this.Download_Queue)
            {
                this.Download_Queue.Clear();
            }
            foreach (string key in this.Download_List.Keys)
            {
                lock (this.Download_List[key])
                {
                    if (this.Download_List[key].Data != null)
                    {
                        lock (this.Free_ChunksLock)
                        {
                            this.Active_Chunks_Max++;
                        }
                    }
                    this.Download_List[key].Status = Download_Status.Canceled;
                    this.Download_List[key].Data = null;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void CancelDownload(string fileName)
        {
            lock (this.Download_Queue)
            {
                if (this.Download_Queue.Contains(fileName))
                {
                    this.Download_Queue.Remove(fileName);
                }
            }
            if (this.Download_List.ContainsKey(fileName))
            {
                lock (this.Download_List[fileName])
                {
                    if (this.Download_List[fileName].Data != null)
                    {
                        lock (this.Free_ChunksLock)
                        {
                            this.Active_Chunks_Max++;
                        }
                    }
                    this.Download_List[fileName].Status = Download_Status.Canceled;
                    this.Download_List[fileName].Data = null;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            this.CancelAllDownloads();
            while (Worker_Count > 0)
            {
                Thread.Sleep(100);
            }
            lock (this.Download_List)
            {
                this.Download_List.Clear();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownloadManager_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            string str = e.UserState.ToString();
            if (e.Cancelled || e.Error != null)
            {
                if (e.Error != null)
                {
                    if (!string.IsNullOrWhiteSpace(str)) 
                    {
                        if (this.Download_List.ContainsKey(str))
                        {
                            lock (this.Download_List[str])
                            {
                                if (this.Download_List[str].Status == Download_Status.Canceled || this.Workers_Max <= 1)
                                {
                                    this.Download_List[str].Data = default;
                                    this.Download_List[str].Status = Download_Status.Canceled;
                                }
                                else
                                {
                                    this.Download_List[str].Data = default;
                                    this.Download_List[str].Status = Download_Status.Queued;
                                    lock (this.Download_Queue)
                                    {
                                        this.Download_Queue.AddLast(str);
                                    }
                                    lock (this.Workers_Live)
                                    {
                                        this.Workers_Max--;
                                    }
                                }
                            }
                        }
                    }
                }
                lock (this.Free_ChunksLock)
                {
                    this.Active_Chunks_Max++;
                }
            }
            else if (!string.IsNullOrWhiteSpace(str))
            {
                lock (this.Download_List[str])
                {
                    if (this.Download_List[str].Status != Download_Status.Downloaded)
                    {
                        this.Download_List[str].Data = new byte[(int)e.Result.Length];
                        Buffer.BlockCopy(e.Result, 0, this.Download_List[str].Data, 0, (int)e.Result.Length);
                        this.Download_List[str].Status = Download_Status.Downloaded;
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public byte[]? GetFile(string fileName)
        {
            Download_Status status;
            byte[]? data = null;
            this.ScheduleFile(fileName);
            lock (this.Download_List[fileName])
            {
                status = this.Download_List[fileName].Status;
            }
            while (status != Download_Status.Downloaded && status != Download_Status.Canceled)
            {
                Thread.Sleep(100);
                lock (this.Download_List[fileName])
                {
                    status = this.Download_List[fileName].Status;
                }
            }
            if (this.Download_List[fileName].Status == Download_Status.Downloaded)
            {
                lock (this.Download_List[fileName])
                {
                    data = this.Download_List[fileName].Data;
                    this.Download_List[fileName].Data = null;
                    lock (this.Free_ChunksLock)
                    {
                        this.Active_Chunks_Max++;
                    }
                }
            }

            return data;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public Download_Status GetStatus(string fileName)
        {
            if (!this.Download_List.ContainsKey(fileName))
            {
                return Download_Status.Unknown;
            }
            else
            {
                return this.Download_List[fileName].Status;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="serverPath"></param>
        public void Initialize(XmlDocument doc, string serverPath)
        {
            this.Free_ChunksLock = new object();
            int num = 0;
            foreach (XmlNode xmlNodes in doc.SelectNodes("/index/fileinfo"))
            {
                if (xmlNodes.SelectSingleNode("section") == null)
                {
                    continue;
                }
                num = int.Parse(xmlNodes.SelectSingleNode("section").InnerText);
            }
            for (int i = 1; i <= num; i++)
            {
                string str1 = string.Format("{0}/section{1}.dat", serverPath, i);
                if (!this.Download_List.ContainsKey(str1))
                {
                    this.Download_List.Add(str1, new DownloadItem());
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void ScheduleFile(string fileName)
        {
            if (this.Download_List.ContainsKey(fileName))
            {
                if (this.Download_List[fileName].Status != Download_Status.Queued && 
                    this.Download_List[fileName].Status != Download_Status.Canceled)
                {
                    return;
                }
                lock (this.Download_Queue)
                {
                    if (this.Download_Queue.Contains(fileName) && this.Download_Queue.Last.Value != fileName)
                    {
                        this.Download_Queue.Remove(fileName);
                        this.Download_Queue.AddLast(fileName);
                    }
                    else if (!this.Download_Queue.Contains(fileName))
                    {
                        this.Download_Queue.AddLast(fileName);
                    }
                }
                lock (this.Download_List[fileName])
                {
                    this.Download_List[fileName].Status = Download_Status.Queued;
                }
            }
            else
            {
                this.Download_List.Add(fileName, new DownloadItem());
                lock (this.Download_Queue)
                {
                    this.Download_Queue.AddLast(fileName);
                }
            }
            if (this.Manager_Running && Worker_Count < this.Workers_Max)
            {
                lock (this.Workers_Live)
                {
                    BackgroundWorker backgroundWorker = new BackgroundWorker();
                    backgroundWorker.DoWork += new DoWorkEventHandler(this.BackgroundWorker_DoWork);
                    backgroundWorker.RunWorkerAsync();
                    this.Workers_Live.Add(backgroundWorker);
                    Worker_Count++;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            this.Manager_Running = true;
            lock (this.Workers_Live)
            {
                while (Worker_Count < this.Workers_Max)
                {
                    BackgroundWorker backgroundWorker = new BackgroundWorker();
                    backgroundWorker.DoWork += new DoWorkEventHandler(this.BackgroundWorker_DoWork);
                    backgroundWorker.RunWorkerAsync();
                    this.Workers_Live.Add(backgroundWorker);
                    Worker_Count++;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            this.Manager_Running = false;
        }
        /// <summary>
        /// 
        /// </summary>
        private class DownloadItem
        {
            /// <summary>
            /// 
            /// </summary>
            public Download_Status Status;
            /// <summary>
            /// 
            /// </summary>
            private byte[]? _data;
            /// <summary>
            /// 
            /// </summary>
            public byte[]? Data
            {
                get { return this._data; }
                set { this._data = value; }
            }
            /// <summary>
            /// 
            /// </summary>
            public DownloadItem()
            {
                this.Status = Download_Status.Queued;
                this.Data = default;
            }
        }
    }
}
