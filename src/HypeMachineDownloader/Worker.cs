using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using HypeMachineDownloader.Models;
using Newtonsoft.Json;
using NLog;

namespace HypeMachineDownloader
{
    public class Worker
    {
        private static readonly Logger Logger = LogManager.GetLogger("HypeMachineDownloader");

        private readonly Stopwatch Watch = Stopwatch.StartNew();
        private CookieContainer Cookies;
        private const string UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko";
        private const string Host = "hypem.com";
        private const string BaseUrl = "http://" + Host;
        // TODO make this configurable
        private const string DownloadsFolder = "downloads";

        public string Account { private get; set; }
        public int Page { private get; set; }
        public int Limit { private get; set; }

        private bool InitSession()
        {
            HtmlNode doc;
            string raw;
            return TryQuery(() =>
            {
                Cookies = new CookieContainer();
                var httpWebRequest = (HttpWebRequest) WebRequest.Create(BaseUrl);
                httpWebRequest.Referer = "https://www.google.ca";
                httpWebRequest.UserAgent = UserAgent;
                httpWebRequest.Method = "GET";
                httpWebRequest.CookieContainer = Cookies;

                var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
                var responseStream = httpWebResponse.GetResponseStream();
                if (responseStream == null) return null;
                raw = new StreamReader(responseStream).ReadToEnd();

                return raw;
            }, out doc, out raw);
        }

        public void Start()
        {
            Logger.Info("Starting for account {0}, page {1}, limit {2}", Account, Page, Limit);

            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePoints = 64;
            ServicePointManager.UseNagleAlgorithm = true;

            if (!InitSession())
            {
                Logger.Error("Could not initialize session");
                return;
            }

            Logger.Info("Session initialized");

            HtmlNode trackListDoc;
            string trackListRaw;
            if (!TryQuery(GetTrackList, out trackListDoc, out trackListRaw))
            {
                Logger.Error("Could not retrieve track list");
                return;
            }

            var trackListJson = trackListDoc.QuerySelector("#displayList-data").InnerHtml;
            var info = JsonConvert.DeserializeObject<InfoModel>(trackListJson);

            if (!Directory.Exists(DownloadsFolder)) Directory.CreateDirectory(DownloadsFolder);
            
            foreach (var track in info.tracks.Take(Limit == 0 ? int.MaxValue : Limit))
            {
                DownloadInfoModel downloadInfo;
                try
                {
                    downloadInfo = GetDownloadUrl(track);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error downloading track {0}", track.name);
                    continue;
                }

                var outputFile = Path.Combine(DownloadsFolder, CleanFileName(track.name + "." + (downloadInfo.ext ?? "mp3")));

                if (File.Exists(outputFile))
                {
                    Logger.Warn("File exists, skipping track {0}", track.name);
                }
                else
                {
                    Logger.Info("Downloading track {0}", track.name);

                    var outputStream = Download(downloadInfo);

                    var stream = File.Create(outputFile);
                    outputStream.CopyTo(stream);
                    stream.Close();
                    stream.Dispose();
                    outputStream.Close();
                }
            }

            Logger.Info("Finished");
        }

        private static string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        private bool TryQuery(Func<string> query, out HtmlNode doc, out string raw)
        {
            raw = "";
            doc = null;
            var success = false;

            for (var tries = 0; tries < 10; tries++)
            {
                int sleepMS;
                var start = Watch.ElapsedMilliseconds;
                raw = query();

                if (!string.IsNullOrEmpty(raw))
                {
                    var html = new HtmlDocument();
                    html.LoadHtml(raw);
                    doc = html.DocumentNode;

                    success = true;
                    var took = Watch.ElapsedMilliseconds - start;
                    Thread.Sleep((int) Math.Min(took + 500, 10000));
                    break;
                }

                Logger.Error("No response for query {0}", query);
                Logger.Info("Retry: {1}", tries);
                switch (tries)
                {
                    case 0:
                        sleepMS = 10000;
                        break;
                    case 1:
                        sleepMS = 60000;
                        break;
                    default:
                        sleepMS = tries*30000;
                        break;
                }
                Thread.Sleep(sleepMS);
            }

            return success;
        }

        private string GetTrackList()
        {
            try
            {
                var httpWebRequest = (HttpWebRequest) WebRequest.Create(BaseUrl + "/" + Account + "/" + Page);
                httpWebRequest.Referer = BaseUrl;
                httpWebRequest.UserAgent = UserAgent;
                httpWebRequest.Method = "GET";
                httpWebRequest.CookieContainer = Cookies;

                var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
                var responseStream = httpWebResponse.GetResponseStream();
                if (responseStream == null) return null;
                var raw = new StreamReader(responseStream).ReadToEnd();

                return raw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetTrackList() exception");
            }

            return null;
        }

        private DownloadInfoModel GetDownloadUrl(TrackModel track)
        {
            var url = "http://hypem.com/serve/source/" + track.id + "/" + track.key;

            var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.Referer = BaseUrl + "/" + Account + "/" + Page;
            httpWebRequest.UserAgent = UserAgent;
            httpWebRequest.Method = "GET";
            httpWebRequest.CookieContainer = Cookies;
            httpWebRequest.ContentType = "application/json";

            var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
            var responseStream = httpWebResponse.GetResponseStream();
            if (responseStream == null) return null;
            var raw = new StreamReader(responseStream).ReadToEnd();

            if (string.IsNullOrEmpty(raw))
            {
                throw new Exception("No response");
            }

            var downloadInfo = JsonConvert.DeserializeObject<DownloadInfoModel>(raw);

            if (downloadInfo == null)
            {
                throw new Exception("Could not parse download info");
            }

            return downloadInfo;
        }

        private Stream Download(DownloadInfoModel info)
        {
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(info.url);
            httpWebRequest.UserAgent = UserAgent;
            httpWebRequest.Method = "GET";
            httpWebRequest.CookieContainer = Cookies;

            var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
            return httpWebResponse.GetResponseStream();
        }
    }
}