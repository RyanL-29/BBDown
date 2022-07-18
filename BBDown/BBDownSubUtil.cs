﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using static BBDown.BBDownLogger;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Fanhuaji_API;

namespace BBDown
{
    class BBDownSubUtil
    {
        public static async Task<List<Subtitle>> GetSubtitlesAsync(string aid, string cid, string epId, bool intl)
        {
            List<Subtitle> subtitles = new List<Subtitle>();
            if (intl)
            {
                try
                {
                    string api = $"https://api.bilibili.tv/intl/gateway/web/v2/subtitle?&episode_id={epId}";
                    string json = await GetWebSourceAsync(api);
                    using var infoJson = JsonDocument.Parse(json);
                    var subs = infoJson.RootElement.GetProperty("data").GetProperty("subtitles").EnumerateArray();
                    foreach (var sub in subs)
                    {
                        Subtitle subtitle = new Subtitle();
                        subtitle.url = sub.GetProperty("url").ToString();
                        subtitle.lan = sub.GetProperty("lang_key").ToString();
                        subtitle.path = $"temp/{aid}/{aid}.{cid}.{subtitle.lan}.srt";
                        subtitles.Add(subtitle);
                        if (subtitle.lan == "zh-hans" || subtitle.lan == "zh-Hans" || subtitle.lan == "cmn-hans" || subtitle.lan == "zh-CN" || subtitle.lan == "zh-Hant" || subtitle.lan == "zh-hant")
                        {
                            Log("字幕修正中: 簡轉繁...");
                            Subtitle subtitleb = new Subtitle();
                            subtitleb.url = sub.GetProperty("url").ToString();
                            subtitleb.lan = "chs-cht";
                            subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                            subtitles.Add(subtitleb);
                        }
                        if (subtitle.lan == "zh-hant" || subtitle.lan == "zh-Hant" || subtitle.lan == "cmn-hant" || subtitle.lan == "zh-TW" || subtitle.lan == "zh-HK" || subtitle.lan == "zh-MO")
                        {
                            Log("字幕修正中: 繁轉簡...");
                            Subtitle subtitleb = new Subtitle();
                            subtitleb.url = sub.GetProperty("url").ToString();
                            subtitleb.lan = "cht-chs";
                            subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                            subtitles.Add(subtitleb);
                        }
                    }
                    return subtitles;
                }
                catch (Exception) { return subtitles; } //返回空列表
            }

            try
            {
                string api = $"https://api.bilibili.com/x/web-interface/view?aid={aid}&cid={cid}";
                string json = await GetWebSourceAsync(api);
                using var infoJson = JsonDocument.Parse(json);
                var subs = infoJson.RootElement.GetProperty("data").GetProperty("subtitle").GetProperty("list").EnumerateArray();
                foreach (var sub in subs)
                {
                    Subtitle subtitle = new Subtitle();
                    subtitle.url = sub.GetProperty("subtitle_url").ToString();
                    subtitle.lan = sub.GetProperty("lan").ToString();
                    subtitle.path = $"temp/{aid}/{aid}.{cid}.{subtitle.lan}.srt";
                    subtitles.Add(subtitle);
                    if (subtitle.lan == "zh-hans" || subtitle.lan == "zh-Hans" || subtitle.lan == "cmn-hans" || subtitle.lan == "zh-CN" || subtitle.lan == "zh-Hant" || subtitle.lan == "zh-hant")
                    {
                        Log("字幕修正中: 簡轉繁...");
                        Subtitle subtitleb = new Subtitle();
                        subtitleb.url = sub.GetProperty("subtitle_url").ToString();
                        subtitleb.lan = "chs-cht";
                        subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                        subtitles.Add(subtitleb);
                    }
                    if (subtitle.lan == "zh-hant" || subtitle.lan == "zh-Hant" || subtitle.lan == "cmn-hant" || subtitle.lan == "zh-TW" || subtitle.lan == "zh-HK" || subtitle.lan == "zh-MO")
                    {
                        Log("字幕修正中: 繁轉簡...");
                        Subtitle subtitleb = new Subtitle();
                        subtitleb.url = sub.GetProperty("subtitle_url").ToString();
                        subtitleb.lan = "cht-chs";
                        subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                        subtitles.Add(subtitleb);
                    }
                }
                //無字幕片源 但是字幕沒上導致的空列表，嘗試從國際介面獲取
                //if (subtitles.Count == 0)
                //{
                    //return await GetSubtitlesAsync(aid, cid, epId, true);
                //}
                return subtitles;
            }
            catch (Exception)
            {
                try
                {
                    //grpc調用介面 protobuf
                    string api = "https://app.biliapi.net/bilibili.community.service.dm.v1.DM/DmView";
                    int _aid = Convert.ToInt32(aid);
                    int _cid = Convert.ToInt32(cid);
                    int _type = 1;
                    byte[] data = new byte[18];
                    data[0] = 0x0; data[1] = 0x0; data[2] = 0x0; data[3] = 0x0; data[4] = 0xD; //先固定死了
                    int i = 5;
                    data[i++] = Convert.ToByte(1 << 3 | 0); // index=1
                    while ((_aid & -128) != 0)
                    {
                        data[i++] = Convert.ToByte((_aid & 127) | 128);
                        _aid = _aid >> 7;
                    }
                    data[i++] = Convert.ToByte(_aid);
                    data[i++] = Convert.ToByte(2 << 3 | 0); // index=2
                    while ((_cid & -128) != 0)
                    {
                        data[i++] = Convert.ToByte((_cid & 127) | 128);
                        _cid = _cid >> 7;
                    }
                    data[i++] = Convert.ToByte(_cid);
                    data[i++] = Convert.ToByte(3 << 3 | 0); // index=3
                    data[i++] = Convert.ToByte(_type);
                    string t = await GetPostResponseAsync(api, data);
                    Regex reg = new Regex("(zh-Han[st]).*?(http.*?\\.json)");
                    foreach (Match m in reg.Matches(t))
                    {
                        Subtitle subtitle = new Subtitle();
                        subtitle.url = m.Groups[2].Value;
                        subtitle.lan = m.Groups[1].Value;
                        subtitle.path = $"temp/{aid}/{aid}.{cid}.{subtitle.lan}.srt";
                        subtitles.Add(subtitle);
                        if (subtitle.lan == "zh-hans" || subtitle.lan == "zh-Hans" || subtitle.lan == "cmn-hans" || subtitle.lan == "zh-CN" || subtitle.lan == "zh-Hant" || subtitle.lan == "zh-hant")
                        {
                            Log("字幕修正中: 簡轉繁...");
                            Subtitle subtitleb = new Subtitle();
                            subtitleb.url = m.Groups[2].Value;
                            subtitleb.lan = "chs-cht";
                            subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                            subtitles.Add(subtitleb);
                        }
                        else if (subtitle.lan == "zh-hant" || subtitle.lan == "zh-Hant" || subtitle.lan == "cmn-hant" || subtitle.lan == "zh-TW" || subtitle.lan == "zh-HK" || subtitle.lan == "zh-MO")
                        {
                            Log("字幕修正中: 繁轉簡...");
                            Subtitle subtitleb = new Subtitle();
                            subtitleb.url = m.Groups[2].Value;
                            subtitleb.lan = "cht-chs";
                            subtitleb.path = $"temp/{aid}/{aid}.{cid}.{subtitleb.lan}.srt";
                            subtitles.Add(subtitleb);
                        }
                    }
                    return subtitles;
                }
                catch (Exception) { return subtitles; } //返回空列表
            }
        }

