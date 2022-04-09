using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using static BBDown.BBDownSubUtil;
using static BBDown.BBDownLogger;
using System.IO;
using Fanhuaji_API;

namespace BBDown
{
    class BBDownMuxer
    {
        public static int RunExe(string app, string parms)
        {
            int code = 0;
            Process p = new Process();
            p.StartInfo.FileName = app;
            p.StartInfo.Arguments = parms;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = false;
            p.ErrorDataReceived += delegate (object sendProcess, DataReceivedEventArgs output) {
                if (!string.IsNullOrWhiteSpace(output.Data))
                    Log(output.Data);
            };
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.Start();
            p.BeginErrorReadLine();
            p.WaitForExit();
            p.Close();
            p.Dispose();
            return code;
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\"", "'");
        }

        public static int MuxByMp4box(string videoPath, string audioPath, string outPath, string desc, string title, string episodeId, string pic, string lang, List<Subtitle> subs, bool audioOnly, bool videoOnly)
        {
            StringBuilder inputArg = new StringBuilder();
            StringBuilder metaArg = new StringBuilder();
            inputArg.Append(" -inter 0 -noprog ");
            if (!string.IsNullOrEmpty(videoPath))
                inputArg.Append($" -add \"{videoPath}#trackID=1:name=\" ");
            if (!string.IsNullOrEmpty(audioPath))
                inputArg.Append($" -add \"{audioPath}:lang={(lang == "" ? "und" : lang)}\" ");
            
            if (!string.IsNullOrEmpty(pic))
                metaArg.Append($":cover=\"{pic}\"");
            if (!string.IsNullOrEmpty(episodeId))
                metaArg.Append($":album=\"{title}\":name=\"{episodeId}\"");
            else
                metaArg.Append($":name=\"{title}\"");
            metaArg.Append($":comment=\"{desc}\"");

            if (subs != null)
            {
                for (int i = 0; i < subs.Count; i++)
                {
                    if (File.Exists(subs[i].path) && File.ReadAllText(subs[i].path) != "")
                    {
                        inputArg.Append($" -add \"{subs[i].path}#trackID=1:name={SubDescDic[subs[i].lan]}:lang={SubLangDic[subs[i].lan]}\" ");
                    }
                }
            }

            //----分析完畢
            var arguments = inputArg.ToString() + (metaArg.ToString() == "" ? "" : " -itags tools=\"\"" + metaArg.ToString()) + $" \"{outPath}\"";
            LogDebug("mp4box命令：{0}", arguments);
            return RunExe("mp4box", arguments);
        }

