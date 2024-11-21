using QRCoder;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using static BBDown.BBDownParser;
using static BBDown.BBDownLogger;
using static BBDown.BBDownMuxer;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using Fanhuaji_API;

namespace BBDown
{
    class Program
    {
        public static string COOKIE = "";
        public static string TOKEN { get; set; } = "";

        public static Dictionary<string, string> qualitys = new Dictionary<string, string>() {
            {"127","8K" }, {"126","DolbyVision" }, {"125","HDR" }, {"120","4K" }, {"116","1080P60" },
            {"112","1080P" }, {"80","1080P" }, {"74","720P60" },
            {"64","720P" }, {"48","720P" }, {"32","480P" }, {"16","360P" }
        };

        public static string APP_DIR = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

        private static int Compare(Video r1, Video r2)
        {
            return (Convert.ToInt32(r1.id) * 100000 + r1.bandwith) > (Convert.ToInt32(r2.id) * 100000 + r2.bandwith) ? -1 : 1;
        }

        private static int Compare(Audio r1, Audio r2)
        {
            return r1.bandwith - r2.bandwith > 0 ? -1 : 1;
        }

        class MyOption
        {
            public string Url { get; set; }
            public bool UseTvApi { get; set; }
            public bool UseAppApi { get; set; }
            public bool UseIntlApi { get; set; }
            public bool UseMP4box { get; set; }
            public bool OnlyHevc { get; set; }
            public bool OnlyAvc { get; set; }
            public bool OnlyShowInfo { get; set; }
            public bool ShowAll { get; set; }
            public bool UseAria2c { get; set; }
            public bool Interactive { get; set; }
            public bool HideStreams { get; set; }
            public bool MultiThread { get; set; }
            public bool VideoOnly { get; set; }
            public bool AudioOnly { get; set; }
            public bool SubOnly { get; set; }
            public bool NoPaddingPageNum { get; set; }
            public bool Debug { get; set; }
            public bool SkipMux { get; set; }
            public string SelectPage { get; set; } = "";
            public string Language { get; set; } = "";
            public string AccessToken { get; set; } = "";
            public string Aria2cProxy { get; set; } = "";
            public string Output { get; set; } = "";

            public override string ToString()
            {
                return $"{{Input={Url}, {nameof(UseTvApi)}={UseTvApi.ToString()}, " +
                    $"{nameof(UseAppApi)}={UseAppApi.ToString()}, " +
                    $"{nameof(UseIntlApi)}={UseIntlApi.ToString()}, " +
                    $"{nameof(UseMP4box)}={UseMP4box.ToString()}, " +
                    $"{nameof(OnlyHevc)}={OnlyHevc.ToString()}, " +
                    $"{nameof(OnlyAvc)}={OnlyAvc.ToString()}, " +
                    $"{nameof(OnlyShowInfo)}={OnlyShowInfo.ToString()}, " +
                    $"{nameof(Interactive)}={Interactive.ToString()}, " +
                    $"{nameof(HideStreams)}={HideStreams.ToString()}, " +
                    $"{nameof(ShowAll)}={ShowAll.ToString()}, " +
                    $"{nameof(UseAria2c)}={UseAria2c.ToString()}, " +
                    $"{nameof(MultiThread)}={MultiThread.ToString()}, " +
                    $"{nameof(VideoOnly)}={VideoOnly.ToString()}, " +
                    $"{nameof(AudioOnly)}={AudioOnly.ToString()}, " +
                    $"{nameof(SubOnly)}={SubOnly.ToString()}, " +
                    $"{nameof(NoPaddingPageNum)}={NoPaddingPageNum.ToString()}, " +
                    $"{nameof(Debug)}={Debug.ToString()}, " +
                    $"{nameof(SelectPage)}={SelectPage}, " +
                    $"{nameof(AccessToken)}={AccessToken}, " +
                    $"{nameof(Aria2cProxy)}={Aria2cProxy}, " +
                    $"{nameof(Output)}={Output}}}";
            }
        }

        public static int Main(params string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 2048;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                return true;
            };