        public static async Task SaveSubtitleAsync(string url, string path)
        {
            if (path.Contains("chs-cht") == true)
            {
                File.WriteAllText(path, await ConvertSubFromJsonCHSCHT(await GetWebSourceAsync(url)), new UTF8Encoding());
            }
            else if (path.Contains("cht-chs") == true) {
                File.WriteAllText(path, await ConvertSubFromJsonCHTCHS(await GetWebSourceAsync(url)), new UTF8Encoding());
            }
            else
            {
                File.WriteAllText(path, ConvertSubFromJson(await GetWebSourceAsync(url)), new UTF8Encoding());
            }
        }

        private static string ConvertSubFromJson(string jsonString)
        {
            StringBuilder lines = new StringBuilder();
            var json = JsonDocument.Parse(jsonString);
            var sub = json.RootElement.GetProperty("body").EnumerateArray().ToList();
            for(int i = 0; i < sub.Count; i++)
            {
                var line = sub[i];
                lines.AppendLine((i + 1).ToString());
                JsonElement from;
                JsonElement content;
                if (line.TryGetProperty("from", out from)) 
                {
                    lines.AppendLine($"{FormatTime(from.ToString())} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                else
                {
                    lines.AppendLine($"{FormatTime("0")} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                //有的沒有內容
                if (line.TryGetProperty("content", out content))
                    lines.AppendLine(content.ToString());
                lines.AppendLine();
            }
            return lines.ToString();
        }

        private static async Task<string> ConvertSubFromJsonCHSCHT(string jsonString)
        {
            //Old Open-CC
            //var converter = new OpenChineseConverter();
            //string jsonString2 = converter.ToTaiwanFromSimplifiedWithPhrases(jsonString);

            //Fanhuaji-API
            var Fanhuaji = new Fanhuaji(Agree: true, Terms_of_Service: Fanhuaji_API.Fanhuaji.Terms_of_Service);
            var subObj = await Fanhuaji.ConvertAsync(jsonString, Fanhuaji_API.Enum.Enum_Converter.Traditional, new Config() { });
            StringBuilder lines = new();
            var json = JsonDocument.Parse(subObj.Data.Text);
            var sub = json.RootElement.GetProperty("body").EnumerateArray().ToList();
            for (int i = 0; i < sub.Count; i++)
            {
                var line = sub[i];
                lines.AppendLine((i + 1).ToString());
                JsonElement from;
                JsonElement content;
                if (line.TryGetProperty("from", out from))
                {
                    lines.AppendLine($"{FormatTime(from.ToString())} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                else
                {
                    lines.AppendLine($"{FormatTime("0")} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                //有的沒有內容
                if (line.TryGetProperty("content", out content))
                    lines.AppendLine(content.ToString());
                lines.AppendLine();
            }
            return lines.ToString();
        }

        private static async Task<string> ConvertSubFromJsonCHTCHS(string jsonString)
        {   //Old Open-CC
            //var converter = new OpenChineseConverter();
            //string jsonString2 = converter.ToSimplifiedFromTraditional(jsonString);
            
            //Fanhuaji-API
            var Fanhuaji = new Fanhuaji(Agree: true, Terms_of_Service: Fanhuaji_API.Fanhuaji.Terms_of_Service);
            var subObj = await Fanhuaji.ConvertAsync(jsonString, Fanhuaji_API.Enum.Enum_Converter.China, new Config() { });
            StringBuilder lines = new StringBuilder();
            var json = JsonDocument.Parse(subObj.Data.Text);
            var sub = json.RootElement.GetProperty("body").EnumerateArray().ToList();
            for (int i = 0; i < sub.Count; i++)
            {
                var line = sub[i];
                lines.AppendLine((i + 1).ToString());
                JsonElement from;
                JsonElement content;
                if (line.TryGetProperty("from", out from))
                {
                    lines.AppendLine($"{FormatTime(from.ToString())} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                else
                {
                    lines.AppendLine($"{FormatTime("0")} --> {FormatTime(line.GetProperty("to").ToString())}");
                }
                //有的沒有內容
                if (line.TryGetProperty("content", out content))
                    lines.AppendLine(content.ToString());
                lines.AppendLine();
            }
            return lines.ToString();
        }

        private static string FormatTime(string sec) //64.13
        {
            string[] v = { sec, "" };
            if (sec.Contains("."))
                v = sec.Split('.');
            v[1] = v[1].PadRight(3, '0').Substring(0, 3);
            int secs = Convert.ToInt32(v[0]);
            TimeSpan ts = new TimeSpan(0, 0, secs);
            string str = "";
            str = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00") + "," + v[1];
            return str;
        }

        public static Dictionary<string, string> SubDescDic = new Dictionary<string, string>
        {
            {"ar", "العربية"}, {"ar-eg", "العربية"},
            {"bg", "български"}, {"cmn-hans", "國語（簡體）"},
            {"cmn-hant", "國語（繁體）"}, {"cs", "čeština"},
            {"da", "Dansk"}, {"da-dk", "Dansk"},
            {"de", "Deutsch"}, {"de-de", "Deutsch"},
            {"el", "Ελληνικά"}, {"en", "English"},
            {"en-US", "English"}, {"es", "Español (Latinoamérica)"},
            {"es-419", "Español (Latinoamérica)"}, {"es-es", "Español (España)"},
            {"es-ES", "Español (España)"}, {"fi", "Suomi"},
            {"fi-fi", "Suomi"}, {"fr", "Français"},
            {"fr-fr", "Français"}, {"he", "עברית"},
            {"he-il", "עברית"}, {"hi", "हिन्दी"},
            {"hi-in", "हिन्दी"}, {"hr", "Hrvatska"},
            {"id", "Indonesia"}, {"id-id", "Indonesia"},
            {"it", "Italiano"}, {"it-it", "Italiano"},
            {"ja", "日本語"}, {"ja-ja", "日本語"},
            {"jp", "日本語"}, {"jp-jp", "日本語"},
            {"ko", "한국어"}, {"ko-kr", "한국어"},
            {"ms", "Melayu"}, {"nb", "Norsk Bokmål"},
            {"nb-no", "Norsk Bokmål"}, {"nl", "Nederlands"},
            {"nl-BE", "Nederlands"}, {"nl-be", "Nederlands"},
            {"nl-nl", "Nederlands"}, {"nob", "norsk"},
            {"pl", "Polski"}, {"pl-pl", "Polski"},
            {"pt", "Português"}, {"pt-BR", "Português"},
            {"pt-br", "Português"}, {"ro", "Română"},
            {"ru", "Русский"}, {"ru-ru", "Русский"},
            {"sk", "slovenský"}, {"sv", "Svenska"},
            {"sv-se", "Svenska"}, {"ta-in", "தமிழ்"},
            {"te-in", "తెలుగు"}, {"th", "ไทย"},
            {"tl", "Tagalog"}, {"tr", "Türkçe"},
            {"tr-tr", "Türkçe"}, {"uk", "Українська"},
            {"vi", "Tiếng Việt"}, {"zxx", "zxx"},
            {"zh-hans", "中文（簡體）"},
            {"zh-Hans", "中文（簡體）"},
            {"zh-CN", "中文（簡體）"},
            {"zh-TW", "中文（繁體）"},
            {"zh-HK", "中文（繁體）"},
            {"zh-MO", "中文（繁體）"},
            {"zh-Hant", "中文（繁體）"},
            {"zh-hant", "中文（繁體）"},
            {"yue", "中文（粵語）"},
            {"hu", "Magyar"},
            {"et", "Eestlane"}, {"bn", "বাংলা ভাষার"},
            {"iw", "שפה עברית"}, {"sr", "српски језик"},
            {"hy", "հայերեն"}, {"az", "Azərbaycan"},
            {"kk", "Қазақ тілі"}, {"is", "icelandic"},
            {"fil", "Pilipino"}, {"ku", "Kurdî"},
            {"ca", "català"}, {"no", "norsk språk"},
            {"chs-cht", "中文（簡轉繁）"}, {"cht-chs", "中文（繁轉簡）"}
        };

        public static Dictionary<string, string> SubLangDic = new Dictionary<string, string> 
        {
            {"ar","ara"}, {"ar-eg","ara"},
            {"bg","bul"}, {"cmn-hans","chi"},
            {"cmn-hant","chi"}, {"cs","cze"},
            {"da","dan"}, {"da-dk","dan"},
            {"de","ger"}, {"de-de","ger"},
            {"el","gre"}, {"en","eng"},
            {"en-US","eng"}, {"es","spa"},
            {"es-419","spa"}, {"es-ES","spa"},
            {"es-es","spa"}, {"fi","fin"},
            {"fi-fi","fin"}, {"fr","fre"},
            {"fr-fr","fre"}, {"he","heb"},
            {"he-il","heb"}, {"hi","hin"},
            {"hi-in","hin"}, {"hr","hrv"},
            {"id","ind"}, {"id-id","ind"},
            {"it","ita"}, {"it-it","ita"},
            {"ja","jpn"}, {"ja-ja","jpn"},
            {"jp","jpn"}, {"jp-jp","jpn"},
            {"ko","kor"}, {"ko-kr","kor"},
            {"ms","may"}, {"nb","nor"},
            {"nb-no","nor"}, {"nl","dut"},
            {"nl-BE","dut"}, {"nl-be","dut"},
            {"nl-nl","dut"}, {"nob","nor"},
            {"pl","pol"}, {"pl-pl","pol"},
            {"pt","por"}, {"pt-BR","por"},
            {"pt-br","por"}, {"ro","rum"},
            {"ru","rus"}, {"ru-ru","rus"},
            {"sk","slo"}, {"sv","swe"},
            {"sv-se","swe"}, {"ta-in","tam"},
            {"te-in","tel"}, {"th","tha"},
            {"tl","tgl"}, {"tr","tur"},
            {"tr-tr","tur"}, {"uk","ukr"},
            {"vi","vie"}, {"zh-hans","chi"},
            {"zh-Hans","chi"}, {"zh-Hant","chi"},
            {"zh-hant","chi"}, {"zh-CN","chi"},
            {"zh-TW","chi"}, {"zh-HK","chi"},
            {"zh-MO","chi"}, {"zh-CHS","chi"},
            {"zh-CHT","chi"}, {"zh-SG","chi"},
            {"et", "est"}, {"bn", "ben"},
            {"iw", "heb"}, {"sr", "srp"},
            {"hy", "arm"}, {"az", "aze"},
            {"kk", "kaz"}, {"is", "ice"},
            {"fil", "phi"}, {"ku", "kur"},
            {"ca", "cat"}, {"no", "nor"},
            {"hu", "hun"},{"chs-cht", "chi"},
            {"cht-chs", "chi"}
        };

        public static Dictionary<string, string> SubTitleDic = new Dictionary<string, string>
        {
            {"ar", "العربية"}, {"ar-eg", "العربية"},
            {"bg", "български"}, {"cmn-hans", "簡體（國語）"},
            {"cmn-hant", "繁體（國語）"}, {"cs", "čeština"},
            {"da", "Dansk"}, {"da-dk", "Dansk"},
            {"de", "Deutsch"}, {"de-de", "Deutsch"},
            {"el", "Ελληνικά"}, {"en", "English"},
            {"en-US", "English"}, {"es", "Español (Latinoamérica)"},
            {"es-419", "Español (Latinoamérica)"}, {"es-es", "Español (España)"},
            {"es-ES", "Español (España)"}, {"fi", "Suomi"},
            {"fi-fi", "Suomi"}, {"fr", "Français"},
            {"fr-fr", "Français"}, {"he", "עברית"},
            {"he-il", "עברית"}, {"hi", "हिन्दी"},
            {"hi-in", "हिन्दी"}, {"hr", "Hrvatska"},
            {"id", "Indonesia"}, {"id-id", "Indonesia"},
            {"it", "Italiano"}, {"it-it", "Italiano"},
            {"ja", "日本語"}, {"ja-ja", "日本語"},
            {"jp", "日本語"}, {"jp-jp", "日本語"},
            {"ko", "한국어"}, {"ko-kr", "한국어"},
            {"ms", "Melayu"}, {"nb", "Norsk Bokmål"},
            {"nb-no", "Norsk Bokmål"}, {"nl", "Nederlands"},
            {"nl-BE", "Nederlands"}, {"nl-be", "Nederlands"},
            {"nl-nl", "Nederlands"}, {"nob", "norsk"},
            {"pl", "Polski"}, {"pl-pl", "Polski"},
            {"pt", "Português"}, {"pt-BR", "Português"},
            {"pt-br", "Português"}, {"ro", "Română"},
            {"ru", "Русский"}, {"ru-ru", "Русский"},
            {"sk", "slovenský"}, {"sv", "Svenska"},
            {"sv-se", "Svenska"}, {"ta-in", "தமிழ்"},
            {"te-in", "తెలుగు"}, {"th", "ไทย"},
            {"tl", "Tagalog"}, {"tr", "Türkçe"},
            {"tr-tr", "Türkçe"}, {"uk", "Українська"},
            {"vi", "Tiếng Việt"}, {"zxx", "zxx"},
            {"zh-hans", "中文（簡體）"},
            {"zh-Hans", "中文（簡體）"},
            {"zh-CN", "中文（簡體）"},
            {"zh-TW", "中文（繁體）"},
            {"zh-HK", "中文（繁體）"},
            {"zh-MO", "中文（繁體）"},
            {"zh-Hant", "中文（繁體）"},
            {"zh-hant", "中文（繁體）"},
            {"yue", "中文（粵語）"},
            {"hu", "Magyar"},
            {"et", "Eestlane"}, {"bn", "বাংলা ভাষার"},
            {"iw", "שפה עברית"}, {"sr", "српски језик"},
            {"hy", "հայերեն"}, {"az", "Azərbaycan"},
            {"kk", "Қазақ тілі"}, {"is", "icelandic"},
            {"fil", "Pilipino"}, {"ku", "Kurdî"},
            {"ca", "català"}, {"no", "norsk språk"},
            {"chs-cht", "中文（簡轉繁）"}, {"cht-chs", "中文（繁轉簡）"}
        };
    }
}