        public static async Task<int> MuxAV(bool useMp4box, string videoPath, string audioPath, string outPath, string desc = "", string title = "", string episodeId = "", string pic = "", string lang = "", List<Subtitle> subs = null, bool audioOnly = false, bool videoOnly = false, string aid = "", string cid = "")
        {
            //Fanhuaji-API
            var Fanhuaji = new Fanhuaji(Agree: true, Terms_of_Service: Fanhuaji_API.Fanhuaji.Terms_of_Service);
            desc = EscapeString(desc);
            title = EscapeString(title);
            episodeId = EscapeString(episodeId);

            if (useMp4box)
            {
                return MuxByMp4box(videoPath, audioPath, outPath, desc, title, episodeId, pic, lang, subs, audioOnly, videoOnly);
            }

            if (outPath.Contains("/") && ! Directory.Exists(Path.GetDirectoryName(outPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            //----分析並生成-i參數
            StringBuilder inputArg = new StringBuilder();
            StringBuilder metaArg = new StringBuilder();
            if (!string.IsNullOrEmpty(videoPath))
                inputArg.Append($" -i \"{videoPath}\" ");
            if (!string.IsNullOrEmpty(audioPath))
                inputArg.Append($" -i \"{audioPath}\" ");
            if (!string.IsNullOrEmpty(pic))
                inputArg.Append($" -i \"{pic}\" ");
            string[] files = System.IO.Directory.GetFiles(Directory.GetCurrentDirectory(), $"temp/{aid}/{aid}.{cid}.*.srt");
            if (subs != null)
            {
                for (int i = 0; i < subs.Count; i++)
                {
                    if(File.Exists(subs[i].path) && File.ReadAllText(subs[i].path) != "")
                    {
                        inputArg.Append($" -i \"{subs[i].path}\" ");
                        if (SubTitleDic[subs[i].lan] == "中文（簡轉繁）" || SubTitleDic[subs[i].lan] == "中文（繁體）")
                        {
                            metaArg.Append($" -metadata:s:s:{i} handler_name=\"{SubDescDic[subs[i].lan]}\" -metadata:s:s:{i} language={SubLangDic[subs[i].lan]} -metadata:s:s:{i} title=\"{SubTitleDic[subs[i].lan]}\" -disposition:s:s:{i} +default+forced ");
                        }
                        else
                        {
                            metaArg.Append($" -metadata:s:s:{i} handler_name=\"{SubDescDic[subs[i].lan]}\" -metadata:s:s:{i} language={SubLangDic[subs[i].lan]} -metadata:s:s:{i} title=\"{SubTitleDic[subs[i].lan]}\" ");
                        }
                        
                    }
                }
                if (files.Length > 0 && subs.Count < 1)
                {
                    Log("正在合併現有字幕...");
                    for (int e = 0; e < files.Length; e++)
                    {
                        inputArg.Append($" -i \"{files[e]}\" ");
                    }

                }
            }
            if (!string.IsNullOrEmpty(pic))
                metaArg.Append(" -disposition:v:1 attached_pic ");
            var inputCount = Regex.Matches(inputArg.ToString(), "-i \"").Count;
            for (int i = 0; i < inputCount; i++)
            {
                inputArg.Append($" -map {i} ");
            }
            var titletcovObj = await Fanhuaji.ConvertAsync(title, Fanhuaji_API.Enum.Enum_Converter.Traditional, new Config() { });
            var titletcov = titletcovObj.Data.Text;
            int isAreaTitle = titletcov.IndexOf("（");
            if (isAreaTitle != -1) {
                titletcov = titletcov.Remove(isAreaTitle);
            }
            var desccovObj = await Fanhuaji.ConvertAsync(desc, Fanhuaji_API.Enum.Enum_Converter.Traditional, new Config() { });
            string desccov = desccovObj.Data.Text;
            //----分析完畢
            var arguments = $"-loglevel warning -y " +
                 inputArg.ToString() + metaArg.ToString() + $" -metadata title=\"" + titletcov + "\" " +
                 (lang == "" ? "-metadata:s:a:0 language=jpn " : $"-metadata:s:a:0 language={lang} ") +
                 $"-metadata description=\"{desccov}\" " +
                 $"-metadata album=\"{titletcov}\" " +
                 (audioOnly ? " -vn " : "") + (videoOnly ? " -an " : "") +
                 $"-c copy " +
                 (subs != null ? " -c:s mov_text " : "") +
                 $"\"{outPath}\"";
            LogDebug("ffmpeg命令：{0}", arguments);
            return RunExe("ffmpeg", arguments);
        }

        public static void MergeFLV(string[] files, string outPath)
        {
            if (files.Length == 1)
            {
                File.Move(files[0], outPath); 
            }
            else
            {
                foreach (var file in files)
                {
                    var tmpFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".ts");
                    var arguments = $"-loglevel warning -y -i \"{file}\" -map 0 -c copy -f mpegts -bsf:v h264_mp4toannexb \"{tmpFile}\"";
                    LogDebug("ffmpeg命令：{0}", arguments);
                    RunExe("ffmpeg", arguments);
                    File.Delete(file);
                }
                var f = GetFiles(Path.GetDirectoryName(files[0]), ".ts");
                CombineMultipleFilesIntoSingleFile(f, outPath);
                foreach (var s in f) File.Delete(s);
            }
        }
    }
}
