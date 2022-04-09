using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static BBDown.BBDownLogger;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownSpaceVideoFetcher : IFetcher
    {
        public async Task<BBDownVInfo> FetchAsync(string id)
        {
            id = id.Substring(4);
            string userInfoApi = $"https://api.bilibili.com/x/space/acc/info?mid={id}&jsonp=jsonp";
            string userName = GetValidFileName(JsonDocument.Parse(await GetWebSourceAsync(userInfoApi)).RootElement.GetProperty("data").GetProperty("name").ToString());
            List<string> urls = new List<string>();
            int pageSize = 100;
            int pageNumber = 1;
            string api = $"https://api.bilibili.com/x/space/arc/search?mid={id}&ps={pageSize}&tid=0&pn={pageNumber}&keyword=&order=pubdate&jsonp=jsonp";
            string json = await GetWebSourceAsync(api);
            var infoJson = JsonDocument.Parse(json);
            var pages = infoJson.RootElement.GetProperty("data").GetProperty("list").GetProperty("vlist").EnumerateArray();
            foreach (var page in pages)
            {
                urls.Add($"https://www.bilibili.com/video/av{page.GetProperty("aid")}");
            }
            int totalCount = infoJson.RootElement.GetProperty("data").GetProperty("page").GetProperty("count").GetInt32();
            int totalPage = (int)Math.Ceiling((double)totalCount / pageSize);
            while (pageNumber < totalPage)
            {
                pageNumber++;
                urls.AddRange(await GetVideosByPageAsync(pageNumber, pageSize, id));
            }
            File.WriteAllText($"{userName}的投稿影片.txt", string.Join('\n', urls));
            Log("目前下載器不支持下載用戶的全部投稿影片，不過程序已經獲取到了該用戶的全部投稿影片網址，你可以自行使用批處理腳本等手段調用本程式進行批次下載。如在Windows系統你可以使用如下代碼：");
            Console.WriteLine();
            Console.WriteLine(@"@echo Off
For / F %%a in (urls.txt) Do (BBDown.exe ""%%a"")
pause");
            Console.WriteLine();
            throw new Exception("暫不支持該功能");
        }

        async Task<List<string>> GetVideosByPageAsync(int pageNumber, int pageSize, string mid)
        {
            List<string> urls = new List<string>();
            string api = $"https://api.bilibili.com/x/space/arc/search?mid={mid}&ps={pageSize}&tid=0&pn={pageNumber}&keyword=&order=pubdate&jsonp=jsonp";
            string json = await GetWebSourceAsync(api);
            var infoJson = JsonDocument.Parse(json);
            var pages = infoJson.RootElement.GetProperty("data").GetProperty("list").GetProperty("vlist").EnumerateArray();
            foreach (var page in pages)
            {
                urls.Add($"https://www.bilibili.com/video/av{page.GetProperty("aid")}");
            }
            return urls;
        }
    }
}