            var rootCommand = new RootCommand
            {
                new Argument<string>(
                    "url",
                    description: "影片網址 或 av|bv|BV|ep|ss"),
                new Option<bool>(
                    new string[]{ "--use-tv-api" ,"-tv"},
                    "使用TV端解析模式"),
                new Option<bool>(
                    new string[]{ "--use-app-api" ,"-app"},
                    "使用APP端解析模式"),
                new Option<bool>(
                    new string[]{ "--use-intl-api" ,"-intl"},
                    "使用國際版解析模式"),
                new Option<bool>(
                    new string[]{ "--use-mp4box"},
                    "使用MP4Box來混流"),
                new Option<bool>(
                    new string[]{ "--only-hevc" ,"-hevc"},
                    "只下載hevc編碼"),
                new Option<bool>(
                    new string[]{ "--only-avc" ,"-avc"},
                    "只下載avc編碼"),
                new Option<bool>(
                    new string[]{ "--only-show-info" ,"-info"},
                    "僅解析而不進行下載"),
                new Option<bool>(
                    new string[]{ "--hide-streams", "-hs"},
                    "不要顯示所有可用音影片軌"),
                new Option<bool>(
                    new string[]{ "--interactive", "-ia"},
                    "互動式選擇清晰度"),
                new Option<bool>(
                    new string[]{ "--show-all"},
                    "展示所有分P標題"),
                new Option<bool>(
                    new string[]{ "--use-aria2c"},
                    "調用aria2c進行下載(你需要自行準備好二進位制可執行文件)"),
                new Option<string>(
                    new string[]{ "--aria2c-proxy"},
                    "調用aria2c進行下載時的代理地址配置"),
                new Option<bool>(
                    new string[]{ "--multi-thread", "-mt"},
                    "使用多執行緒下載"),
                new Option<string>(
                    new string[]{ "--select-page" ,"-p"},
                    "選擇指定分p或分p範圍：(-p 8 或 -p 1,2 或 -p 3-5 或 -p ALL)"),
                new Option<bool>(
                    new string[]{ "--audio-only"},
                    "僅下載音訊"),
                new Option<bool>(
                    new string[]{ "--video-only"},
                    "僅下載影片"),
                new Option<bool>(
                    new string[]{ "--sub-only"},
                    "僅下載字幕"),
                new Option<bool>(
                    new string[]{ "--no-padding-page-num"},
                    "不給分P序號補零"),
                new Option<bool>(
                    new string[]{ "--debug"},
                    "輸出除錯日誌"),
                new Option<bool>(
                    new string[]{ "--skip-mux"},
                    "跳過混流步驟"),
                new Option<string>(
                    new string[]{ "--language"},
                    "設置混流的音訊語言(代碼)，如chi, jpn等"),
                new Option<string>(
                    new string[]{ "--access-token" ,"-token"},
                    "設置access_token用以下載TV/APP介面的會員內容"),
                new Option<string>(
                    new string[]{ "--output" ,"-o"},
                    "設置分類資料夾")
            };

            Command loginCommand = new Command(
                "login",
                "通過APP掃描二維碼以登錄您的WEB帳號");
            rootCommand.AddCommand(loginCommand);
            Command loginTVCommand = new Command(
                "logintv",
                "通過APP掃描二維碼以登錄您的TV帳號");
            rootCommand.AddCommand(loginTVCommand);
            rootCommand.Description = "BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟體.";
            rootCommand.TreatUnmatchedTokensAsErrors = true;

            //WEB登錄
            loginCommand.Handler = CommandHandler.Create(async delegate
            {
                try
                {
                    Log("獲取登錄地址...");
                    string loginUrl = "https://passport.bilibili.com/qrcode/getLoginUrl";
                    string url = JsonDocument.Parse(await GetWebSourceAsync(loginUrl)).RootElement.GetProperty("data").GetProperty("url").ToString();
                    string oauthKey = GetQueryString("oauthKey", url);
                    //Log(oauthKey);
                    //Log(url);
                    bool flag = false;
                    Log("生成二維碼...");
                    QRCodeGenerator qrGenerator = new QRCodeGenerator();
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                    QRCode qrCode = new QRCode(qrCodeData);
                    Bitmap qrCodeImage = qrCode.GetGraphic(7);
                    qrCodeImage.Save("qrcode.png", System.Drawing.Imaging.ImageFormat.Png);
                    Log("生成二維碼成功：qrcode.png, 請打開並掃描");
                    while (true)
                    {
                        await Task.Delay(1000);
                        string w = await GetLoginStatusAsync(oauthKey);
                        string data = JsonDocument.Parse(w).RootElement.GetProperty("data").ToString();
                        if (data == "-2")
                        {
                            LogColor("二維碼已過期, 請重新執行登錄指令.");
                            break;
                        }
                        else if (data == "-4") //等待掃碼
                        {
                            continue;
                        }
                        else if (data == "-5") //等待確認
                        {
                            if (!flag)
                            {
                                Log("掃碼成功, 請確認...");
                                flag = !flag;
                            }
                        }
                        else
                        {
                            string cc = JsonDocument.Parse(w).RootElement.GetProperty("data").GetProperty("url").ToString();
                            string cookiePath = Directory.GetCurrentDirectory();
                            Log("登錄成功: SESSDATA=" + GetQueryString("SESSDATA", cc));
                            //導出cookie
                            if (!File.Exists($"cookie.txt"))
                            {
                                File.Create($"{cookiePath}/cookie.txt");
                                int i = 0;
                                while (!File.Exists($"cookie.txt") && i < 30)
                                {
                                    Thread.Sleep(200);
                                    i++;
                                }
                                using (StreamWriter sw = new StreamWriter($"{cookiePath}/cookie.txt"))
                                {
                                    sw.Write("SESSDATA=" + GetQueryString("SESSDATA", cc));
                                }
                            }
                            else
                            {
                                using (StreamWriter sw = new StreamWriter($"{cookiePath}/cookie.txt"))
                                {
                                    sw.Write("SESSDATA=" + GetQueryString("SESSDATA", cc));
                                }
                            }
                            File.WriteAllText(Path.Combine(APP_DIR, "BBDown.data"), cc.Substring(cc.IndexOf('?') + 1).Replace("&", ";"));
                            File.Delete("qrcode.png");
                            break;
                        }
                    }
                }
                catch (Exception e) { LogError(e.Message); }
            });

