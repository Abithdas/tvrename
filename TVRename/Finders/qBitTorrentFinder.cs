// 
// Main website for TVRename is http://tvrename.com
// 
// Source code available at https://github.com/TV-Rename/tvrename
// 
// Copyright (c) TV Rename. This code is released under GPLv3 https://github.com/TV-Rename/tvrename/blob/master/LICENSE.md
//

using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace TVRename
{
    //See https://github.com/qbittorrent/qBittorrent/wiki/Web-API-Documentation for details
    // ReSharper disable once InconsistentNaming
    public enum qBitTorrentAPIVersion
    {
        v0, //Applies to qBittorrent up to v3.1.x
        v1, //Applies to qBittorrent v3.2.0-v4.0.4
        v2 //Applies to qBittorrent v4.1+ 
    }
    // ReSharper disable once InconsistentNaming
    public enum qBitTorrentAPIPath
    {
        settings,
        torrents,
        torrentDetails,
        addFile,
        addUrl
    }
    // ReSharper disable once InconsistentNaming
    internal class qBitTorrentFinder : DownloadingFinder
    {
        public qBitTorrentFinder(TVDoc i) : base(i) { }
        public override bool Active() => TVSettings.Instance.CheckqBitTorrent;
        [NotNull]
        protected override string Checkname() => "Looked in the qBitTorrent for the missing files to see if they are being downloaded";

        protected override void DoCheck(SetProgressDelegate prog, ICollection<ShowItem> showList,TVDoc.ScanSettings settings)
        {
            List<TorrentEntry> downloading = GetTorrentDownloads();
            SearchForAppropriateDownloads(downloading, DownloadApp.qBitTorrent,settings);
        }

        [NotNull]
        internal static List<TorrentEntry> GetTorrentDownloads()
        {
            List < TorrentEntry >  ret = new List<TorrentEntry>();

            // get list of files being downloaded by qBitTorrentFinder

            if (string.IsNullOrEmpty(TVSettings.Instance.qBitTorrentHost) || string.IsNullOrEmpty(TVSettings.Instance.qBitTorrentPort))
            {
                return ret;
            }
            
            string settingsString = null;
            string downloadsString = null;

            try
            {
                settingsString= JsonHelper.Obtain( GetApiUrl(qBitTorrentAPIPath.settings ) );
                downloadsString = JsonHelper.Obtain(GetApiUrl(qBitTorrentAPIPath.torrents));

                JToken settings = JToken.Parse(settingsString);
                JArray currentDownloads = JArray.Parse(downloadsString);

                if(currentDownloads is null || settings is null)
                {
                    LOGGER.Warn($"Could not get currentDownloads or settings from qBitTorrent: {settingsString} {currentDownloads}");
                    return ret;
                }

                string savePath = (string)settings["save_path"]??string.Empty;

                foreach (JToken torrent in currentDownloads.Children())
                {
                    AddFilesFromTorrent(ret, torrent, savePath);
                }
            }
            catch (WebException wex)
            {
                LOGGER.Warn(
                    $"Could not connect to local instance {TVSettings.Instance.qBitTorrentHost}:{TVSettings.Instance.qBitTorrentPort}, Please check qBitTorrent Settings and ensure qBitTorrent is running with no password required for local connections: {wex.LoggableDetails()}");
            }
            catch (JsonReaderException ex)
            {
                LOGGER.Warn(ex,
                    $"Could not parse data recieved from {settingsString} {downloadsString}");
            }

            return ret;
        }

        private static void AddFilesFromTorrent(ICollection<TorrentEntry> ret, [NotNull] JToken torrent, string savePath)
        {
            string torrentDetailsString=string.Empty;
            try
            {
                (string hashCode, string torrentName) = ExtractTorrentDetails(torrent);

                string url = GetApiUrl(qBitTorrentAPIPath.torrentDetails) + hashCode;
                torrentDetailsString = JsonHelper.Obtain(url);
                JArray torrentDetails = JArray.Parse(torrentDetailsString);

                if (torrentDetails is null)
                {
                    LOGGER.Warn(
                        $"Could not get details of downloads from {url} from qBitTorrent: {torrentDetailsString}");

                    return;
                }

                if (!torrentDetails.Children().Any())
                {
                    string proposedFilename = TVSettings.Instance.FilenameFriendly(savePath + torrentName) +
                                              TVSettings.Instance.VideoExtensionsArray[0];

                    ret.Add(new TorrentEntry(torrentName, proposedFilename, 0));
                    return;
                }

                foreach (JToken file in torrentDetails.Children())
                {
                    (string downloadedFilename, bool isOnHold, int percentComplete) = ExtractTorrentFileDetails(file);

                    if (!downloadedFilename.Contains(".!qB\\.unwanted\\") && !isOnHold)
                    {
                        ret.Add(new TorrentEntry(torrentName, savePath + downloadedFilename, percentComplete));
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                LOGGER.Warn(ex,
                    $"Could not parse data recieved from {torrentDetailsString}");
            }
        }

        private static (string downloadedFilename, bool isOnHold, int percentComplete) ExtractTorrentFileDetails([NotNull] JToken file)
        {
            string downloadedFilename = file["name"].ToString();
            bool isOnHold = file["priority"].Value<int>() == 0;
            int percentComplete = (int)(100 * file["progress"].ToObject<float>());

            return (downloadedFilename, isOnHold, percentComplete);
        }

        private static (string hashCode, string torrentName) ExtractTorrentDetails([NotNull] JToken torrent)
        {
            string hashCode = (string)torrent["hash"];
            string torrentName = (string)torrent["name"];

            return (hashCode,torrentName);
        }

        [NotNull]
        private static string GetApiUrl(qBitTorrentAPIPath path)
        {
            string url = $"http://{TVSettings.Instance.qBitTorrentHost}:{TVSettings.Instance.qBitTorrentPort}/";

            switch (TVSettings.Instance.qBitTorrentAPIVersion)
            {
                case qBitTorrentAPIVersion.v1:
                {
                    switch (path)
                    {
                        case qBitTorrentAPIPath.settings:
                            return url+"query/preferences";

                        case qBitTorrentAPIPath.torrents:
                            return url + "query/torrents?filter=all";

                        case qBitTorrentAPIPath.torrentDetails:
                            return url + "query/propertiesFiles/";

                        case qBitTorrentAPIPath.addFile:
                            return url + "command/upload";

                        case qBitTorrentAPIPath.addUrl:
                            return url + "command/download";

                        default:
                            throw new ArgumentOutOfRangeException(nameof(path), path, null);
                    }
                }

                case qBitTorrentAPIVersion.v2:
                {
                    switch (path)
                    {
                        case qBitTorrentAPIPath.settings:
                            return url + "api/v2/app/preferences";

                        case qBitTorrentAPIPath.torrents:
                            return url + "api/v2/torrents/info?filter=all";

                        case qBitTorrentAPIPath.torrentDetails:
                            return url + "api/v2/torrents/files?hash=";

                        case qBitTorrentAPIPath.addFile:
                            return url + "api/v2/torrents/add";

                        case qBitTorrentAPIPath.addUrl:
                            return url + "api/v2/torrents/add";

                            default:
                            throw new ArgumentOutOfRangeException(nameof(path), path, null);
                    }
                }

                case qBitTorrentAPIVersion.v0:
                    throw new NotSupportedException("Only qBitTorrent API v1 and v2 are supported");
            default:
                    throw new ArgumentOutOfRangeException(nameof(TVSettings.Instance.qBitTorrentAPIVersion), TVSettings.Instance.qBitTorrentAPIVersion, null);
            }
        }

        internal static void StartTorrentDownload(string torrentUrl, string torrentFileName, bool downloadFileFirst)
        {
            if (string.IsNullOrEmpty(TVSettings.Instance.qBitTorrentHost) || string.IsNullOrEmpty(TVSettings.Instance.qBitTorrentPort))
            {
                LOGGER.Warn($"Could not download {torrentUrl} via qBitTorrent as settings are not entered for host and port");
                return;
            }

            if (downloadFileFirst)
            {
                AddFile(torrentFileName,GetApiUrl(qBitTorrentAPIPath.addFile));
            }
            else
            {
                DownloadUrl(torrentUrl, GetApiUrl(qBitTorrentAPIPath.addUrl));
            }
        }

        private static void AddFile(string torrentName, string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    MultipartFormDataContent m = new MultipartFormDataContent();
                    m.AddFile("torrents", torrentName, "application/x-bittorrent");
                    HttpResponseMessage response = client.PostAsync(url, m).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        LOGGER.Warn(
                            $"Tried to download {torrentName} from file to qBitTorrent via {url}. Got following response {response.StatusCode}");
                    }
                    else
                    {
                        LOGGER.Info(
                            $"Started download of {torrentName} via file to qBitTorrent using {url}. Got following response {response.StatusCode}");
                    }
                }
            }
            catch (WebException wex)
            {
                LOGGER.Warn(
                    $"Could not connect to {wex.Response.ResponseUri} to download {torrentName}, Please check qBitTorrent Settings and ensure qBitTorrent is running with no password required for local connections : {wex.Message}");
            }
        }

        private static void DownloadUrl(string torrentUrl, string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    Dictionary<string, string> values = new Dictionary<string, string> {{"urls", torrentUrl}};
                    FormUrlEncodedContent content = new FormUrlEncodedContent(values);
                    HttpResponseMessage response = client.PostAsync(url, content).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        LOGGER.Warn(
                            $"Tried to download {torrentUrl} from qBitTorrent via {url}. Got following response {response.StatusCode}");
                    }
                    else
                    {
                        LOGGER.Info(
                            $"Started download of {torrentUrl} via qBitTorrent using {url}. Got following response {response.StatusCode}");
                    }
                }
            }
            catch (WebException wex)
            {
                LOGGER.Warn(
                    $"Could not connect to {wex.Response.ResponseUri} to download {torrentUrl}, Please check qBitTorrent Settings and ensure qBitTorrent is running with no password required for local connections : {wex.LoggableDetails()}");
            }
        }
    }
}
