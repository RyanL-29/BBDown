using ICSharpCode.SharpZipLib.GZip;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static BBDown.BBDownEntity;
using static BBDown.BBDownLogger;

namespace BBDown
{
    static class BBDownUtil
    {
        public static readonly HttpClient AppHttpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        });

        public static async Task CheckUpdateAsync()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string nowVer = $"{ver.Major}.{ver.Minor}.{ver.Build}";
                string redirctUrl = await Get302("https://github.com/RyanL-29/BBDown/releases/latest");
                string latestVer = redirctUrl.Replace("https://github.com/RyanL-29/BBDown/releases/tag/", "");
                if (nowVer != latestVer && !latestVer.StartsWith("https"))
                {
                    Console.Title = $"發現新版本：{latestVer}";
                }
            }
            catch (Exception)
            {
                ;
            }
        }

        public static async Task<string> GetAvIdAsync(string input)
        {
            if (input.StartsWith("http"))
            {
                if (input.Contains("b23.tv"))
                    input = await Get302(input);
                if (input.Contains("video/av"))
                {
                    return Regex.Match(input, "av(\\d{1,})").Groups[1].Value;
                }
                else if (input.Contains("video/BV"))
                {
                    return await GetAidByBVAsync(Regex.Match(input, "BV(\\w+)").Groups[1].Value);
                }
                else if (input.Contains("video/bv"))
                {
                    return await GetAidByBVAsync(Regex.Match(input, "bv(\\w+)").Groups[1].Value);
                }
                else if (input.Contains("/cheese/"))
                {
                    string epId = "";
                    if (input.Contains("/ep"))
                    {
                        epId = Regex.Match(input, "/ep(\\d{1,})").Groups[1].Value;
                    }
                    else if(input.Contains("/ss"))
                    {
                        epId = await GetEpidBySSIdAsync(Regex.Match(input, "/ss(\\d{1,})").Groups[1].Value);
                    }
                    return $"cheese:{epId}";
                }
                else if (input.Contains("/ep"))
                {
                    string epId = Regex.Match(input, "/ep(\\d{1,})").Groups[1].Value;
                    return $"ep:{epId}";
                }
                else if (input.Contains("/space.bilibili.com/"))
                {
                    string mid = Regex.Match(input, "space.bilibili.com/(\\d{1,})").Groups[1].Value;
                    return $"mid:{mid}";
                }
                else if (input.Contains("ep_id="))
                {
                    string epId = GetQueryString("ep_id", input);
                    return $"ep:{epId}";
                }
                else if (Regex.IsMatch(input, "www.bilibili.tv/\\w+/play/\\d+/(\\d+)"))
                {
                    string epId = Regex.Match(input, "www.bilibili.tv/\\w+/play/\\d+/(\\d+)").Groups[1].Value;
                    return $"ep:{epId}";
                }
                else
                {
                    string web = await GetWebSourceAsync(input);
                    Regex regex = new Regex("window.__INITIAL_STATE__=([\\s\\S].*?);\\(function\\(\\)");
                    string json = regex.Match(web).Groups[1].Value;
                    using var jDoc = JsonDocument.Parse(json);
                    string epId = jDoc.RootElement.GetProperty("epList").EnumerateArray().First().GetProperty("id").ToString();
                    return $"ep:{epId}";
                }
            }
            else if (input.StartsWith("BV"))
            {
                return await GetAidByBVAsync(input.Substring(2));
            }
            else if (input.StartsWith("bv"))
            {
                return await GetAidByBVAsync(input.Substring(2));
            }
            else if (input.ToLower().StartsWith("av")) //av
            {
                return input.ToLower().Substring(2);
            }
            else if (input.StartsWith("ep"))
            {
                string epId = Regex.Match(input, "ep(\\d{1,})").Groups[1].Value;
                return $"ep:{epId}";
            }
            else if (input.StartsWith("ss"))
            {
                string web = await GetWebSourceAsync("https://www.bilibili.com/bangumi/play/" + input);
                Regex regex = new Regex("window.__INITIAL_STATE__=([\\s\\S].*?);\\(function\\(\\)");
                string json = regex.Match(web).Groups[1].Value;
                using var jDoc = JsonDocument.Parse(json);
                string epId = jDoc.RootElement.GetProperty("epList").EnumerateArray().First().GetProperty("id").ToString();
                return $"ep:{epId}";
            }
            else
            {
                throw new Exception("輸入有誤");
            }
        }

        public static string FormatFileSize(double fileSize)
        {
            if (fileSize < 0)
            {
                throw new ArgumentOutOfRangeException("fileSize");
            }
            else if (fileSize >= 1024 * 1024 * 1024)
            {
                return string.Format("{0:########0.00} GB", ((double)fileSize) / (1024 * 1024 * 1024));
            }
            else if (fileSize >= 1024 * 1024)
            {
                return string.Format("{0:####0.00} MB", ((double)fileSize) / (1024 * 1024));
            }
            else if (fileSize >= 1024)
            {
                return string.Format("{0:####0.00} KB", ((double)fileSize) / 1024);
            }
            else
            {
                return string.Format("{0} bytes", fileSize);
            }
        }

        public static string FormatTime(int time)
        {
            TimeSpan ts = new TimeSpan(0, 0, time);
            string str = "";
            str = (ts.Hours.ToString("00") == "00" ? "" : ts.Hours.ToString("00") + "h") + ts.Minutes.ToString("00") + "m" + ts.Seconds.ToString("00") + "s";
            return str;
        }

        public static async Task<string> GetWebSourceAsync(string url)
        {
            string htmlCode = string.Empty;
            try
            {
                //讀取JSON存放
                string jsonpath = Directory.GetCurrentDirectory();
                string jsonfile = File.ReadAllText($"{jsonpath}/config.json");
                var configjson = JsonDocument.Parse(jsonfile);
                var UA = configjson.RootElement.GetProperty("UserAgent");
                var webRequestClient = new HttpClient();
                using var webRequest = new HttpRequestMessage(HttpMethod.Get, url);
                webRequest.Headers.Add("User-Agent", UA.ToString());
                webRequest.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                var request_cookie = Program.COOKIE.ToString();
                if (!request_cookie.EndsWith(";")) {
                    request_cookie += ";";
                }
                if (url.Contains("/ep") || url.Contains("/ss")) {
                    request_cookie += "CURRENT_FNVAL=4048;";
                }
                    webRequest.Headers.Add("Cookie", request_cookie);
                if (url.Contains("api.bilibili.com/pgc/player/web/playurl") || url.Contains("api.bilibili.com/pugv/player/web/playurl"))
                    webRequest.Headers.Add("Referer", "https://www.bilibili.com");
                //webRequest.Headers.CacheControl = CacheControlHeaderValue.Parse("no-cache");
                webRequest.Headers.Connection.Clear();
                LogDebug("获取网页内容：Url: {0}, Headers: {1}", url, webRequest.Headers);
                var webResponse = (await AppHttpClient.SendAsync(webRequest, HttpCompletionOption.ResponseHeadersRead)).EnsureSuccessStatusCode();
                htmlCode = await webResponse.Content.ReadAsStringAsync();

            }
            catch (Exception)
            {
                ;
            }
            LogDebug("Response: {0}", htmlCode);
            return htmlCode;
        }

        public static async Task<string> GetPostResponseAsync(string Url, byte[] postData)
        {
            LogDebug("Post to: {0}, data: {1}", Url, Convert.ToBase64String(postData));
            string htmlCode = string.Empty;
            using HttpRequestMessage request = new(HttpMethod.Post, Url);
            request.Headers.Add("ContentType", "application/grpc");
            request.Headers.Add("ContentLength", postData.Length.ToString());
            request.Headers.Add("UserAgent", "Dalvik/2.1.0 (Linux; U; Android 6.0.1; oneplus a5010 Build/V417IR) 6.10.0 os/android model/oneplus a5010 mobi_app/android build/6100500 channel/bili innerVer/6100500 osVer/6.0.1 network/2");
            request.Headers.Add("Cookie", Program.COOKIE);
            request.Content = new ByteArrayContent(postData);
            var webResponse = await AppHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            Stream myRequestStream = await webResponse.Content.ReadAsStreamAsync();
            htmlCode = await webResponse.Content.ReadAsStringAsync();
            return htmlCode;
        }

        public static async Task<string> GetAidByBVAsync(string bv)
        {
            string api = $"https://api.bilibili.com/x/web-interface/archive/stat?bvid={bv}";
            string json = await GetWebSourceAsync(api);
            using var jDoc = JsonDocument.Parse(json);
            string aid = jDoc.RootElement.GetProperty("data").GetProperty("aid").ToString();
            return aid;
        }

        public static async Task<string> GetEpidBySSIdAsync(string ssid)
        {
            string api = $"https://api.bilibili.com/pugv/view/web/season?season_id={ssid}";
            string json = await GetWebSourceAsync(api);
            using var jDoc = JsonDocument.Parse(json);
            string epId = jDoc.RootElement.GetProperty("data").GetProperty("episodes").EnumerateArray().First().GetProperty("id").ToString();
            return epId;
        }

        public static async Task<string> GetSSIdByMDAsync(string mdId)
        {
            var api = $"https://api.bilibili.com/pgc/review/user?media_id={mdId}";
            var json = await GetWebSourceAsync(api);
            using var jDoc = JsonDocument.Parse(json);
            var ssId = "ss" + jDoc.RootElement.GetProperty("result").GetProperty("media").GetProperty("season_id").ToString();
            return ssId;
        }

        private static async Task RangeDownloadToTmpAsync(int id, string url, string tmpName, long fromPosition, long? toPosition, Action<int, long, long> onProgress, bool failOnRangeNotSupported = false)
        {
            DateTimeOffset? lastTime = File.Exists(tmpName) ? new FileInfo(tmpName).LastWriteTimeUtc : null;
            using (var fileStream = new FileStream(tmpName, FileMode.Create))
            {
                fileStream.Seek(0, SeekOrigin.End);
                var downloadedBytes = fromPosition + fileStream.Position;

                using var httpRequestMessage = new HttpRequestMessage();
                if (!url.Contains("platform=android_tv_yst") && !url.Contains("platform=android"))
                    httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://www.bilibili.com");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
                httpRequestMessage.Headers.TryAddWithoutValidation("Cookie", Program.COOKIE);
                httpRequestMessage.Headers.Range = new(downloadedBytes, toPosition);
                httpRequestMessage.Headers.IfRange = lastTime != null ? new(lastTime.Value) : null;
                httpRequestMessage.RequestUri = new(url);

                using var response = (await AppHttpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead)).EnsureSuccessStatusCode();

                if (response.StatusCode == HttpStatusCode.OK) // server doesn't response a partial content
                {
                    if (failOnRangeNotSupported && (downloadedBytes > 0 || toPosition != null)) throw new NotSupportedException("Range request is not supported.");
                    downloadedBytes = 0;
                    fileStream.Seek(0, SeekOrigin.Begin);
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                var totalBytes = downloadedBytes + (response.Content.Headers.ContentLength ?? long.MaxValue - downloadedBytes);

                const int blockSize = 1048576 / 4;
                var buffer = new byte[blockSize];

                while (downloadedBytes < totalBytes)
                {
                    var recevied = await stream.ReadAsync(buffer);
                    if (recevied == 0) break;
                    await fileStream.WriteAsync(buffer.AsMemory(0, recevied));
                    await fileStream.FlushAsync();
                    downloadedBytes += recevied;
                    onProgress(id, downloadedBytes - fromPosition, totalBytes);
                }

                if (response.Content.Headers.ContentLength != null && (response.Content.Headers.ContentLength != new FileInfo(tmpName).Length))
                    throw new Exception("Retry...");
            }
        }

        public static async Task DownloadFile(string url, string path, bool aria2c, string aria2cProxy)
        {
            LogDebug("Start downloading: {0}", url);
            if (aria2c)
            {
                BBDownAria2c.DownloadFileByAria2c(url, path, aria2cProxy);
                if (File.Exists(path + ".aria2") || !File.Exists(path))
                    throw new Exception("aria2下載可能存在錯誤");
                Console.WriteLine();
                return;
            }
            int retry = 0;
            string tmpName = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".tmp");
            reDown:
                try
                {
                    using (var progress = new ProgressBar())
                    {
                        await RangeDownloadToTmpAsync(0, url, tmpName, 0, null, (_, downloaded, total) => progress.Report((double)downloaded / total));
                        File.Move(tmpName, path, true);
                    }
                }
                catch (Exception) {
                    {
                        if (++retry == 3) throw;
                        goto reDown;
                    }
                }
        }

        //https://stackoverflow.com/a/25877042
        public static async Task RunWithMaxDegreeOfConcurrency<T>(
            int maxDegreeOfConcurrency, IEnumerable<T> collection, Func<T, Task> taskFactory)
        {
            var activeTasks = new List<Task>(maxDegreeOfConcurrency);
            foreach (var task in collection.Select(taskFactory))
            {
                activeTasks.Add(task);
                if (activeTasks.Count == maxDegreeOfConcurrency)
                {
                    await Task.WhenAny(activeTasks.ToArray());
                    //observe exceptions here
                    activeTasks.RemoveAll(t => t.IsCompleted);
                }
            }
            await Task.WhenAll(activeTasks.ToArray()).ContinueWith(t =>
            {
                //observe exceptions in a manner consistent with the above   
            });
        }

        public static async Task MultiThreadDownloadFileAsync(string url, string path, bool aria2c, string aria2cProxy)
        {
            if (aria2c)
            {
                BBDownAria2c.DownloadFileByAria2c(url, path, aria2cProxy);
                if (File.Exists(path + ".aria2") || !File.Exists(path))
                    throw new Exception("aria2下載可能存在錯誤");
                Console.WriteLine();
                return;
            }
            long fileSize = GetFileSize(url);
            LogDebug("檔案大小：{0} bytes", fileSize);
            List<Clip> allClips = GetAllClips(url, fileSize);
            int total = allClips.Count;
            LogDebug("分段數量：{0}", total);
            ConcurrentDictionary<int, long> clipProgress = new();
            foreach (var i in allClips) clipProgress[i.index] = 0;

            using (var progress = new ProgressBar())
            {
                progress.Report(0);
                await Parallel.ForEachAsync(allClips, async (clip, _) =>
                 {
                     int retry = 0;
                     string tmp = Path.Combine(Path.GetDirectoryName(path), clip.index.ToString("00000") + "_" + Path.GetFileNameWithoutExtension(path) + (Path.GetExtension(path).EndsWith(".mp4") ? ".vclip" : ".aclip"));
                     reDown:
                         try
                         {
                             await RangeDownloadToTmpAsync(clip.index, url, tmp, clip.from, clip.to == -1 ? null : clip.to, (index, downloaded, _) =>
                             {
                                 clipProgress[index] = downloaded;
                                 progress.Report((double)clipProgress.Values.Sum() / fileSize);
                             }, true);
                         }
                         catch (NotSupportedException)
                         {
                             if (++retry == 3) throw new Exception($"伺服器可能並不支持多線程下載，請使用 --multi-thread false 關閉多線程");
                             goto reDown;
                         }
                         catch (Exception)
                         {
                             if (++retry == 3) throw new Exception($"Failed to download clip {clip.index}");
                             goto reDown;
                         }
                 });
            }
        }

        //此函數主要是切片下載邏輯
        private static List<Clip> GetAllClips(string url, long fileSize)
        {
            List<Clip> clips = new List<Clip>();
            int index = 0;
            long counter = 0;
            int perSize = 10 * 1024 * 1024;
            while (fileSize > 0)
            {
                Clip c = new Clip();
                c.index = index;
                c.from = counter;
                c.to = c.from + perSize;
                //沒到最後
                if (fileSize - perSize > 0)
                {
                    fileSize -= perSize;
                    counter += perSize + 1;
                    index++;
                    clips.Add(c);
                }
                //已到最後
                else
                {
                    c.to = -1;
                    clips.Add(c);
                    break;
                }
            }
            return clips;
        }

        /// <summary>
        /// 輸入一堆已存在的文件，合併到新文件
        /// </summary>
        /// <param name="files"></param>
        /// <param name="outputFilePath"></param>
        public static void CombineMultipleFilesIntoSingleFile(string[] files, string outputFilePath)
        {
            if (files.Length == 1)
            {
                FileInfo fi = new FileInfo(files[0]);
                fi.MoveTo(outputFilePath);
                return;
            }

            if (!Directory.Exists(Path.GetDirectoryName(outputFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

            string[] inputFilePaths = files;
            using (var outputStream = File.Create(outputFilePath))
            {
                foreach (var inputFilePath in inputFilePaths)
                {
                    if (inputFilePath == "")
                        continue;
                    using (var inputStream = File.OpenRead(inputFilePath))
                    {
                        // Buffer size can be passed as the second argument.
                        inputStream.CopyTo(outputStream);
                    }
                    //Console.WriteLine("The file {0} has been processed.", inputFilePath);
                }
            }
            //Global.ExplorerFile(outputFilePath);
        }

        /// <summary>
        /// 尋找指定目錄下指定後綴的文件的詳細路徑 如".txt"
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static string[] GetFiles(string dir, string ext)
        {
            List<string> al = new List<string>();
            StringBuilder sb = new StringBuilder();
            DirectoryInfo d = new DirectoryInfo(dir);
            foreach (FileInfo fi in d.GetFiles())
            {
                if (fi.Extension.ToUpper() == ext.ToUpper())
                {
                    al.Add(fi.FullName);
                }
            }
            string[] res = al.ToArray();
            Array.Sort(res); //排序
            return res;
        }

        private static long GetFileSize(string url)
        {
            WebClient webClient = new WebClient();
            if (!url.Contains("platform=android_tv_yst"))
                webClient.Headers.Add("Referer", "https://www.bilibili.com");
            webClient.Headers.Add("User-Agent", "Mozilla/5.0");
            webClient.OpenRead(url);
            long totalSizeBytes = Convert.ToInt64(webClient.ResponseHeaders["Content-Length"]);

            return totalSizeBytes;
        }

        //重定向
        public static async Task<string> Get302(string url)
        {
            //this allows you to set the settings so that we can get the redirect url
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            string redirectedUrl = null;
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            {
                // ... Read the response to see if we have the redirected url
                if (response.StatusCode == System.Net.HttpStatusCode.Found)
                {
                    HttpResponseHeaders headers = response.Headers;
                    if (headers != null && headers.Location != null)
                    {
                        redirectedUrl = headers.Location.AbsoluteUri;
                    }
                }
            }

            return redirectedUrl;
        }

        public static string GetValidFileName(string input, string re = ".")
        {
            string title = input;
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar.ToString(), re);
            }
            return title;
        }

        
        /// <summary>    
        /// 獲取url字串參數，返回參數值字串    
        /// </summary>    
        /// <param name="name">參數名稱</param>    
        /// <param name="url">url字串</param>    
        /// <returns></returns>    
        public static string GetQueryString(string name, string url)
        {
            Regex re = new Regex(@"(^|&)?(\w+)=([^&]+)(&|$)?", System.Text.RegularExpressions.RegexOptions.Compiled);
            MatchCollection mc = re.Matches(url);
            foreach (Match m in mc)
            {
                if (m.Result("$2").Equals(name))
                {
                    return m.Result("$3");
                }
            }
            return "";
        }

        public static async Task<string> GetLoginStatusAsync(string oauthKey)
        {
            string queryUrl = "https://passport.bilibili.com/qrcode/getLoginInfo";
            WebClient webClient = new WebClient();
            NameValueCollection postValues = new NameValueCollection();
            postValues.Add("oauthKey", oauthKey);
            postValues.Add("gourl", "https://www.bilibili.com/");
            byte[] responseArray = await (await AppHttpClient.PostAsync(queryUrl, new FormUrlEncodedContent(postValues.ToDictionary()))).Content.ReadAsByteArrayAsync();
            return Encoding.UTF8.GetString(responseArray);
        }

        //https://s1.hdslb.com/bfs/static/player/main/video.9efc0c61.js
        public static string GetSession(string buvid3)
        {
            //這個參數可以沒有 所以此處就不寫具體實現了
            throw new NotImplementedException();
        }

        public static string GetSign(string parms)
        {
            string toEncode = parms + "59b43e04ad6965f34319062b478f83dd";
            MD5 md5 = MD5.Create();
            byte[] bs = Encoding.UTF8.GetBytes(toEncode);
            byte[] hs = md5.ComputeHash(bs);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hs)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public static string GetTimeStamp(bool bflag)
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            string ret = string.Empty;
            if (bflag)
                ret = Convert.ToInt64(ts.TotalSeconds).ToString();
            else
                ret = Convert.ToInt64(ts.TotalMilliseconds).ToString();

            return ret;
        }

        //https://stackoverflow.com/questions/1344221/how-can-i-generate-random-alphanumeric-strings
        private static Random random = new Random();
        public static string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        //https://stackoverflow.com/a/45088333
        public static string ToQueryString(NameValueCollection nameValueCollection)
        {
            NameValueCollection httpValueCollection = HttpUtility.ParseQueryString(string.Empty);
            httpValueCollection.Add(nameValueCollection);
            return httpValueCollection.ToString();
        }

        public static Dictionary<string, string> ToDictionary(this NameValueCollection nameValueCollection)
        {
            var dict = new Dictionary<string, string>();
            foreach (var key in nameValueCollection.AllKeys)
            {
                dict[key] = nameValueCollection[key];
            }
            return dict;
        }

        public static string GetMaxQn()
        {
            return Program.qualitys.Keys.First();
        }

        public static NameValueCollection GetTVLoginParms()
        {
            NameValueCollection sb = new();
            DateTime now = DateTime.Now;
            string deviceId = GetRandomString(20);
            string buvid = GetRandomString(37);
            string fingerprint = $"{now.ToString("yyyyMMddHHmmssfff")}{GetRandomString(45)}";
            sb.Add("appkey","4409e2ce8ffd12b8");
            sb.Add("auth_code", "");
            sb.Add("bili_local_id", deviceId);
            sb.Add("build", "102801");
            sb.Add("buvid", buvid);
            sb.Add("channel", "master");
            sb.Add("device", "OnePlus");
            sb.Add($"device_id", deviceId);
            sb.Add("device_name", "OnePlus7TPro");
            sb.Add("device_platform", "Android10OnePlusHD1910");
            sb.Add($"fingerprint", fingerprint);
            sb.Add($"guid", buvid);
            sb.Add($"local_fingerprint", fingerprint);
            sb.Add($"local_id", buvid);
            sb.Add("mobi_app", "android_tv_yst");
            sb.Add("networkstate", "wifi");
            sb.Add("platform", "android");
            sb.Add("sys_ver", "29");
            sb.Add($"ts", GetTimeStamp(true));
            sb.Add($"sign", GetSign(ToQueryString(sb)));

            return sb;
        }

        /// <summary>
        /// 编码转换
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string GetVideoCodec(string code)
        {
            return code switch
            {
                "13" => "AV1",
                "12" => "HEVC",
                "7" => "AVC",
                _ => "UNKNOWN"
            };
        }
    }
}
