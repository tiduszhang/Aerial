using System.IO;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using System.Threading;

namespace Aerial
{
    public class Caching
    {
        public static string TempFolder = "";
        public static string CacheFolder = new RegSettings().CacheLocation;

        public static int DelayAmount = 1000 * 10; // 10 seconds.
        public static int NumOfCurrentDownloads = 0;



        /// <summary>
        /// Init cache. Clear partially downloaded files from temp folder.
        /// </summary>
        internal static void Setup()
        {
            // If there is no location stored in the Registry, use the default location
            if (string.IsNullOrWhiteSpace(CacheFolder))
            {
                CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aerial");
            }
            TempFolder = Path.Combine(CacheFolder, "temp");

            // Ensure folders exist
            Directory.CreateDirectory(CacheFolder);
            Directory.CreateDirectory(TempFolder);

            // Delete partial temp files if any 
            foreach (var file in Directory.CreateDirectory(TempFolder).GetFiles())
            {
                //file.Delete();
            }
        }

        internal static bool IsHit(string url)
        {
            string filename = Path.GetFileName(url);
            return File.Exists(Path.Combine(CacheFolder, filename));
        }

        internal static bool IsCaching(string url)
        {
            string filename = Path.GetFileName(url);
            return File.Exists(Path.Combine(TempFolder, filename));
        }

        internal static string Get(string url)
        {
            string filename = Path.GetFileName(url);
            return Path.Combine(CacheFolder, filename);
        }

        internal static void StartDelayedCache(string url)
        {
            if (EnsureEnoughSpace())
            {
                Task.Delay(DelayAmount).ContinueWith(t =>
                {
                    DownloadFile(url);
                    //if (!IsCaching(url))
                    //{
                    //    DownloadFile(url);
                    //    //using (WebClient client = new WebClient())
                    //    //{
                    //    //    client.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadFileComplete);
                    //    //    string filename = Path.GetFileName(url);
                    //    //    client.DownloadFileAsync(new Uri(url), Path.Combine(TempFolder, filename), filename);
                    //    //    DownloadStart();
                    //    //}
                    //}
                });
            }
        }



        /// <summary>
        /// 创建WebClient对象(断点续接 ：  一般用于下载文件)
        /// </summary>
        /// <param name="seek"></param>
        /// <returns></returns>
        public static WebClient CreatedWebClient(long seek)
        {
            var webClient = new WebClientCore();

            //设置不验证 
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) =>
            {
                return true;
            });

