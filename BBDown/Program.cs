using Newtonsoft.Json.Linq;
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
using Newtonsoft.Json;
using OpenCC.NET;
using System.Text.RegularExpressions;

namespace BBDown
{
    class Program
    {
        public static string COOKIE = "";
        public static string TOKEN { get; set; } = "";

        public static Dictionary<string, string> qualitys = new Dictionary<string, string>() {
            {"125","HDR" }, {"120","4K" }, {"116","1080P60" },
            {"112","1080P" }, {"80","1080P" }, {"74","720P60" },
            {"64","720P" }, {"48","720P" }, {"32","480P" }, {"16","360P" }
        };

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
            public bool UseIntlApi { get; set; }
            public bool OnlyHevc { get; set; }
            public bool OnlyShowInfo { get; set; }
            public bool ShowAll { get; set; }
            public bool UseAria2c { get; set; }
            public bool Interactive { get; set; }
            public bool HideStreams { get; set; }
            public bool MultiThread { get; set; }
            public bool VideoOnly { get; set; }
            public bool AudioOnly { get; set; }
            public bool Debug { get; set; }
            public bool SkipMux { get; set; }
            public string SelectPage { get; set; } = "";
            public string Language { get; set; } = "";
            public string AccessToken { get; set; } = "";
            
            public override string ToString()
            {
                return $"{{Input={Url}, {nameof(UseTvApi)}={UseTvApi.ToString()}, " +
                    $"{nameof(UseIntlApi)}={UseIntlApi.ToString()}, " +
                    $"{nameof(OnlyHevc)}={OnlyHevc.ToString()}, " +
                    $"{nameof(OnlyShowInfo)}={OnlyShowInfo.ToString()}, " +
                    $"{nameof(Interactive)}={Interactive.ToString()}, " +
                    $"{nameof(HideStreams)}={HideStreams.ToString()}, " +
                    $"{nameof(ShowAll)}={ShowAll.ToString()}, " +
                    $"{nameof(UseAria2c)}={UseAria2c.ToString()}, " +
                    $"{nameof(MultiThread)}={MultiThread.ToString()}, " +
                    $"{nameof(VideoOnly)}={VideoOnly.ToString()}, " +
                    $"{nameof(AudioOnly)}={AudioOnly.ToString()}, " +
                    $"{nameof(Debug)}={Debug.ToString()}, " +
                    $"{nameof(SelectPage)}={SelectPage}, " +
                    $"{nameof(AccessToken)}={AccessToken}}}";
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
                    description: "影片地址 或 av|bv|BV|ep|ss"),
                new Option<bool>(
                    new string[]{ "--use-tv-api" ,"-tv"},
                    "使用TV端解析模式"),
                new Option<bool>(
					new string[]{ "--use-intl-api" ,"-intl"},
                    "使用国际版解析模式"),
                new Option<bool>(
                    new string[]{ "--only-hevc" ,"-hevc"},
                    "下載hevc編碼"),
                new Option<bool>(
                    new string[]{ "--only-show-info" ,"-info"},
                    "只解析不下載"),
                new Option<bool>(
                    new string[]{ "--hide-streams", "-hs"},
                    "不要顯示所有可用音視頻流"),
                new Option<bool>(
                    new string[]{ "--interactive", "-ia"},
                    "交互式選擇清晰度"),
                new Option<bool>(
                    new string[]{ "--show-all"},
                    "展示所有分P标题"),
                new Option<bool>(
                    new string[]{ "--use-aria2c"},
                    "调用aria2c进行下载(你需要自行准备好二进制可执行文件)"),
                new Option<bool>(
                    new string[]{ "--multi-thread", "-mt"},
                    "使用多線程下载"),
                new Option<string>(
                    new string[]{ "--select-page" ,"-p"},
                    "選擇指定分p或分p範圍"),
				new Option<bool>(
                    new string[]{ "--audio-only"},
                    "仅下载音频"),
                new Option<bool>(
                    new string[]{ "--video-only"},
                    "仅下载视频"),
                new Option<bool>(
                    new string[]{ "--debug"},
                    "輸出調試日誌"),
				new Option<bool>(
                    new string[]{ "--skip-mux"},
                    "跳過混流步驟"),
                new Option<string>(
                    new string[]{ "--language"},
                    "設置混流的音頻語言(代碼)，如chi, jpn等"),
                new Option<string>(
                    new string[]{ "--access-token" ,"-a"},
                    "設置access_token用以下載TV接口的會員內容")
            };

