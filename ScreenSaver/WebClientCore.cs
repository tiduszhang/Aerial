using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Aerial
{
    [System.Runtime.InteropServices.ComVisible(true)]
    [System.ComponentModel.DesignerCategory("Code")]
    [System.ComponentModel.Designer("Code")]
    internal class WebClientCore : WebClient
    {
        /// <summary>
        /// 设置超时时间(毫秒)
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// 断点续接位置（一般用于下载文件）
        /// </summary>
        public long Seek { get; set; }

        /// <summary>
        /// 获取请求
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            //if (Timeout <= 0)
            //{
            //    Timeout = 3000;
            //}
            try
            {
                WebRequest request = base.GetWebRequest(address);
                if (request is HttpWebRequest)
                {
                    HttpWebRequest httpRequest = request as HttpWebRequest;

                    if (Seek > 0)
                    {
                        httpRequest.AddRange(Seek);
                    }

                    if (Timeout > 0)
                    {
                        httpRequest.Timeout = Timeout;
                        httpRequest.ReadWriteTimeout = Timeout;
                    }
                    return httpRequest;
                }
                else if (request is FtpWebRequest)
                {
                    var url = address.ToString().Substring(0, address.ToString().LastIndexOf(@"/") + 1);
                    CreateDirectory(url);
                    //if (fileCheckExist(url, address.ToString().Replace(url, "")))
                    //{
                    //    fileDelete(address.ToString());
                    //}
                    var ftpRequest = base.GetWebRequest(address) as FtpWebRequest;

                    ftpRequest.KeepAlive = false;
                    if (Timeout > 0)
                    {
                        ftpRequest.Timeout = Timeout;
                        ftpRequest.ReadWriteTimeout = Timeout;
                    }

                    return ftpRequest;
                }
                else if (Timeout > 0)
                {
                    request.Timeout = Timeout;
                }
                return request;
            }
            catch (Exception ex)
            {
                ex.ToString();
            }

            return base.GetWebRequest(address);
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="remoteDirectory"></param>
        private void CreateDirectory(string remoteDirectory)
        {
            remoteDirectory = remoteDirectory.Replace("\\", "/");
            string[] directors = remoteDirectory.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string parentDirector = directors[0] + @"//" + directors[1] + "/";
            for (int i = 2; i < directors.Length; i++)
            {
                parentDirector += directors[i];
                if (!parentDirector.EndsWith("/"))
                {
                    parentDirector += "/";
                }

                FtpWebRequest ftpRequestListDirectoryDetails = base.GetWebRequest(new Uri(parentDirector)) as FtpWebRequest;
                try
                {
                    ftpRequestListDirectoryDetails.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                    FtpWebResponse response = (FtpWebResponse)ftpRequestListDirectoryDetails.GetResponse();
                    response.Close();
                }
                catch (Exception exListDirectory)
                {
                    exListDirectory.ToString();

                    try
                    {
                        FtpWebRequest ftpRequestMakeDirectory = base.GetWebRequest(new Uri(parentDirector)) as FtpWebRequest;
                        ftpRequestMakeDirectory.Method = WebRequestMethods.Ftp.MakeDirectory;
                        FtpWebResponse response = (FtpWebResponse)ftpRequestMakeDirectory.GetResponse();
                        response.Close();
                    }
                    catch (Exception exMakeDirectory)
                    {
                        exMakeDirectory.ToString();
                    }
                }
            }

        }

        /// <summary>
        /// 文件存在检查
        /// </summary>
        /// <param name="ftpPath"></param>
        /// <param name="ftpName"></param>
        /// <returns></returns>
        public bool FileCheckExist(string ftpPath, string ftpName)
        {
            bool success = false;
            FtpWebRequest ftpWebRequest = null;
            WebResponse webResponse = null;
            System.IO.StreamReader reader = null;
            try
            {
                ftpWebRequest = base.GetWebRequest(new Uri(ftpPath)) as FtpWebRequest;
                ftpWebRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                webResponse = ftpWebRequest.GetResponse();
                reader = new System.IO.StreamReader(webResponse.GetResponseStream());
                string line = reader.ReadLine();
                while (line != null)
                {
                    if (line == ftpName)
                    {
                        success = true;
                        break;
                    }
                    line = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
                success = false;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
                if (webResponse != null)
                {
                    webResponse.Close();
                }
            }
            return success;
        }

        /// <summary>
        /// 消除文件
        /// </summary>
        /// <param name="ftpPath"></param>
        public bool FileDelete(string ftpPath)
        {
            bool success = false;
            FtpWebRequest ftpWebRequest = null;
            FtpWebResponse ftpWebResponse = null;
            System.IO.Stream ftpResponseStream = null;
            System.IO.StreamReader streamReader = null;
            try
            {
                ftpWebRequest = base.GetWebRequest(new Uri(ftpPath)) as FtpWebRequest;
                ftpWebRequest.Method = WebRequestMethods.Ftp.DeleteFile;
                ftpWebResponse = (FtpWebResponse)ftpWebRequest.GetResponse();
                long size = ftpWebResponse.ContentLength;
                ftpResponseStream = ftpWebResponse.GetResponseStream();
                streamReader = new System.IO.StreamReader(ftpResponseStream);
                string result = String.Empty;
                result = streamReader.ReadToEnd();

                success = true;
            }
            catch (Exception ex)
            {
                ex.ToString();
                success = false;
            }
            finally
            {
                if (streamReader != null)
                {
                    streamReader.Close();
                }
                if (ftpResponseStream != null)
                {
                    ftpResponseStream.Close();
                }
                if (ftpWebResponse != null)
                {
                    ftpWebResponse.Close();
                }
            }
            return success;
        }

    }
}
