using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using static BBDown.BBDownSubUtil;
using static BBDown.BBDownLogger;
using System.IO;
using OpenCC.NET;

namespace BBDown
{
    class BBDownMuxer
    {
        public static int ffmpeg(string parms)
        {
            int code = 0;
            Process p = new Process();
            p.StartInfo.FileName = "ffmpeg";
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

        public static int MuxAV(string videoPath, string audioPath, string outPath, string desc = "", string title = "", string episodeId = "", string pic = "", string lang = "", List<Subtitle> subs = null, bool audioOnly = false, bool videoOnly = false, string aid = "", string cid = "")
        {
            desc = EscapeString(desc);
            title = EscapeString(title);

            if (outPath.Contains("/") && ! Directory.Exists(Path.GetDirectoryName(outPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            //----分析并生成-i参数
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
                    if (File.Exists(subs[i].path) && File.ReadAllText(subs[i].path) != "")
                    {
                        inputArg.Append($" -i \"{subs[i].path}\" ");
                        metaArg.Append($" -metadata:s:s:{i} handler_name=\"{SubDescDic[subs[i].lan]}\" -metadata:s:s:{i} language={SubLangDic[subs[i].lan]} -metadata:s:s:{i} title=\"{SubTitleDic[subs[i].lan]}\"");
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
            var converter = new OpenChineseConverter();
            string titletcov = converter.ToTaiwanFromSimplifiedWithPhrases(title);
            titletcov = titletcov.Remove(titletcov.IndexOf("（"));
            string desccov = converter.ToTaiwanFromSimplifiedWithPhrases(desc);
            //----分析完毕
            var arguments = $"-loglevel warning -y " +
                 inputArg.ToString() + metaArg.ToString() + $" -metadata title=\"" + titletcov + "\" " +
                 (lang == "" ? "" : $"-metadata:s:a:0 language={lang} ") +
                 $"-metadata description=\"{desccov}\" " +
                 $"-metadata album=\"{titletcov}\" " +
                 (audioOnly ? " -vn " : "") + (videoOnly ? " -an " : "") +
                 $"-c copy " +
                 (subs != null ? " -c:s mov_text " : "") +
                 $"\"{outPath}\"";
            LogDebug("ffmpeg命令：{0}", arguments);
            return ffmpeg(arguments);
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
                    ffmpeg(arguments);
                    File.Delete(file);
                }
                var f = GetFiles(Path.GetDirectoryName(files[0]), ".ts");
                CombineMultipleFilesIntoSingleFile(f, outPath);
                foreach (var s in f) File.Delete(s);
            }
        }
    }
}
