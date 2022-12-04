using System.Net.Cache;
using System.Net;
using System.Text;
using System.Xml;
using SBRW.Launcher.Core.Extension.Time_;
using SBRW.Launcher.Core.Extension.Numbers_;
using SBRW.Launcher.RunTime.LauncherCore.Languages.Visual_Forms;
using System;
using System.IO;

namespace SBRW.Launcher.Core.Downloader.LZMA.Debug
{
    internal class Program
    {
        public static Download_LZMA_Data? LZMA_Downloader { get; set; }
        private static string GameFolderPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameFiles"); } }
        private static string Launcher_CDN { get; set; } = "http://g2-sbrw.davidcarbon.download";
        private static DateTime? DownloadStartTime { get; set; }

        static void Main(string[] args)
        {
            try
            {
                LZMA_Downloader = new Download_LZMA_Data(3, 2, 16, DownloadStartTime ?? DateTime.Now)
                {
                    Progress_Update_Frequency = 800
                };

                LZMA_Downloader.Internal_Error += (x, D_Live_Events) =>
                {
                    if (D_Live_Events.Recorded_Exception != null && LZMA_Downloader.Downloading)
                    {
                        Console.WriteLine(D_Live_Events.Recorded_Exception);
                        OnDownloadFailed(D_Live_Events.Recorded_Exception);
                    }
                };

                LZMA_Downloader.Complete += (X_Input, Live_Data) =>
                {
                    if (Live_Data.Complete)
                    {
                        OnDownloadFinished();
                    }
                };

                LZMA_Downloader.Live_Progress += (X_Input, Live_Data) =>
                {
                    if (LZMA_Downloader != null)
                    {
                        if (LZMA_Downloader.Downloading)
                        {
                            decimal Calulated_Division = 0;

                            try
                            {
                                Calulated_Division = decimal.Divide(Live_Data.Bytes_Received, Live_Data.Bytes_To_Receive_Total);
                            }
                            catch
                            {

                            }

                            try
                            {
                                Console.WriteLine(string.Format("{0} of {1} ({3}%) — {2}", Time_Conversion.FormatFileSize(Live_Data.Bytes_Received),
                                Time_Conversion.FormatFileSize(Live_Data.Bytes_To_Receive_Total), Time_Conversion.EstimateFinishTime(Live_Data.Bytes_Received, Live_Data.Bytes_To_Receive_Total,
                                Live_Data.Start_Time), Math_Core.Clamp(Math.Round(Calulated_Division * 100, 0), 0, 100)));
                            }
                            catch
                            {
                                /* Ignore Exception */
                            }
                        }
                    }
                };

                Console.WriteLine("Checking Core Files...".ToUpper());

                string GameExePath = Path.Combine(GameFolderPath, "nfsw.exe");

                if (!File.Exists(GameExePath) && LZMA_Downloader != null)
                {
                    DownloadStartTime = DateTime.Now;
                    Console.WriteLine("Downloading: Core GameFiles".ToUpper());
                    LZMA_Downloader.StartDownload(Launcher_CDN, string.Empty, GameFolderPath, false, false, 1130632198);
                    while (LZMA_Downloader.Downloading)
                    {

                    }
                }
                else
                {
                    DownloadTracksFiles();
                }
            }
            catch (Exception Error)
            {
                Console.WriteLine(Error.ToString());
            }
        }

        public static void DownloadTracksFiles()
        {
            Console.WriteLine("Checking Tracks Files...".ToUpper());

            string SpecificTracksFilePath = Path.Combine(GameFolderPath, "Tracks", "STREAML5RA_98.BUN");
            if (!File.Exists(SpecificTracksFilePath) && (LZMA_Downloader != null))
            {
                DownloadStartTime = DateTime.Now;
                Console.WriteLine("Downloading: Tracks Data".ToUpper());
                LZMA_Downloader.StartDownload(Launcher_CDN, "Tracks", GameFolderPath, false, false, 615494528);
            }
            else
            {
                DownloadSpeechFiles();
            }
        }

        public static void DownloadSpeechFiles()
        {
            if (true)
            {
                string speechFile = string.Empty;
                int speechSize = 0;

                Console.WriteLine("Looking for correct Speech Files...".ToUpper());

                try
                {
                    speechFile = Download_LZMA_Support.SpeechFiles("en");

                    Uri URLCall = new Uri(Launcher_CDN + "/" + speechFile + "/index.xml");
                    ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds;
                    var Client = new WebClient
                    {
                        Encoding = Encoding.UTF8,
                        CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
                    };

                    Client.Headers.Add("user-agent", "SBRW Launcher (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)");

                    try
                    {
                        string response = Client.DownloadString(URLCall);

                        XmlDocument speechFileXml = new XmlDocument();
                        speechFileXml.LoadXml(response);

                        if (speechFileXml != default)
                        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                            XmlNode speechSizeNode = speechFileXml.SelectSingleNode("index/header/compressed");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            speechSize = Convert.ToInt32(speechSizeNode.InnerText);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        }
                        else
                        {
                            speechFile = Translations.Speech_Files("en");
                            speechSize = Translations.Speech_Files_Size();
                        }
                    }
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        if (Client != null)
                        {
                            Client.Dispose();
                        }
                    }
                }
                catch (Exception Error)
                {
                    Console.WriteLine(Error.ToString());
                    speechFile = Translations.Speech_Files("en");
                    speechSize = Translations.Speech_Files_Size();
                }

                Console.WriteLine(string.Format("Checking for {0} Speech Files.", speechFile).ToUpper());

                string SoundSpeechPath = Path.Combine(GameFolderPath, "Sound", "Speech", "copspeechsth_" + speechFile + ".big");
                if (!File.Exists(SoundSpeechPath) && LZMA_Downloader != null)
                {
                    DownloadStartTime = DateTime.Now;
                    Console.WriteLine("Downloading: Language Audio".ToUpper());
                    LZMA_Downloader.StartDownload(Launcher_CDN, speechFile, GameFolderPath, false, false, speechSize);
                }
                else
                {
                    OnDownloadFinished();
                    Console.WriteLine("DOWNLOAD: Game Files Download is Complete!");
                }
            }
            else
            {
#if !(RELEASE_UNIX || DEBUG_UNIX)
                GC.Collect();
#endif
            }
        }

        private static void OnDownloadFinished()
        {
            try
            {
                if (LZMA_Downloader != null)
                {
                    if (LZMA_Downloader.Downloading)
                    {
                        LZMA_Downloader.Stop();
                    }
                }
            }
            catch (Exception Error_Live)
            {
                Console.WriteLine(Error_Live.ToString());
            }
        }

        private static void OnDownloadFailed(Exception Error)
        {
            try
            {
                if (LZMA_Downloader != null)
                {
                    LZMA_Downloader.Stop();
                }
            }
            catch (Exception Error_Live)
            {
                Console.WriteLine(Error_Live.ToString());
            }
        }
    }
}