            webClient.Seek = seek;
            return webClient;
        }


        private static void DownloadFile(string url)
        {
            using (WebClient client = new WebClient())
            {
                string filename = Path.GetFileName(url);
                var filePath = Path.Combine(TempFolder, filename);
                //client.DownloadFileAsync(new Uri(url), Path.Combine(TempFolder, filename), filename);

                FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                var webClient = CreatedWebClient(fileStream.Length);
                try
                {
                    DownloadStart();
                    using (var readStream = webClient.OpenRead(url))
                    {

                        if (fileStream.Length > 0)
                        {
                            fileStream.Seek(fileStream.Length, SeekOrigin.Current);
                        }

                        byte[] buffer = new byte[4096];
                        int i = 0;
                        int l = 0;
                        int b = 0;

                        while ((l = readStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            i = i + l;
                            fileStream.Write(buffer, 0, l);
                            Array.Clear(buffer, 0, buffer.Length);

                            if (b++ % 10 == 0)
                            {
                                fileStream.Flush(true);
                            }
                        }

                        fileStream.Flush(true);
                    }
                }
                catch (Exception ex)
                {
                    ex.ToString();

                    OnDownloadFileComplete(client, new AsyncCompletedEventArgs(ex, false, filename));
                }
                fileStream.Close();
                webClient.Dispose();

                Thread.Sleep(1000);

                OnDownloadFileComplete(client, new AsyncCompletedEventArgs(null, false, filename));
            }
        }



        private static void OnDownloadFileComplete(object sender, AsyncCompletedEventArgs e)
        {
            var filename = e.UserState.ToString();
            var tempFullPath = Path.Combine(TempFolder, filename);
            var cacheFullpath = Path.Combine(CacheFolder, filename);
            if (e.Cancelled == false && e.Error == null)
            {
                FileInfo tempFileInfo = new FileInfo(tempFullPath);
                if (tempFileInfo.Exists && tempFileInfo.Length > 0)
                {
                    // delete if old file exists
                    if (File.Exists(cacheFullpath))
                    {
                        FileInfo cacheFileInfo = new FileInfo(cacheFullpath);

                        if (cacheFileInfo.Length == tempFileInfo.Length)
                        {
                            File.Delete(tempFullPath);
                        }
                        else
                        {
                            File.Copy(cacheFullpath, cacheFullpath + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak");
                            File.Delete(cacheFullpath);
                            File.Move(tempFullPath, cacheFullpath);
                        }
                    }
                }
            }
            else
            {
                // attempt to remove partially downloaded file
                // File.Delete(tempFullPath);
            }

            DownloadEnd();

        }

        internal static async void UpdateCachePath(string oldCacheDirectory, string cacheLocation)
        {
            if (oldCacheDirectory == cacheLocation) return;
            CacheFolder = cacheLocation;

            // Move old cache to new location if space allows
            var currentCacheSpace = GetDirectorySize(oldCacheDirectory);
            if (currentCacheSpace < CacheSpace() - (1000 * 1000 * 1000))
            {
                // Note might take a while, hanging the save dialog
                // video blocks this command: Directory.Move(oldCacheDirectory, cacheLocation);
                foreach (var f in Directory.GetFiles(oldCacheDirectory))
                {
                    var newfile = Path.Combine(cacheLocation, Path.GetFileName(f));
                    if (!File.Exists(newfile))
                        await Task.Factory.StartNew(() => File.Move(f, newfile));

                }
            }

            DeleteCache(oldCacheDirectory);

            // Delete old cache
            try
            {
                await Task.Factory.StartNew(() => Directory.Delete(oldCacheDirectory, true));
            }
            catch (UnauthorizedAccessException)
            {
                // Leave dir for now.
                // todo - windows removes all files after the video player stops using them,
                // yet leaves the folder, we need to redo this operation in 3 mins, for example.
            }
        }

        public static async void DeleteCache(string folder = null)
        {
            if (folder == null) folder = CacheFolder;
            foreach (var f in Directory.GetFiles(folder))
            {
                try
                {
                    await Task.Factory.StartNew(() => File.Delete(f));
                }
                catch (UnauthorizedAccessException ex)
                {
                    // video may be used while deleting
                    Trace.WriteLine("Access denied while moving cached files " + ex);
                }
            }
        }

        public static long CacheSpace()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (CacheFolder.StartsWith(drive.Name))
                    return drive.TotalFreeSpace;
            }
            return 0;
        }

        public static long GetDirectorySize(string path = null)
        {
            if (path == null) path = CacheFolder;
            long size = 0;
            if (Directory.Exists(path))
                foreach (string name in Directory.GetFiles(path, "*.*"))
                    size += new FileInfo(name).Length;

            return size;
        }

        /// <summary>
        ///  Ensures the drive with user folder has more than 1 gig space left.
        /// </summary>
        /// <returns></returns>
        private static bool EnsureEnoughSpace()
        {
            return CacheSpace() > 1000000000;
        }

        public static string TryHit(string url)
        {
            if (IsHit(url))
                return Get(url);
            return url;
        }

        private static void DownloadStart()
        {
            Interlocked.Increment(ref NumOfCurrentDownloads);
        }

        private static void DownloadEnd()
        {
            Interlocked.Decrement(ref NumOfCurrentDownloads);
        }
    }
}