﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace SBRW.Launcher.Core.Downloader.LZMA
{
    /// <summary>
    /// 
    /// </summary>
    public class Download_LZMA_Data_Hash
    {
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, Download_LZMA_Data_Hash_Tuple> File_List { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Queue<string> Queue_Hash { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public object Queue_Hash_Lock { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int Worker_Count { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool Use_Cache { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Download_LZMA_Data_Hash()
        {
            Queue_Hash_Lock = new object();
            Worker_Count = 0;
            Use_Cache = true;
            File_List = new Dictionary<string, Download_LZMA_Data_Hash_Tuple>();
            Queue_Hash = new Queue<string>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="P_Worker_Count"></param>
        public Download_LZMA_Data_Hash(int P_Worker_Count)
        {
            Queue_Hash_Lock = new object();
            Worker_Count = P_Worker_Count;
            Use_Cache = true;
            File_List = new Dictionary<string, Download_LZMA_Data_Hash_Tuple>();
            Queue_Hash = new Queue<string>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="P_Worker_Count"></param>
        /// <param name="P_Use_Cache"></param>
        public Download_LZMA_Data_Hash(int P_Worker_Count, bool P_Use_Cache)
        {
            Queue_Hash_Lock = new object();
            Worker_Count = P_Worker_Count;
            Use_Cache = P_Use_Cache;
            File_List = new Dictionary<string, Download_LZMA_Data_Hash_Tuple>();
            Queue_Hash = new Queue<string>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="P_Worker_Count"></param>
        /// <param name="P_Use_Cache"></param>
        /// <param name="P_File_List"></param>
        public Download_LZMA_Data_Hash(int P_Worker_Count, bool P_Use_Cache, Dictionary<string, Download_LZMA_Data_Hash_Tuple> P_File_List)
        {
            Queue_Hash_Lock = new object();
            Worker_Count = P_Worker_Count;
            Use_Cache = P_Use_Cache;
            File_List = P_File_List;
            Queue_Hash = new Queue<string>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="P_Worker_Count"></param>
        /// <param name="P_Use_Cache"></param>
        /// <param name="P_File_List"></param>
        /// <param name="P_Queue_Hash"></param>
        public Download_LZMA_Data_Hash(int P_Worker_Count, bool P_Use_Cache, Dictionary<string, Download_LZMA_Data_Hash_Tuple> P_File_List, Queue<string> P_Queue_Hash)
        {
            this.Queue_Hash_Lock = new object();
            this.Use_Cache = P_Use_Cache;
            this.File_List = P_File_List;
            this.Queue_Hash = P_Queue_Hash;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs args)
        {
            while (true)
            {
                lock (Queue_Hash_Lock)
                {
                    if (this.Queue_Hash.Count == 0)
                    {
                        Worker_Count--;
                        break;
                    }
                }
                string str = string.Empty;
                lock (Queue_Hash_Lock)
                {
                    str = this.Queue_Hash.Dequeue();
                }
                string base64String = string.Empty;
                if (File.Exists(str))
                {
                    if (string.IsNullOrWhiteSpace(this.File_List[str].Old))
                    {
                        try
                        {
                            using (FileStream fileStream = File.OpenRead(str))
                            {
                                using (MD5 mD5 = MD5.Create())
                                {
                                    base64String = Convert.ToBase64String(mD5.ComputeHash(fileStream));
                                }
                            }
                        }
                        catch (Exception) 
                        { 

                        }
                    }
                    else
                    {
                        base64String = this.File_List[str].Old;
                    }
                }
                lock (this.File_List[str])
                {
                    this.File_List[str].Old = base64String;
                }
            }
        }

        private void BackgroundWorker_RunWorkerComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            lock (this.Queue_Hash)
            {
                this.Queue_Hash.Clear();
            }
            while (Worker_Count > 0)
            {
                Thread.Sleep(100);
            }
            this.File_List.Clear();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string GetHashOld(string fileName)
        {
            string empty = string.Empty;
            while (true)
            {
                lock (this.File_List[fileName])
                {
                    empty = this.File_List[fileName].Old;
                }
                if (empty != string.Empty)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            return empty;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool HashesMatch(string fileName)
        {
            bool @new;
            try
            {
                while (true)
                {
                    lock (this.File_List[fileName])
                    {
                        if (this.File_List[fileName].Old != string.Empty)
                        {
                            @new = this.File_List[fileName].New == this.File_List[fileName].Old;
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return @new;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="patchPath"></param>
        /// <param name="hashFileNameSuffix"></param>
        /// <param name="maxWorkers"></param>
        public void Start(XmlDocument doc, string patchPath, string hashFileNameSuffix, int maxWorkers)
        {
            foreach (XmlNode xmlNodes in doc.SelectNodes("/index/fileinfo"))
            {
                string innerText = xmlNodes.SelectSingleNode("path").InnerText;
                string str = xmlNodes.SelectSingleNode("file").InnerText;
                if (!string.IsNullOrWhiteSpace(patchPath))
                {
                    int num = innerText.IndexOf("/");
                    innerText = (num < 0 ? patchPath : innerText.Replace(innerText.Substring(0, num), patchPath));
                }
                string str1 = string.Concat(innerText, "/", str);
                if (xmlNodes.SelectSingleNode("hash") != null)
                {
                    this.File_List.Add(str1, new Download_LZMA_Data_Hash_Tuple(string.Empty, xmlNodes.SelectSingleNode("hash").InnerText));
                    this.Queue_Hash.Enqueue(str1);
                }
                else
                {
                    this.File_List.Add(str1, new Download_LZMA_Data_Hash_Tuple(string.Empty, string.Empty));
                }
            }

            if (this.Use_Cache && File.Exists(string.Concat("HashFile", hashFileNameSuffix)))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(string.Concat("HashFile", hashFileNameSuffix), FileMode.Open))
                    {
                        DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
                        {
                            Key = Encoding.ASCII.GetBytes("12345678"),
                            IV = Encoding.ASCII.GetBytes("12345678")
                        };

                        using (CryptoStream cryptoStream = new CryptoStream(fileStream, dESCryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                string str2 = string.Empty;
                                while (true)
                                {
                                    string str3 = streamReader.ReadLine();
                                    str2 = str3;
                                    if (string.IsNullOrWhiteSpace(str3))
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        string[] strArrays = str2.Split('\t');
                                        string str4 = strArrays[0];
                                        if (this.File_List.ContainsKey(str4) && File.Exists(str4) && long.Parse(strArrays[2]) == (new FileInfo(str4)).LastWriteTime.Ticks)
                                        {
                                            this.File_List[str4].Old = strArrays[1];
                                            this.File_List[str4].Ticks = long.Parse(strArrays[2]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (CryptographicException)
                {
                    this.File_List.Clear();
                }
                catch (Exception)
                {
                    this.File_List.Clear();
                }
            }
            Worker_Count = 0;
            while (Worker_Count < maxWorkers && this.Queue_Hash.Count > 0)
            {
                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += new DoWorkEventHandler(this.BackgroundWorker_DoWork);
                backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(this.BackgroundWorker_RunWorkerComplete);
                backgroundWorker.RunWorkerAsync();
                Worker_Count++;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashFileNameSuffix"></param>
        /// <param name="writeOldHashes"></param>
        public void WriteHashCache(string hashFileNameSuffix, bool writeOldHashes)
        {
            lock (this.File_List)
            {
                try
                {
                    using (FileStream fileStream = File.Create($"HashFile{hashFileNameSuffix}"))
                    {
                        using (DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(fileStream, dESCryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write))
                            {
                                using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                                {
                                    dESCryptoServiceProvider.IV = Encoding.ASCII.GetBytes("12345678");
                                    dESCryptoServiceProvider.Key = dESCryptoServiceProvider.IV;

                                    foreach (string key in this.File_List.Keys)
                                    {
                                        string hash = writeOldHashes ? this.File_List[key].Old : this.File_List[key].New;

                                        if (!File.Exists(key) || string.IsNullOrWhiteSpace(hash))
                                        {
                                            continue;
                                        }

                                        DateTime lastWriteTime = new FileInfo(key).LastWriteTime;
                                        streamWriter.WriteLine($"{key}\t{hash}\t{lastWriteTime.Ticks}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    /* Ignore Exception */
                }
            }
        }
    }
}