            //TV登錄
            loginTVCommand.Handler = CommandHandler.Create(async delegate
            {
                try
                {
                    string loginUrl = "https://passport.snm0516.aisee.tv/x/passport-tv-login/qrcode/auth_code";
                    string pollUrl = "https://passport.bilibili.com/x/passport-tv-login/qrcode/poll";
                    var parms = GetTVLoginParms();
                    Log("獲取登錄地址...");
                    WebClient webClient = new WebClient();
                    byte[] responseArray = await (await AppHttpClient.PostAsync(loginUrl, new FormUrlEncodedContent(parms.ToDictionary()))).Content.ReadAsByteArrayAsync();
                    string web = Encoding.UTF8.GetString(responseArray);
                    string url = JsonDocument.Parse(web).RootElement.GetProperty("data").GetProperty("url").ToString();
                    string authCode = JsonDocument.Parse(web).RootElement.GetProperty("data").GetProperty("auth_code").ToString();
                    Log("生成二維碼...");
                    QRCodeGenerator qrGenerator = new QRCodeGenerator();
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                    QRCode qrCode = new QRCode(qrCodeData);
                    Bitmap qrCodeImage = qrCode.GetGraphic(7);
                    qrCodeImage.Save("qrcode.png", System.Drawing.Imaging.ImageFormat.Png);
                    Log("生成二維碼成功：qrcode.png, 請打開並掃描");
                    parms.Set("auth_code", authCode);
                    parms.Set("ts", GetTimeStamp(true));
                    parms.Remove("sign");
                    parms.Add("sign", GetSign(ToQueryString(parms)));
                    while (true)
                    {
                        await Task.Delay(1000);
                        responseArray = await (await AppHttpClient.PostAsync(pollUrl, new FormUrlEncodedContent(parms.ToDictionary()))).Content.ReadAsByteArrayAsync();
                        web = Encoding.UTF8.GetString(responseArray);
                        string code = JsonDocument.Parse(web).RootElement.GetProperty("code").ToString();
                        if (code == "86038")
                        {
                            LogColor("二維碼已過期, 請重新執行登錄指令.");
                            break;
                        }
                        else if (code == "86039") //等待掃碼
                        {
                            continue;
                        }
                        else
                        {
                            string cc = JsonDocument.Parse(web).RootElement.GetProperty("data").GetProperty("access_token").ToString();
                            Log("登錄成功: AccessToken=" + cc);
                            //導出cookie
                            File.WriteAllText(Path.Combine(APP_DIR, "BBDownTV.data"), "access_token=" + cc);
                            File.Delete("qrcode.png");
                            break;
                        }
                    }
                }
                catch (Exception e) { LogError(e.Message); }
            });

            rootCommand.Handler = CommandHandler.Create<MyOption>(async (myOption) =>
            {
                await DoWorkAsync(myOption);
            });

            return rootCommand.InvokeAsync(args).Result;
        }

        private static async Task DoWorkAsync(MyOption myOption)
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Console.Write($"BBDown version {ver.Major}.{ver.Minor}.{ver.Build}, Bilibili Downloader. 24/7 version \r\n");
            Console.ResetColor();
            Console.Write("BBDown Server Edition");
            Console.WriteLine();
            //Fanhuaji-API
            var Fanhuaji = new Fanhuaji(Agree: true, Terms_of_Service: Fanhuaji_API.Fanhuaji.Terms_of_Service);
            //檢測更新
            new Thread(async () =>
            {
                await CheckUpdateAsync();
            }).Start();

