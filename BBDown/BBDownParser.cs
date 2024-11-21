﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BBDown.BBDownEntity;
using static BBDown.BBDownLogger;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownParser
    {
        private static async Task<string> GetPlayJsonAsync(bool onlyAvc, string aidOri, string aid, string cid, string epId, bool tvApi, bool intl, bool appApi, string qn = "0")
        {
            LogDebug("aid={0},cid={1},epId={2},tvApi={3},IntlApi={4},appApi={5},qn={6}", aid, cid, epId, tvApi, intl, appApi, qn);

            if (intl) return await GetPlayJsonAsync(aid, cid, epId, qn);


            bool cheese = aidOri.StartsWith("cheese:");
            bool bangumi = cheese || aidOri.StartsWith("ep:");
            LogDebug("bangumi={0},cheese={1}", bangumi, cheese);

            if (appApi) return BBDownAppHelper.DoReq(aid, cid, qn, bangumi, onlyAvc, Program.TOKEN);

            string prefix = tvApi ? (bangumi ? "api.snm0516.aisee.tv/pgc/player/api/playurltv" : "api.snm0516.aisee.tv/x/tv/ugc/playurl")
                        : (bangumi ? "api.bilibili.com/pgc/player/web/playurl" : "api.bilibili.com/x/player/playurl");
            string api = $"https://{prefix}?avid={aid}&cid={cid}&qn={qn}&type=&otype=json" + (tvApi ? "" : "&fourk=1") +
                $"&fnver=0&fnval=4048" + (tvApi ? "&device=android&platform=android" +
                "&mobi_app=android_tv_yst&npcybs=0&force_host=2&build=102801" +
                (Program.TOKEN != "" ? $"&access_key={Program.TOKEN}" : "") : "") +
                (bangumi ? $"&module=bangumi&ep_id={epId}&fourk=1" + "&session=" : "");
            if (tvApi && bangumi)
            {
                api = (Program.TOKEN != "" ? $"access_key={Program.TOKEN}&" : "") +
                    $"aid={aid}&appkey=4409e2ce8ffd12b8&build=102801" +
                    $"&cid={cid}&device=android&ep_id={epId}&expire=0" +
                    $"&fnval=80&fnver=0&fourk=1" +
                    $"&mid=0&mobi_app=android_tv_yst" +
                    $"&module=bangumi&npcybs=0&otype=json&platform=android" +
                    $"&qn={qn}&ts={GetTimeStamp(true)}";
                api = $"https://{prefix}?" + api + (bangumi ? $"&sign={GetSign(api)}" : "");
            }

            //課程介面
            if (cheese) api = api.Replace("/pgc/", "/pugv/");

            //Console.WriteLine(api);
            string webJson = await GetWebSourceAsync(api);
            //以下情況從網頁原始碼嘗試解析
            if (webJson.Contains("\"大会员专享限制\""))
            {
                string cookiePath = Directory.GetCurrentDirectory();
                if (!File.Exists($"invalid_cookie.txt"))
                {
                    //File.Delete($"{cookiePath}/cookie.txt");
                    //if (File.Exists($"BBDown.data")) { File.Delete($"{cookiePath}/BBDown.data"); }        
                    //File.Create($"{cookiePath}/cookie.txt");
                File.Create($"{cookiePath}/invalid_cookie.txt");
                }
                string webUrl = "https://www.bilibili.com/bangumi/play/ep" + epId;
                string webSource = await GetWebSourceAsync(webUrl);
                string retryWebJson = Regex.Match(webSource, @"window.__playinfo__=([\\s\\S]*?)<\\/script>").Groups[1].Value;
                if (!String.IsNullOrEmpty(webJson)) {
                    webJson = retryWebJson;
                }
            }
            return webJson;
        }

        private static async Task<string> GetPlayJsonAsync(string aid, string cid, string epId, string qn, string code = "0")
        {
            string api = $"https://api.biliintl.com/intl/gateway/v2/ogv/playurl?" +
                $"aid={aid}&cid={cid}&ep_id={epId}&platform=android&s_locale=zh_SG&prefer_code_type={code}&qn={qn}" + (Program.TOKEN != "" ? $"&access_key={Program.TOKEN}" : "");
            string webJson = await GetWebSourceAsync(api);
            return webJson;
        }

        public static async Task<(string, List<Video>, List<Audio>, List<string>, List<string>)> ExtractTracksAsync(bool onlyHevc, bool onlyAvc, string aidOri, string aid, string cid, string epId, bool tvApi, bool intlApi, bool appApi, string qn = "0")
        {
            List<Video> videoTracks = new List<Video>();
            List<Audio> audioTracks = new List<Audio>();
            List<string> clips = new List<string>();
            List<string> dfns = new List<string>();

            //調用解析
            string webJsonStr = await GetPlayJsonAsync(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi);

            var respJson = JsonDocument.Parse(webJsonStr);
            var data = respJson.RootElement;

            //intl介面
            if (webJsonStr.Contains("\"stream_list\""))
            {
                int pDur = data.GetProperty("data").GetProperty("video_info").GetProperty("timelength").GetInt32() / 1000;
                var audio = data.GetProperty("data").GetProperty("video_info").GetProperty("dash_audio").EnumerateArray().ToList();
                foreach(var stream in data.GetProperty("data").GetProperty("video_info").GetProperty("stream_list").EnumerateArray())
                {
                    JsonElement dashVideo;
                    if (stream.TryGetProperty("dash_video", out dashVideo))
                    {
                        if (dashVideo.GetProperty("base_url").ToString() != "")
                        {
                            Video v = new Video();
                            v.dur = pDur;
                            v.id = stream.GetProperty("stream_info").GetProperty("quality").ToString();
                            v.dfn = Program.qualitys[v.id];
                            v.bandwith = Convert.ToInt64(dashVideo.GetProperty("bandwidth").ToString()) / 1000;
                            v.baseUrl = dashVideo.GetProperty("base_url").ToString();
                            v.codecs = GetVideoCodec(dashVideo.GetProperty("codecid").ToString());
                            if (!videoTracks.Contains(v)) videoTracks.Add(v);
                        }
                    }
                }

                foreach(var node in audio)
                {
                    Audio a = new Audio();
                    a.id = node.GetProperty("id").ToString();
                    a.dfn = node.GetProperty("id").ToString();
                    a.dur = pDur;
                    a.bandwith = Convert.ToInt64(node.GetProperty("bandwidth").ToString()) / 1000;
                    a.baseUrl = node.GetProperty("base_url").ToString();
                    a.codecs = "M4A";
                    audioTracks.Add(a);
                }

                return (webJsonStr, videoTracks, audioTracks, clips, dfns);
            }

            if (webJsonStr.Contains("\"dash\":{")) //dash
            {
                List<JsonElement> audio = null;
                List<JsonElement> video = null;
                int pDur = 0;
                string nodeName = "data";
                if (webJsonStr.Contains("\"result\":{"))
                {
                    nodeName = "result";
                }

                try { pDur = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("duration").GetInt32() : respJson.RootElement.GetProperty("dash").GetProperty("duration").GetInt32(); } catch { }
                try { pDur = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("timelength").GetInt32() / 1000 : respJson.RootElement.GetProperty("timelength").GetInt32() / 1000; } catch { }

                bool reParse = false;
            reParse:
                if (reParse)
                {
                    webJsonStr = await GetPlayJsonAsync(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi, GetMaxQn());
                    respJson = JsonDocument.Parse(webJsonStr);
                }
                try { video = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("video").EnumerateArray().ToList() : respJson.RootElement.GetProperty("dash").GetProperty("video").EnumerateArray().ToList(); } catch { }
                try { audio = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("audio").EnumerateArray().ToList() : respJson.RootElement.GetProperty("dash").GetProperty("audio").EnumerateArray().ToList(); } catch { }
                if (video != null)
                {
                    foreach (var node in video)
                    {
                        Video v = new Video();
                        v.dur = pDur;
                        v.id = node.GetProperty("id").ToString();
                        v.dfn = Program.qualitys[v.id];
                        v.bandwith = Convert.ToInt64(node.GetProperty("bandwidth").ToString()) / 1000;
                        v.baseUrl = node.GetProperty("base_url").ToString();
                        v.codecs = GetVideoCodec(node.GetProperty("codecid").ToString());
                        v.size = node.TryGetProperty("size", out var sizeNode) ? Convert.ToDouble(sizeNode.ToString()) : 0;
                        if (!tvApi && !appApi)
                        {
                            v.res = node.GetProperty("width").ToString() + "x" + node.GetProperty("height").ToString();
                            v.fps = node.GetProperty("frame_rate").ToString();
                        }
                        if (onlyHevc && v.codecs != "HEVC") continue;
                        if (onlyAvc && v.codecs != "AVC") continue;
                        if (!videoTracks.Contains(v)) videoTracks.Add(v);
                    }
                }

                //此處處理免二壓影片，需要單獨再請求一次
                if (!reParse && !appApi)
                {
                    reParse = true;
                    goto reParse;
                }

                if (audio != null)
                {
                    foreach (var node in audio)
                    {
                        Audio a = new Audio();
                        a.id = node.GetProperty("id").ToString();
                        a.dfn = a.id;
                        a.dur = pDur;
                        a.bandwith = Convert.ToInt64(node.GetProperty("bandwidth").ToString()) / 1000;
                        a.baseUrl = node.GetProperty("base_url").ToString();
                        a.codecs = node.GetProperty("codecs").ToString().Replace("mp4a.40.2", "M4A");
                        audioTracks.Add(a);
                    }
                }
            }
            else if (webJsonStr.Contains("\"durl\":[")) //flv
            {
                //默認以最高清晰度解析
                webJsonStr = await GetPlayJsonAsync(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi, GetMaxQn());
                respJson = JsonDocument.Parse(webJsonStr);
                string quality = "";
                string videoCodecid = "";
                string url = "";
                double size = 0;
                double length = 0;
                if (webJsonStr.Contains("\"data\":{"))
                {
                    quality = respJson.RootElement.GetProperty("data").GetProperty("quality").ToString();
                    videoCodecid = respJson.RootElement.GetProperty("data").GetProperty("video_codecid").ToString();
                    //獲取所有分段
                    foreach (var node in respJson.RootElement.GetProperty("data").GetProperty("durl").EnumerateArray().ToList())
                    {
                        clips.Add(node.GetProperty("url").ToString());
                        size += node.GetProperty("size").GetDouble();
                        length += node.GetProperty("length").GetDouble();
                    }
                    //TV模式可用清晰度
                    JsonElement qnExtras;
                    JsonElement acceptQuality;
                    if (respJson.RootElement.GetProperty("data").TryGetProperty("qn_extras", out qnExtras)) 
                    {
                        foreach (var node in qnExtras.EnumerateArray())
                        {
                            dfns.Add(node.GetProperty("qn").ToString());
                        }
                    }
                    else if (respJson.RootElement.GetProperty("data").TryGetProperty("accept_quality", out acceptQuality)) //非tv模式可用清晰度
                    {
                        foreach (var node in acceptQuality.EnumerateArray())
                        {
                            string _qn = node.ToString();
                            if (_qn != null && _qn.Length > 0)
                                dfns.Add(node.ToString());
                        }
                    }
                }
                else
                {
                    //如果獲取數據失敗，嘗試從根路徑獲取數據
                    string nodeinfo = respJson.ToString();
                    var nodeJson = JsonDocument.Parse(nodeinfo).RootElement;
                    quality = nodeJson.GetProperty("quality").ToString();
                    videoCodecid = nodeJson.GetProperty("video_codecid").ToString();
                    //獲取所有分段
                    foreach (var node in nodeJson.GetProperty("durl").EnumerateArray())
                    {
                        clips.Add(node.GetProperty("url").ToString());
                        size += node.GetProperty("size").GetDouble();
                        length += node.GetProperty("length").GetDouble();
                    }
                    //TV模式可用清晰度
                    JsonElement qnExtras;
                    JsonElement acceptQuality;
                    if (nodeJson.TryGetProperty("qn_extras", out qnExtras))
                    {
                        //獲取可用清晰度
                        foreach (var node in qnExtras.EnumerateArray())
                        {
                            dfns.Add(node.GetProperty("qn").ToString());
                        }
                    }                   
                    else if (nodeJson.TryGetProperty("accept_quality", out acceptQuality)) //非tv模式可用清晰度
                    {
                        foreach (var node in acceptQuality.EnumerateArray())
                        {
                            string _qn = node.ToString();
                            if (_qn != null && _qn.Length > 0)
                                dfns.Add(node.ToString());
                        }
                    }
                }

                Video v = new Video();
                v.id = quality;
                v.dfn = Program.qualitys[quality];
                v.baseUrl = url;
                v.codecs = GetVideoCodec(videoCodecid);
                v.dur = (int)length / 1000;
                v.size = size;
                if (!videoTracks.Contains(v)) videoTracks.Add(v);
            }

            return (webJsonStr, videoTracks, audioTracks, clips, dfns);
        }
    }
}
