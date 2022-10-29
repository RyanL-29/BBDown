using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownBangumiInfoFetcher : IFetcher
    {
        public async Task<BBDownVInfo> FetchAsync(string id)
        {
            id = id.Substring(3);
            string index = "";
            string api = $"https://api.bilibili.com/pgc/view/web/season?ep_id={id}";
            string json = await GetWebSourceAsync(api);
            using var infoJson = JsonDocument.Parse(json);
            var result = infoJson.RootElement.GetProperty("result");
            string cover = result.GetProperty("cover").ToString();
            string title = result.GetProperty("title").ToString();
            string desc = result.GetProperty("evaluate").ToString();
            string pubTime = result.GetProperty("publish").GetProperty("pub_time").ToString();
            var pages = result.GetProperty("episodes").EnumerateArray().ToList();
            List<Page> pagesInfo = new List<Page>();
            int i = 1;

            //episodes為空; 或者未包含對應epid，番外/花絮什麼的
            if (pages.Count == 0 || !result.GetProperty("episodes").ToString().Contains($"/ep{id}")) 
            {
                JsonElement sections;
                if (result.TryGetProperty("section", out sections))
                {
                    foreach (var section in sections.EnumerateArray())
                    {
                        if (section.ToString().Contains($"/ep{id}"))
                        {
                            title += "[" + section.GetProperty("title").GetSingle() + "]";
                            pages = section.GetProperty("episodes").EnumerateArray().ToList();
                            break;
                        }
                    }
                }
            }

            foreach (var page in pages)
            {
                //跳過預告
                JsonElement badge;
                if (page.TryGetProperty("badge", out badge) && (badge.ToString() == "預告" || badge.ToString() == "预告")) continue;
                string res = "";
                try
                {
                    res = page.GetProperty("dimension").GetProperty("width").ToString() + "x" + page.GetProperty("dimension").GetProperty("height").ToString();
                }
                catch (Exception) { }
                string _title = page.GetProperty("title").ToString() + " " + page.GetProperty("long_title").ToString();
                _title = _title.Trim();
                Page p = new Page(i++,
                    page.GetProperty("aid").ToString(),
                    page.GetProperty("cid").ToString(),
                    page.GetProperty("id").ToString(),
                    _title,
                    0, 
                    res,
                    Int32.Parse(page.GetProperty("title").ToString()));
                if (p.epid == id) index = p.index.ToString();
                pagesInfo.Add(p);
            }


            var info = new BBDownVInfo();
            info.Title = title.Trim();
            info.Desc = desc.Trim();
            info.Pic = cover;
            info.PubTime = pubTime;
            info.PagesInfo = pagesInfo;
            info.IsBangumi = true;
            info.IsCheese = true;
            info.Index = index;
            return info;
        }
    }
}