            try
            {
                //Read cookie from cookie.txt
                string cookiePath = Directory.GetCurrentDirectory();
                if (!File.Exists($"cookie.txt"))
                {
                    File.Create($"{cookiePath}/cookie.txt");
                }
                string cookieString = File.ReadAllText($"{cookiePath}/cookie.txt");
                bool interactMode = myOption.Interactive;
                bool infoMode = myOption.OnlyShowInfo;
                bool tvApi = myOption.UseTvApi;
                bool appApi = myOption.UseAppApi;
                bool intlApi = myOption.UseIntlApi;
                bool useMp4box = myOption.UseMP4box;
                bool onlyHevc = myOption.OnlyHevc;
                bool onlyAvc = myOption.OnlyAvc;
                bool hideStreams = myOption.HideStreams;
                bool multiThread = myOption.MultiThread;
                bool audioOnly = myOption.AudioOnly;
                bool videoOnly = myOption.VideoOnly;
                bool subOnly = myOption.SubOnly;
                bool skipMux = myOption.SkipMux;
                bool showAll = myOption.ShowAll;
                bool useAria2c = myOption.UseAria2c;
                string aria2cProxy = myOption.Aria2cProxy;
                DEBUG_LOG = myOption.Debug;
                string input = myOption.Url;
                string lang = myOption.Language;
                string selectPage = myOption.SelectPage.ToUpper();
                string aidOri = ""; //原始aid
                COOKIE = cookieString;
                TOKEN = myOption.AccessToken.Replace("access_token=", "");
                string output = myOption.Output;

                //audioOnly和videoOnly同時開啟則全部忽視
                if (audioOnly && videoOnly)
                {
                    audioOnly = false;
                    videoOnly = false;
                }

                //OnlyHevc和OnlyAvc同時開啟則全部忽視
                if (onlyAvc && onlyHevc)
                {
                    onlyAvc = false;
                    onlyHevc = false;
                }

                List<string> selectedPages = null;
                if (!string.IsNullOrEmpty(GetQueryString("p", input)))
                {
                    selectedPages = new List<string>();
                    selectedPages.Add(GetQueryString("p", input));
                }

                LogDebug("AppDirectory: {0}", APP_DIR);
                LogDebug("運行參數：{0}", myOption);
                if (string.IsNullOrEmpty(COOKIE) && File.Exists(Path.Combine(APP_DIR, "BBDown.data")) && !tvApi)
                {
                    Log("載入本地cookie...");
                    LogDebug("文件路徑：{0}", Path.Combine(APP_DIR, "BBDown.data"));
                    COOKIE = File.ReadAllText(Path.Combine(APP_DIR, "BBDown.data"));
                }
                if (string.IsNullOrEmpty(TOKEN) && File.Exists(Path.Combine(APP_DIR, "BBDownTV.data")) && tvApi)
                {
                    Log("載入本地token...");
                    LogDebug("文件路徑：{0}", Path.Combine(APP_DIR, "BBDownTV.data"));
                    TOKEN = File.ReadAllText(Path.Combine(APP_DIR, "BBDownTV.data"));
                    TOKEN = TOKEN.Replace("access_token=", "");
                }
                if (string.IsNullOrEmpty(TOKEN) && File.Exists(Path.Combine(APP_DIR, "BBDownApp.data")) && appApi)
                {
                    Log("載入本地token...");
                    LogDebug("文件路徑：{0}", Path.Combine(APP_DIR, "BBDownApp.data"));
                    TOKEN = File.ReadAllText(Path.Combine(APP_DIR, "BBDownApp.data"));
                    TOKEN = TOKEN.Replace("access_token=", "");
                }
                Log("獲取aid...");
                aidOri = await GetAvIdAsync(input);
                Log("獲取aid結束: " + aidOri);
                //-p的優先度大於URL中的自帶p參數，所以先清空selectedPages
                if (!string.IsNullOrEmpty(selectPage) && selectPage != "ALL" && selectPage != "LATEST")
                {
                    selectedPages = new List<string>();
                    try
                    {
                        string tmp = selectPage;
                        tmp = tmp.Trim().Trim(',');
                        if (tmp.Contains("-"))
                        {
                            int start = int.Parse(tmp.Split('-')[0]);
                            int end = int.Parse(tmp.Split('-')[1]);
                            for (int i = start; i <= end; i++)
                            {
                                selectedPages.Add(i.ToString());
                            }
                        }
                        else
                        {
                            foreach (var s in tmp.Split(','))
                            {
                                selectedPages.Add(s);
                            }
                        }
                    }
                    catch { LogError("解析分P參數時失敗了~"); selectedPages = null; };
                }

                if (selectPage == "ALL") selectedPages = null;

                if (string.IsNullOrEmpty(aidOri)) throw new Exception("輸入有誤");
                Log("獲取影片訊息...");
                IFetcher fetcher = new BBDownNormalInfoFetcher();
                if (aidOri.StartsWith("cheese"))
                {
                    fetcher = new BBDownCheeseInfoFetcher();
                }
                else if (aidOri.StartsWith("ep"))
                {
                    if (intlApi)
                        fetcher = new BBDownIntlBangumiInfoFetcher();
                    else
                        fetcher = new BBDownBangumiInfoFetcher();
                }
                else if (aidOri.StartsWith("mid"))
                {
                    fetcher = new BBDownSpaceVideoFetcher();
                }
                var vInfo = await fetcher.FetchAsync(aidOri);
                string title = vInfo.Title;
                string desc = vInfo.Desc;
                string pic = vInfo.Pic;
                string pubTime = vInfo.PubTime;
                LogColor("影片標題: " + title);
                Log("發布時間: " + pubTime);
                List<Page> pagesInfo = vInfo.PagesInfo;
                List<Subtitle> subtitleInfo = new List<Subtitle>();
                bool more = false;
                bool bangumi = vInfo.IsBangumi;
                bool cheese = vInfo.IsCheese;

                //列印分P訊息
                foreach (Page p in pagesInfo)
                {
                    if (!showAll && more && p.index != pagesInfo.Count) continue;
                    if (!showAll && !more && p.index > 5)
                    {
                        Log("......");
                        more = true;
                    }
                    else
                    {
                        Log($"P{p.index}: [{p.cid}] [{p.title}] [{FormatTime(p.dur)}]");
                    }
                }

                if (selectPage == "LATEST")
                {
                    selectedPages = new List<string> { pagesInfo.Count.ToString() };
                    LogDebug(pagesInfo.Count.ToString());
                }

                //如果用戶沒有選擇分P，根據epid來確定某一集
                if (selectedPages == null && selectPage != "ALL" && !string.IsNullOrEmpty(vInfo.Index) && selectPage != "LATEST")
                {
                    selectedPages = new List<string> { vInfo.Index };
                    Log("程序已自動選擇你輸入的集數，如果要下載其他集數請自行指定分P(如可使用-p ALL代表全部 -p LATEST代表最新一集)");
                }

                Log($"共計 {pagesInfo.Count} 個分P, 已選擇：" + (selectedPages == null ? "ALL" : string.Join(",", selectedPages)));

                //過濾不需要的分P
                if (selectedPages != null)
                    pagesInfo = pagesInfo.Where(p => selectedPages.Contains(p.index.ToString())).ToList();

                foreach (Page p in pagesInfo)
                {
                    Log($"開始解析P{p.index}...");
                    if (!infoMode)
                    {
                        if (!Directory.Exists($"temp/{p.aid}"))
                        {
                            Directory.CreateDirectory($"temp/{p.aid}");
                        }
                        if (!subOnly && !File.Exists($"temp/{p.aid}/{p.aid}.jpg"))
                        {
                            Log("下載封面...");
                            LogDebug("下載：{0}", pic);
                            new WebClient().DownloadFile(pic, $"temp/{p.aid}/{p.aid}.jpg");
                        }
                        string[] files = System.IO.Directory.GetFiles(Directory.GetCurrentDirectory(), $"temp/{p.aid}/{p.aid}.{p.cid}.*.srt");
                        if (files.Length > 0)
                        {
                            Log("字幕已經獲取...");
                            for (int i = 0; i < files.Length; i++)
                            { LogDebug(files[i]); }

                        }
                        else 
                        {
                            LogDebug("獲取字幕...");
                            subtitleInfo = await BBDownSubUtil.GetSubtitlesAsync(p.aid, p.cid, p.epid, intlApi);
                            foreach (Subtitle s in subtitleInfo)
                            {
                                Log($"下載字幕 {s.lan} => {BBDownSubUtil.SubDescDic[s.lan]}...");
                                LogDebug("下載：{0}", s.url);
                                await BBDownSubUtil.SaveSubtitleAsync(s.url, s.path);
                                if (subOnly && File.Exists(s.path) && File.ReadAllText(s.path) != "")
                                {
                                    string _indexStr = p.index.ToString("0".PadRight(pagesInfo.OrderByDescending(_p => _p.index).First().index.ToString().Length, '0'));
                                    //處理文件夾以.結尾導致的異常情況
                                    if (title.EndsWith(".")) title += "_fix";
                                    string _outSubPath = GetValidFileName(title) + (pagesInfo.Count > 1 ? $"/[P{_indexStr}]{GetValidFileName(p.title)}" : (vInfo.PagesInfo.Count > 1 ? $"[P{_indexStr}]{GetValidFileName(p.title)}" : "")) + $"_{BBDownSubUtil.SubDescDic[s.lan]}.srt";
                                    if (_outSubPath.Contains("/"))
                                    {
                                        if (!Directory.Exists(Path.GetDirectoryName(_outSubPath)))
                                        {
                                            Directory.CreateDirectory(Path.GetDirectoryName(_outSubPath));
                                        }
                                    }
                                    File.Move(s.path, _outSubPath, true);
                                }
                            }

                            if (subOnly)
                            {
                                if (Directory.Exists(p.aid) && Directory.GetFiles(p.aid).Length == 0) Directory.Delete(p.aid, true);
                                continue;
                            }
                        }
                    }

                    string webJsonStr = "";
                    List<Video> videoTracks = new();
                    List<Audio> audioTracks = new();
                    List<string> clips = new();
                    List<string> dfns = new();

                    string indexStr = myOption.NoPaddingPageNum ? p.index.ToString() : p.index.ToString("0".PadRight(pagesInfo.OrderByDescending(_p => _p.index).First().index.ToString().Length, '0'));
                    string videoPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.mp4";
                    string audioPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.m4a";
                    //處理文件夾以.結尾導致的異常情況
                    if (title.EndsWith(".")) title += "_fix";
                    var titleObj = await Fanhuaji.ConvertAsync(title, Fanhuaji_API.Enum.Enum_Converter.Traditional);
                    title = titleObj.Data.Text;
                    string ep = p.ep;
                    if (int.TryParse(p.ep, out _)) { 
                        ep =  int.Parse(ep).ToString("D2");
                    }
                    //讀取JSON存放
                    string jsonpath = Directory.GetCurrentDirectory();
                    string jsonfile = File.ReadAllText($"{jsonpath}/config.json");
                    var json = JsonDocument.Parse(jsonfile);
                    var dirname = json.RootElement.GetProperty("dir");
                    title = Regex.Replace(title, @"[<>:""/\\|?*]", "-");

                    //調用解析
                    (webJsonStr, videoTracks, audioTracks, clips, dfns) = await ExtractTracksAsync(onlyHevc, onlyAvc, aidOri, p.aid, p.cid, p.epid, tvApi, intlApi, appApi);
                    string outPath = dirname + (output != "" ? "/" + output : "") + (pagesInfo.Count > 1 ? $"/{json.RootElement.GetProperty("prefix")}{title} - {ep} [0000P]{json.RootElement.GetProperty("suffix")}" +
                    $".mp4" : $"/{json.RootElement.GetProperty("prefix")}{title} - {ep} [0000P]{json.RootElement.GetProperty("suffix")}.mp4");


                    //此處代碼簡直災難，後續最佳化吧
                    if ((videoTracks.Count != 0 || audioTracks.Count != 0) && clips.Count == 0)   //dash
                    {
                        if (webJsonStr.Contains("\"video\":[") && videoTracks.Count == 0) 
                        {
                            LogError("沒有找到符合要求的影片軌");
                            if (!audioOnly) continue;
                        }
                        if (webJsonStr.Contains("\"audio\":[") && audioTracks.Count == 0)
                        {
                            LogError("沒有找到符合要求的音軌");
                            if (!videoOnly) continue;
                        }
                        //降序
                        videoTracks.Sort(Compare);
                        audioTracks.Sort(Compare);

                        if (audioOnly) videoTracks.Clear();
                        if (videoOnly) audioTracks.Clear();

                        int vIndex = 0;
                        int aIndex = 0;

                        if (!hideStreams)
                        {
                            //展示所有的音影片軌訊息
                            if (videoTracks.Count > 0) 
                            {
                                Log($"共計{videoTracks.Count}條影片軌.");
                                int index = 0;
                                foreach (var v in videoTracks)
                                {
                                    int pDur = p.dur == 0 ? v.dur : p.dur;
                                    LogColor($"{index++}. [{v.dfn}] [{v.res}] [{v.codecs}] [{v.fps}] [{v.bandwith} kbps] [{FormatFileSize(v.size)}]".Replace("[] ", ""), false);
                                    if (infoMode) Console.WriteLine(v.baseUrl);
                                }
                            }
                            if (audioTracks.Count > 0)
                            {
                                Log($"共計{audioTracks.Count}條音軌.");
                                int index = 0;
                                foreach (var a in audioTracks)
                                {
                                    int pDur = p.dur == 0 ? a.dur : p.dur;
                                    LogColor($"{index++}. [{a.codecs}] [{a.bandwith} kbps] [~{FormatFileSize(pDur * a.bandwith * 1024 / 8)}]", false);
                                    if (infoMode) Console.WriteLine(a.baseUrl);
                                }
                            }
                        }
                        if (infoMode) continue;
                        if (interactMode && !hideStreams)
                        {
                            if (videoTracks.Count > 0)
                            {
                                Log("請選擇一條影片軌(輸入序號): ", false);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                vIndex = Convert.ToInt32(Console.ReadLine());
                                if (vIndex > videoTracks.Count || vIndex < 0) vIndex = 0;
                                Console.ResetColor();
                            }
                            if (audioTracks.Count > 0)
                            {
                                Log("請選擇一條音軌(輸入序號): ", false);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                aIndex = Convert.ToInt32(Console.ReadLine());
                                if (aIndex > audioTracks.Count || aIndex < 0) aIndex = 0;
                                Console.ResetColor();
                            }
                        }

                        Log($"已選擇的流:");
                        if (videoTracks.Count > 0)
                            LogColor($"[影片] [{videoTracks[vIndex].dfn}] [{videoTracks[vIndex].res}] [{videoTracks[vIndex].codecs}] [{videoTracks[vIndex].fps}] [{videoTracks[vIndex].bandwith} kbps] [~{FormatFileSize(videoTracks[vIndex].dur * videoTracks[vIndex].bandwith * 1024 / 8)}]".Replace("[] ", ""), false);
                        if (audioTracks.Count > 0)
                            LogColor($"[音訊] [{audioTracks[aIndex].codecs}] [{audioTracks[aIndex].bandwith} kbps] [~{FormatFileSize(audioTracks[aIndex].dur * audioTracks[aIndex].bandwith * 1024 / 8)}]", false);

                        outPath = dirname + (output != "" ? "/" + output : "") + (pagesInfo.Count > 1 ? $"/{json.RootElement.GetProperty("prefix")}{title} - {ep} [{videoTracks[vIndex].dfn}]{json.RootElement.GetProperty("suffix")}" +
                        $".mp4" : $"/{json.RootElement.GetProperty("prefix")}{title} - {ep} [{videoTracks[vIndex].dfn}]{json.RootElement.GetProperty("suffix")}.mp4");

                        if (File.Exists(outPath) && new FileInfo(outPath).Length != 0)
                        {
                            Log($"{outPath}已存在, 跳過下載...");
                            continue;
                        }

                        if (videoTracks.Count > 0)
                        {
                            //杜比視界，使用mp4box封裝
                            if (videoTracks[vIndex].dfn == qualitys["126"] && !useMp4box)
                            {
                                LogError($"檢測到杜比視界清晰度,將強制使用mp4box混流...");
                                useMp4box = true;
                            }
                            if (multiThread && !videoTracks[vIndex].baseUrl.Contains("-cmcc-"))
                            {
                                Log($"開始多執行緒下載P{p.index}影片...");
                                await MultiThreadDownloadFileAsync(videoTracks[vIndex].baseUrl, videoPath, useAria2c, aria2cProxy);
                                Log("合併影片分片...");
                                CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(videoPath), ".vclip"), videoPath);
                                Log("清理分片...");
                                foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                            }
                            else
                            {
                                if (multiThread && videoTracks[vIndex].baseUrl.Contains("-cmcc-"))
                                    LogError("檢測到cmcc域名cdn, 已經禁用多執行緒");
                                Log($"開始下載P{p.index}影片...");
                                await DownloadFile(videoTracks[vIndex].baseUrl, videoPath, useAria2c, aria2cProxy);
                            }
                        }
                        if (audioTracks.Count > 0)
                        {
                            if (multiThread && !audioTracks[aIndex].baseUrl.Contains("-cmcc-"))
                            {
                                Log($"開始多執行緒下載P{p.index}音訊...");
                                await MultiThreadDownloadFileAsync(audioTracks[aIndex].baseUrl, audioPath, useAria2c, aria2cProxy);
                                Log("合併音訊分片...");
                                CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(audioPath), ".aclip"), audioPath);
                                Log("清理分片...");
                                foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                            }
                            else
                            {
                                if (multiThread && audioTracks[aIndex].baseUrl.Contains("-cmcc-"))
                                    LogError("檢測到cmcc域名cdn, 已經禁用多執行緒");
                                Log($"開始下載P{p.index}音訊...");
                                await DownloadFile(audioTracks[aIndex].baseUrl, audioPath, useAria2c, aria2cProxy);
                            }
                        }

                        Log($"下載P{p.index}完畢");
                        if (videoTracks.Count == 0) videoPath = "";
                        if (audioTracks.Count == 0) audioPath = "";
                        if (skipMux) continue;
                        Log("開始合併影音" + (subtitleInfo.Count > 0 ? "和字幕" : "") + "...");
                        int code = await MuxAV(useMp4box, videoPath, audioPath, outPath,
                            desc,
                            title,
                            vInfo.PagesInfo.Count > 1 ? ($"P{indexStr}.{p.title}") : "",
                            File.Exists($"temp/{p.aid}/{p.aid}.jpg") ? $"temp/{p.aid}/{p.aid}.jpg" : "",
                            lang,
                            subtitleInfo, audioOnly, videoOnly, p.aid, p.cid);
                        if (code != 0 || !File.Exists(outPath) || new FileInfo(outPath).Length == 0)
                        {
                            LogError("合併失敗"); continue;
                        }
                        Log("清理臨時文件...");
                        Thread.Sleep(200);
                        if (videoTracks.Count > 0) File.Delete(videoPath);
                        if (audioTracks.Count > 0) File.Delete(audioPath);
                    }
                    else if (clips.Count > 0 && dfns.Count > 0)   //flv
                    {
                        bool flag = false;
                    reParse:
                        //降序
                        videoTracks.Sort(Compare);

                        if (interactMode && !flag)
                        {
                            int i = 0;
                            dfns.ForEach(key => LogColor($"{i++}.{qualitys[key]}"));
                            Log("請選擇最想要的清晰度(輸入序號): ", false);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            var vIndex = Convert.ToInt32(Console.ReadLine());
                            if (vIndex > dfns.Count || vIndex < 0) vIndex = 0;
                            Console.ResetColor();
                            //重新解析
                            (webJsonStr, videoTracks, audioTracks, clips, dfns) = await ExtractTracksAsync(onlyHevc, onlyAvc, aidOri, p.aid, p.cid, p.epid, tvApi, intlApi, appApi, dfns[vIndex]);
                            flag = true;
                            videoTracks.Clear();
                            goto reParse;
                        }

                        Log($"共計{videoTracks.Count}條流(共有{clips.Count}個分段).");
                        int index = 0;
                        foreach (var v in videoTracks)
                        {
                            LogColor($"{index++}. [{v.dfn}] [{v.res}] [{v.codecs}] [{v.fps}] [~{(v.size / 1024 / v.dur * 8).ToString("00")} kbps] [{FormatFileSize(v.size)}]".Replace("[] ", ""), false);
                            if (infoMode)
                            {
                                clips.ForEach(delegate (string c) { Console.WriteLine(c); });
                            }
                        }
                        if (infoMode) continue;
                        if (File.Exists(outPath) && new FileInfo(outPath).Length != 0)
                        {
                            Log($"{outPath}已存在, 跳過下載...");
                            continue;
                        }
                        var pad = string.Empty.PadRight(clips.Count.ToString().Length, '0');
                        for (int i = 0; i < clips.Count; i++)
                        {
                            var link = clips[i];
                            videoPath = $"{p.aid}/{p.aid}.P{indexStr}.{p.cid}.{i.ToString(pad)}.mp4";
                            if (multiThread && !link.Contains("-cmcc-"))
                            {
                                if (videoTracks.Count != 0)
                                {
                                    Log($"開始多執行緒下載P{p.index}影片, 片段({(i + 1).ToString(pad)}/{clips.Count})...");
                                    await MultiThreadDownloadFileAsync(link, videoPath, useAria2c, aria2cProxy);
                                    Log("合併影片分片...");
                                    CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(videoPath), ".vclip"), videoPath);
                                }
                                Log("清理分片...");
                                foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                            }
                            else
                            {
                                if (multiThread && link.Contains("-cmcc-"))
                                    LogError("檢測到cmcc域名cdn, 已經禁用多執行緒");
                                if (videoTracks.Count != 0)
                                {
                                    Log($"開始下載P{p.index}影片, 片段({(i + 1).ToString(pad)}/{clips.Count})...");
                                    await DownloadFile(link, videoPath, useAria2c, aria2cProxy);
                                }
                            }
                        }
                        Log($"下載P{p.index}完畢");
                        Log("開始合併分段...");
                        var files = GetFiles(Path.GetDirectoryName(videoPath), ".mp4");
                        videoPath = $"{p.aid}/{p.aid}.P{indexStr}.{p.cid}.mp4";
                        MergeFLV(files, videoPath);
                        if (skipMux) continue;
                        Log("開始混流影片" + (subtitleInfo.Count > 0 ? "和字幕" : "") + "...");
                        int code = await MuxAV(false, videoPath, "", outPath,
                            desc,
                            title,
                            vInfo.PagesInfo.Count > 1 ? ($"P{indexStr}.{p.title}") : "",
                            File.Exists($"temp/{p.aid}/{p.aid}.jpg") ? $"temp/{p.aid}/{p.aid}.jpg" : "",
                            lang,
                            subtitleInfo, audioOnly, videoOnly, p.aid, p.cid);
                        if (code != 0 || !File.Exists(outPath) || new FileInfo(outPath).Length == 0)
                        {
                            LogError("合併失敗"); continue;
                        }
                        Log("清理臨時文件...");
                        Thread.Sleep(200);
                        if (videoTracks.Count != 0) File.Delete(videoPath);
                    }
                    else
                    {
                        if (webJsonStr.Contains("平台不可观看"))
                        {
                            throw new Exception("當前(WEB)平台不可觀看，請嘗試使用TV API解析。");
                        }
                        else if (webJsonStr.Contains("购买后才能观看"))
                        {
                            throw new Exception("購買後才能觀看");
                        }
                        else if (webJsonStr.Contains("大会员专享限制"))
                        {
                            throw new Exception("大會員專享限制");
                        }
                        else if (webJsonStr.Contains("地区不可观看") || webJsonStr.Contains("地區不可觀看"))
                        {
                            throw new Exception("當前地區不可觀看，請嘗試使用代理解析。");
                        }
                        LogError("解析此分P失敗(使用--debug查看詳細信息)");
                        LogDebug("{0}", webJsonStr);
                        continue;
                    }
                }
                Log("任務完成");
            }
            catch (Exception e)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(e.Message);
                Console.ResetColor();
                Console.WriteLine();
                Thread.Sleep(1);
            }
        }
    }
}