            Command loginCommand = new Command(
                "login",
                "通過APP掃描二維碼以登錄您的WEB賬號");
            rootCommand.AddCommand(loginCommand);
            Command loginTVCommand = new Command(
                "logintv",
                "通過APP掃描二維碼以登錄您的TV賬號");
            rootCommand.AddCommand(loginTVCommand);
            rootCommand.Description = "BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟件.";
            rootCommand.TreatUnmatchedTokensAsErrors = true;

            //WEB登录
            loginCommand.Handler = CommandHandler.Create(delegate
            {
                try
                {
                    Log("獲取登錄地址...");
                    string loginUrl = "https://passport.bilibili.com/qrcode/getLoginUrl";
                    string url = JObject.Parse(GetWebSource(loginUrl))["data"]["url"].ToString();
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
                        Thread.Sleep(1000);
                        string w = GetLoginStatus(oauthKey);
                        string data = JObject.Parse(w)["data"].ToString();
                        if (data == "-2")
                        {
                            LogColor("二維碼已過期, 請重新執行登錄指令.");
                            break;
                        }
                        else if (data == "-4") //等待扫码
                        {
                            continue;
                        }
                        else if (data == "-5") //等待确认
                        {
                            if (!flag)
                            {
                                Log("掃碼成功, 請確認...");
                                flag = !flag;
                            }
                        }
                        else
                        {
                            string cc = JObject.Parse(w)["data"]["url"].ToString();
                            string cookiePath = Directory.GetCurrentDirectory();
                            Log("登录成功: SESSDATA=" + GetQueryString("SESSDATA", cc));
                            //导出cookie
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
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "BBDown.data"), "SESSDATA=" + GetQueryString("SESSDATA", cc));
                            File.Delete("qrcode.png");
                            break;
                        }
                    }
                }
                catch (Exception e) { LogError(e.Message); }
            });

            //TV登录
            loginTVCommand.Handler = CommandHandler.Create(delegate
            {
                try
                {
                    string loginUrl = "https://passport.snm0516.aisee.tv/x/passport-tv-login/qrcode/auth_code";
                    string pollUrl = "https://passport.bilibili.com/x/passport-tv-login/qrcode/poll";
                    var parms = GetTVLoginParms();
                    Log("獲取登錄地址...");
                    WebClient webClient = new WebClient();
                    byte[] responseArray = webClient.UploadValues(loginUrl, parms);
                    string web = Encoding.UTF8.GetString(responseArray);
                    string url = JObject.Parse(web)["data"]["url"].ToString();
                    string authCode = JObject.Parse(web)["data"]["auth_code"].ToString();
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
                        Thread.Sleep(1000);
                        responseArray = webClient.UploadValues(pollUrl, parms);
                        web = Encoding.UTF8.GetString(responseArray);
                        string code = JObject.Parse(web)["code"].ToString();
                        if (code == "86038")
                        {
                            LogColor("二維碼已過期, 請重新執行登錄指令.");
                            break;
                        }
                        else if (code == "86039") //等待扫码
                        {
                            continue;
                        }
                        else
                        {
                            string cc = JObject.Parse(web)["data"]["access_token"].ToString();
                            string cookiePath = Directory.GetCurrentDirectory();
                            Log("登錄成功: AccessToken=" + cc);
                            //导出cookie
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
                                    sw.Write("access_token=" + cc);
                                }
                            }
                            else
                            {
                                using (StreamWriter sw = new StreamWriter($"{cookiePath}/cookie.txt"))    
                                {
                                    sw.Write("access_token=" + cc);
                                }
                            }
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "BBDownTV.data"), "access_token=" + cc);
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
            Console.Write($"BBDown version {ver.Major}.{ver.Minor}.{ver.Build}, Bilibili Downloader.\r\n");
            Console.ResetColor();
            Console.Write("BBDown Server Edition");
            Console.WriteLine();
            //检测更新
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
                if (cookieString != null) {cookieString = "";}
                LogDebug(cookieString);
                bool interactMode = myOption.Interactive;
                bool infoMode = myOption.OnlyShowInfo;
                bool tvApi = myOption.UseTvApi;
				bool intlApi = myOption.UseIntlApi;
                bool hevc = myOption.OnlyHevc;
                bool hideStreams = myOption.HideStreams;
                bool multiThread = myOption.MultiThread;
                bool audioOnly = myOption.AudioOnly;
                bool videoOnly = myOption.VideoOnly;
                bool skipMux = myOption.SkipMux;
                bool showAll = myOption.ShowAll;
                bool useAria2c = myOption.UseAria2c;
                DEBUG_LOG = myOption.Debug;
                string input = myOption.Url;
                string lang = myOption.Language;
                string selectPage = myOption.SelectPage.ToUpper();
                string aidOri = ""; //原始aid
                COOKIE = cookieString;
                TOKEN = myOption.AccessToken.Replace("access_token=", "");

                //audioOnly和videoOnly同时开启则全部忽视
                if (audioOnly && videoOnly)
                {
                    audioOnly = false;
                    videoOnly = false;
                }
                
                List<string> selectedPages = null;
                if (!string.IsNullOrEmpty(GetQueryString("p", input)))
                {
                    selectedPages = new List<string>();
                    selectedPages.Add(GetQueryString("p", input));
                }

                LogDebug("運行參數：{0}", myOption);
                if (string.IsNullOrEmpty(COOKIE) && File.Exists(Path.Combine(AppContext.BaseDirectory, "BBDown.data")) && !tvApi)
                {
                    Log("加載本地cookie...");
                    LogDebug("文件路徑：{0}", Path.Combine(AppContext.BaseDirectory, "BBDown.data"));
                    COOKIE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BBDown.data"));
                }
                if (string.IsNullOrEmpty(TOKEN) && File.Exists(Path.Combine(AppContext.BaseDirectory, "BBDownTV.data")) && tvApi)
                    {
                    Log("加載本地token...");
                    LogDebug("文件路徑：{0}", Path.Combine(AppContext.BaseDirectory, "BBDownTV.data"));
                    TOKEN = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BBDownTV.data"));
                    TOKEN = TOKEN.Replace("access_token=", "");
                }
                Log("獲取aid...");
                aidOri = await GetAvIdAsync(input);
                Log("獲取aid結束: " + aidOri);
                //-p的优先级大于URL中的自带p参数，所以先清空selectedPages
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
                Log("獲取視頻信息...");
                IFetcher fetcher = new BBDownNormalInfoFetcher();
                if (aidOri.StartsWith("cheese"))
                {
                    fetcher = new BBDownCheeseInfoFetcher();
                }
                else if (aidOri.StartsWith("ep"))
                {
                    fetcher = new BBDownBangumiInfoFetcher();
                }									

                var vInfo = fetcher.Fetch(aidOri);
                string title = vInfo.Title;
                string desc = vInfo.Desc;
                string pic = vInfo.Pic;
                string pubTime = vInfo.PubTime;
                LogColor("視頻標題: " + title);
                LogDebug("發佈時間: " + pubTime);
                List<Page> pagesInfo = vInfo.PagesInfo;
                List<Subtitle> subtitleInfo = new List<Subtitle>();
                bool more = false;
                bool bangumi = vInfo.IsBangumi;
                bool cheese = vInfo.IsCheese;

                //打印分P信息
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
                        LogDebug($"P{p.index}: [{p.cid}] [{p.title}] [{FormatTime(p.dur)}]");
                    }
                }
                if (selectPage == "LATEST")
                {
                    selectedPages = new List<string>();
                    selectedPages.Add(pagesInfo.Count.ToString());
                    LogDebug(pagesInfo.Count.ToString());
                }
                //如果用户没有选择分P，根据epid来确定某一集
                if (selectedPages == null && selectPage != "ALL" && !string.IsNullOrEmpty(vInfo.Index) && selectPage != "LATEST")
                {
                    selectedPages = new List<string> { vInfo.Index };
                    Log("程序已自動選擇你輸入的集數，如果要下載其他集數請自行指定分P(可使用參數-p ALL代表全部 -p LATEST代表最新一集)");
                }
                
                Log($"共計 {pagesInfo.Count} 個分P, 已選擇：" + (selectedPages == null ? "ALL" : string.Join(",", selectedPages)));

                //过滤不需要的分P
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
                        if (!File.Exists($"temp/{p.aid}/{p.aid}.jpg"))
                        {
                            Log("下載封面...");
                            LogDebug("下載：{0}", pic);
                            new WebClient().DownloadFile(pic, $"temp/{p.aid}/{p.aid}.jpg");
                        }
                        string[] files = System.IO.Directory.GetFiles(Directory.GetCurrentDirectory(), $"temp/{p.aid}/{p.aid}.{p.cid}.*.srt");
                        if (files.Length > 0)
                        {
                            Log("字幕已經獲取...");
                            for (int i = 0; i<files.Length; i++)
                            { LogDebug(files[i]); }
                            
                        }
                        else
                        {
                            LogDebug("獲取字幕...");
                            subtitleInfo = BBDownSubUtil.GetSubtitles(p.aid, p.cid, p.epid, intlApi);
                            foreach (Subtitle s in subtitleInfo)
                            {
                                Log($"下載字幕 {s.lan} => {BBDownSubUtil.SubDescDic[s.lan]}...");
                                LogDebug("下載：{0}", s.url);
                                BBDownSubUtil.SaveSubtitle(s.url, s.path);
                            }
                        }

                    }
                    string webJsonStr = "";
                    List<Video> videoTracks = new List<Video>();
                    List<Audio> audioTracks = new List<Audio>();
                    List<string> clips = new List<string>();
                    List<string> dfns = new List<string>();
                    string indexStr = p.index.ToString("0".PadRight(pagesInfo.OrderByDescending(_p => _p.index).First().index.ToString().Length, '0'));
                    string videoPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.mp4";
                    string audioPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.m4a";
                    //处理文件夹以.结尾导致的异常情况															  
                    if (title.EndsWith(".")) title += "_fix";
                    var converter = new OpenChineseConverter();
                    title = converter.ToTaiwanFromSimplified(title);
                    Log(title);
                    string ep = p.index.ToString("D2");
                    //讀取JSON存放
                    string jsonpath = Directory.GetCurrentDirectory();
                    string jsonfile = File.ReadAllText($"{jsonpath}/config.json");
                    dynamic json = JValue.Parse(jsonfile);
                    string dirname = json.dir;
                    title = Regex.Replace(title, @"[<>:""/\\|?*]", "-");

                    
                    //调用解析
                    (webJsonStr, videoTracks, audioTracks, clips, dfns) = ExtractTracks(hevc, aidOri, p.aid, p.cid, p.epid, tvApi, intlApi);
                    //File.WriteAllText($"debug.json", JObject.Parse(webJsonStr).ToString());
                    JObject respJson = JObject.Parse(webJsonStr);
                    string outPath = dirname + (pagesInfo.Count > 1 ? $"/{json.prefix}{title}[{ep}][0000P]{json.suffix}" +
                    $".mp4" : $"/{json.prefix}{title}[{ep}][0000P]{json.suffix}.mp4");
                    //此处代码简直灾难，后续优化吧
                    if ((videoTracks.Count != 0 || audioTracks.Count != 0) && clips.Count == 0)   //dash
                    {
                        if (webJsonStr.Contains("\"video\":[") && videoTracks.Count == 0)

                        {
                            LogError("沒有找到符合要求的視頻流");
                            if (!audioOnly) continue;
                        }
                        if (webJsonStr.Contains("\"audio\":[") && audioTracks.Count == 0)
                        {
                            LogError("沒有找到符合要求的音頻流");
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
                            //展示所有的音视频流信息
                            if (videoTracks.Count > 0)
                            {
                                Log($"共計{videoTracks.Count}條視頻流.");
                                int index = 0;
                                foreach (var v in videoTracks)
                                {
                                    int pDur = p.dur == 0 ? v.dur : p.dur;
                                    LogColor($"{index++}. [{v.dfn}] [{v.res}] [{v.codecs}] [{v.fps}] [{v.bandwith} kbps] [~{FormatFileSize(pDur * v.bandwith * 1024 / 8)}]".Replace("[] ", ""), false);
                                    if (infoMode) Console.WriteLine(v.baseUrl);
                                }
                            }
                            if (audioTracks.Count > 0)
                            {
                                Log($"共計{audioTracks.Count}條音頻流.");
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
                                Log("請選擇一條視頻流(輸入序號): ", false);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                vIndex = Convert.ToInt32(Console.ReadLine());
                                if (vIndex > videoTracks.Count || vIndex < 0) vIndex = 0;
                                Console.ResetColor();
                            }
                            if (audioTracks.Count > 0)
                            {
                                Log("請選擇一條音頻流(輸入序號): ", false);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                aIndex = Convert.ToInt32(Console.ReadLine());
                                if (aIndex > audioTracks.Count || aIndex < 0) aIndex = 0;
                                Console.ResetColor();
                            }
                        }

                        Log($"已選擇的流:");
                        if (videoTracks.Count > 0)
                            LogColor($"[視頻] [{videoTracks[vIndex].dfn}] [{videoTracks[vIndex].res}] [{videoTracks[vIndex].codecs}] [{videoTracks[vIndex].fps}] [{videoTracks[vIndex].bandwith} kbps] [~{FormatFileSize(videoTracks[vIndex].dur * videoTracks[vIndex].bandwith * 1024 / 8)}]".Replace("[] ", ""), false);
                        if (audioTracks.Count > 0)
                            LogColor($"[音頻] [{audioTracks[aIndex].codecs}] [{audioTracks[aIndex].bandwith} kbps] [~{FormatFileSize(audioTracks[aIndex].dur * audioTracks[aIndex].bandwith * 1024 / 8)}]", false);

                        outPath = dirname + (pagesInfo.Count > 1 ? $"/{json.prefix}{title}[{ep}][{videoTracks[vIndex].dfn}]{json.suffix}" +
                        $".mp4" : $"/{json.prefix}{title}[{ep}][{videoTracks[vIndex].dfn}]{json.suffix}.mp4");

                        if (File.Exists(outPath) && new FileInfo(outPath).Length != 0)
                        {
                            Log($"{outPath}已存在, 跳過下載...");
                            continue;
                        }
								
                            if (videoTracks.Count > 0)
                            {
                                if (multiThread && !videoTracks[vIndex].baseUrl.Contains("-cmcc-"))
                                {
                                    Log($"開始多線程下載P{p.index}視頻...");
                                    await MultiThreadDownloadFileAsync(videoTracks[vIndex].baseUrl, videoPath, useAria2c);
                                    Log("合併視頻分片...");
                                    CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(videoPath), ".vclip"), videoPath);
                                    Log("清理分片...");
                                    foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                                }
                                else
                                {
                                    if (multiThread && videoTracks[vIndex].baseUrl.Contains("-cmcc-"))
                                        LogError("檢測到cmcc域名cdn, 已經禁用多線程");
                                    Log($"開始下載P{p.index}視頻...");
                                    await DownloadFile(videoTracks[vIndex].baseUrl, videoPath, useAria2c);
                                }

                            }
                            if (audioTracks.Count > 0)
                            {
                                if (multiThread && !audioTracks[aIndex].baseUrl.Contains("-cmcc-"))
                                {
                                    Log($"開始多線程下載P{p.index}音頻...");
                                    await MultiThreadDownloadFileAsync(audioTracks[aIndex].baseUrl, audioPath, useAria2c);
                                    Log("合併音頻分片...");
                                    CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(audioPath), ".aclip"), audioPath);
                                    Log("清理分片...");
                                    foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                                }
                                else
                                {
                                    if (multiThread && audioTracks[aIndex].baseUrl.Contains("-cmcc-"))
                                        LogError("檢測到cmcc域名cdn, 已經禁用多線程");
                                    Log($"開始下載P{p.index}音頻...");
                                    await DownloadFile(audioTracks[aIndex].baseUrl, audioPath, useAria2c);
                                }
                            }

                            Log($"下載P{p.index}完畢");
                            if (videoTracks.Count == 0) videoPath = "";
                            if (audioTracks.Count == 0) audioPath = "";

                            if (skipMux) continue;
                            Log("開始合併音視頻" + (subtitleInfo.Count > 0 ? "和字幕" : "") + "...");
                            int code = MuxAV(videoPath, audioPath, outPath,
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
                            if (videoTracks.Count > 0) File.Delete(videoPath);
                            if (audioTracks.Count > 0) File.Delete(audioPath);
                        }
                        else if (clips.Count > 0 && dfns.Count > 0)  //flv										
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
                                (webJsonStr, videoTracks, audioTracks, clips, dfns) = ExtractTracks(hevc, aidOri, p.aid, p.cid, p.epid, tvApi, intlApi, dfns[vIndex]);
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
                                videoPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.{i.ToString(pad)}.mp4";
                                if (multiThread && !link.Contains("-cmcc-"))
                                {
                                    if (videoTracks.Count != 0)
                                    {
                                        Log($"開始多線程下載P{p.index}視頻, 片段({(i + 1).ToString(pad)}/{clips.Count})...");
                                        await MultiThreadDownloadFileAsync(link, videoPath, useAria2c);
                                        Log("合併視頻分片...");
                                        CombineMultipleFilesIntoSingleFile(GetFiles(Path.GetDirectoryName(videoPath), ".vclip"), videoPath);
                                    }
                                    Log("清理分片...");
                                    foreach (var file in new DirectoryInfo(Path.GetDirectoryName(videoPath)).EnumerateFiles("*.?clip")) file.Delete();
                                }
                                else
                                {
                                    if (multiThread && link.Contains("-cmcc-"))
                                        LogError("檢測到cmcc域名cdn, 已經禁用多線程");
                                    if (videoTracks.Count != 0)
                                    {
                                        Log($"開始下載P{p.index}視頻, 片段({(i + 1).ToString(pad)}/{clips.Count})...");
                                        await DownloadFile(link, videoPath, useAria2c);
                                    }
                                }
                            }
                            Log($"下載P{p.index}完畢");
                            Log("開始合併分段...");
                            var files = GetFiles(Path.GetDirectoryName(videoPath), ".mp4");
                            videoPath = $"temp/{p.aid}/{p.aid}.P{indexStr}.{p.cid}.mp4";
                            MergeFLV(files, videoPath);
                            Log("開始混流視頻" + (subtitleInfo.Count > 0 ? "和字幕" : "") + "...");
                            int code = MuxAV(videoPath, "", outPath,
                                desc,
                                title,
                                vInfo.PagesInfo.Count > 1 ? ($"P{indexStr}.{p.title}") : "",
                                File.Exists($"temp/{p.aid}/{p.aid}.jpg") ? $"{p.aid}/{p.aid}.jpg" : "",
                                lang,
                                subtitleInfo, audioOnly, videoOnly, p.aid, p.cid);
                            if (code != 0 || !File.Exists(outPath) || new FileInfo(outPath).Length == 0)
                            {
                                LogError("合併失敗"); continue;
                            }
                            Log("清理臨時文件...");
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
