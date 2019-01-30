using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

namespace Negri.Wot
{
    public class FtpPutter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FtpPutter));

        private readonly string _url;
        private readonly string _user;
        private readonly string _password;

        public FtpPutter(string url, string user, string password)
        {
            _url = url;
            _user = user;
            _password = password;
        }

        private static void Execute(Action action, int maxTries = 10)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    
                    if (i < (maxTries - 1))
                    {
                        Log.Warn(ex);
                        Log.Warn("...esperando antes de tentar novamente.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }
                    else
                    {
                        Log.Error(ex);
                    }
                    lastException = ex;
                }
            }
            if (lastException != null)
            {
                throw lastException;
            }
        }

        /// <summary>
        /// Apaga um conjunto de arquivos
        /// </summary>
        public void DeleteFile(string file)
        {
            Execute(() =>
            {
                var url = _url + file;
                Log.DebugFormat("Deletando '{0}'...", url);

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
                request.Method = WebRequestMethods.Ftp.DeleteFile;

                request.Credentials = new NetworkCredential(_user, _password);

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Log.DebugFormat("...status: {0}", response.StatusDescription);
                response.Close();
            });            
        }

        public IEnumerable<string> List(string subDir = null, string filter = null)
        {
            
            Exception lastException = new ApplicationException("Erro no controle de fluxo!");
            const int maxTry = 10;
            for (var i = 0; i < maxTry; ++i)
            {
                try
                {
                    var url = _url;
                    if (!string.IsNullOrWhiteSpace(subDir))
                    {
                        url = _url + $"{subDir}/";
                    }

                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
                    request.Method = WebRequestMethods.Ftp.ListDirectory;

                    request.Credentials = new NetworkCredential(_user, _password);

                    FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                    Stream responseStream = response.GetResponseStream();
                    Debug.Assert(responseStream != null, "responseStream != null");
                    StreamReader reader = new StreamReader(responseStream);
                    var content = reader.ReadToEnd();
                    reader.Close();
                    response.Close();

                    Log.InfoFormat("Lido o conteúdo.");

                    var res = content.Split(new [] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).Where(s => !string.IsNullOrWhiteSpace(s));
                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        res = res.Where(s => s.ToLowerInvariant().Contains(filter.ToLowerInvariant()));
                    }
                    return res.ToArray();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    if (i < (maxTry - 1))
                    {
                        Log.Warn("...esperando antes de tentar novamente.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }
                    lastException = ex;
                }
            }
            throw lastException;
        }

        private void Put(string destiny, byte[] content)
        {
            Log.InfoFormat("Destino é '{0}' e deverá ter {1} bytes...", destiny, content.Length);

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(destiny);
            request.Method = WebRequestMethods.Ftp.UploadFile;

            request.Credentials = new NetworkCredential(_user, _password);

            request.ContentLength = content.Length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(content, 0, content.Length);
            requestStream.Close();

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            Log.InfoFormat("Upload File Complete, status {0}", response.StatusDescription.TrimEnd());

            response.Close();
        }

        public void PutClan(string clanFile)
        {
            Log.InfoFormat("Iniciando o upload FTP de '{0}'...", clanFile);

            var fi = new FileInfo(clanFile);
            var fileName = fi.Name;
            byte[] content = File.ReadAllBytes(clanFile);
            var destiny = _url + "Clans/" + fileName;
            
            Execute(() =>
            {
                Put(destiny, content);                
            });
        }

        public void SetRenameFile(string oldTag, string newTag)
        {
            byte[] content = Encoding.UTF8.GetBytes(newTag);
            var destiny = _url + "Renames/" + oldTag + ".ren.txt";
            Execute(() =>
            {
                Put(destiny, content);
            });
        }

        public int DeleteOldFiles(int daysToKeepOnDelete, string subdir = null, string filter = null)
        {
            if (daysToKeepOnDelete < 7)
            {
                throw new ArgumentOutOfRangeException(nameof(daysToKeepOnDelete), daysToKeepOnDelete, @"devem ser mantidos pelo menos 7 dias");
            }

            var regex = new Regex(@"\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);

            int deleted = 0;
            var files = List(subdir, filter).ToArray();
            foreach (var file in files)
            {                
                var m = regex.Match(file);
                if (m.Success)
                {
                    DateTime date = DateTime.ParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    double age = (DateTime.UtcNow.Date - date).TotalDays;
                    if (age > daysToKeepOnDelete)
                    {
                        string toDelete = file;
                        if (!string.IsNullOrWhiteSpace(subdir))
                        {
                            toDelete = subdir + "/" + file;
                        }
                        DeleteFile(toDelete);
                        ++deleted;
                    }
                }
            }

            return deleted;
        }

        public void PutMoe(string moeFile)
        {
            Log.InfoFormat("Iniciando o upload FTP de '{0}'...", moeFile);

            var fi = new FileInfo(moeFile);
            var fileName = fi.Name;
            byte[] content = File.ReadAllBytes(moeFile);
            var destiny = _url + "MoE/" + fileName;

            Execute(() =>
            {
                Put(destiny, content);
            });
        }

        public void PutTankReference(string tankFile)
        {
            Log.InfoFormat("Iniciando o upload FTP de '{0}'...", tankFile);

            var fi = new FileInfo(tankFile);
            var fileName = fi.Name;
            byte[] content = File.ReadAllBytes(tankFile);
            var destiny = _url + "Tanks/" + fileName;

            Execute(() =>
            {
                Put(destiny, content);
            });
        }

        
    }
}