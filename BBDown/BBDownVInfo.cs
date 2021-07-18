using System;
using System.Collections.Generic;
using System.Text;
using static BBDown.BBDownEntity;

namespace BBDown
{
    class BBDownVInfo
    {
        /// <summary>
        /// 影片index 用於番劇或課程判斷當前選擇的是第幾集
        /// </summary>
        private string index;

        /// <summary>
        /// 影片標題
        /// </summary>
        private string title;

        /// <summary>
        /// 影片描述
        /// </summary>
        private string desc;

        /// <summary>
        /// 影片封面
        /// </summary>
        private string pic;

        /// <summary>
        /// 影片發布時間
        /// </summary>
        private string pubTime;

        private bool isBangumi;
        private bool isCheese;

        /// <summary>
        /// 影片分P訊息
        /// </summary>
        private List<Page> pagesInfo;

        public string Title { get => title; set => title = value; }
        public string Desc { get => desc; set => desc = value; }
        public string Pic { get => pic; set => pic = value; }
        public string PubTime { get => pubTime; set => pubTime = value; }
        public bool IsBangumi { get => isBangumi; set => isBangumi = value; }
        public bool IsCheese { get => isCheese; set => isCheese = value; }
        public string Index { get => index; set => index = value; }
        internal List<Page> PagesInfo { get => pagesInfo; set => pagesInfo = value; }
    }
}
