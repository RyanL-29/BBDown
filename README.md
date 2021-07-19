[![img](https://img.shields.io/github/stars/RyanL-29/BBDown?label=%E7%82%B9%E8%B5%9E)](https://github.com/RyanL-29/BBDown)  [![img](https://img.shields.io/github/last-commit/RyanL-29/BBDown?label=%E6%9C%80%E8%BF%91%E6%8F%90%E4%BA%A4)](https://github.com/RyanL-29/BBDown)  [![img](https://img.shields.io/github/release/RyanL-29/BBDown?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC)](https://github.com/RyanL-29/BBDown/releases)  [![img](https://img.shields.io/github/license/RyanL-29/BBDown?label=%E8%AE%B8%E5%8F%AF%E8%AF%81)](https://github.com/RyanL-29/BBDown)

# BBDown Server Edition (此版本並不能單獨使用, 需配合 [BBDownAutoExtension](https://github.com/RyanL-29/BBDownAutoExtension))
BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟體. 24/7 version (fork from BBDown)

此版本適用於伺服器及Windwos Server 2019 NAS上面使用, 可以全自動運行
測試環境為 Windows Server 2019

# 下載
https://github.com/nilaoda/BBDown/releases

# 開始使用
目前命令行參數支持情況
```
BBDown:
  BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟件.

Usage:
  BBDown [options] <url> [command]

Arguments:
  <url> 視頻地址 或 av|bv|BV|ep|ss

Options:
  -tv, --use-tv-api 使用TV端解析模式
  -app, --use-app-api 使用APP端解析模式
  -intl, --use-intl-api 使用國際版解析模式
  --use-mp4box 使用MP4Box來混流
  -hevc, --only-hevc 只下載hevc編碼
  -avc, --only-avc 只下載avc編碼
  -info, --only-show-info 僅解析而不進行下載
  -hs, --hide-streams 不要顯示所有可用音視頻流
  -ia, --interactive 交互式選擇清晰度
  --show-all 展示所有分P標題
  --use-aria2c 調用aria2c進行下載(你需要自行準備好二進制可執行文件)
  --aria2c-proxy <aria2c-proxy> 調用aria2c進行下載時的代理地址配置
  -mt, --multi-thread 使用多線程下載
  -p, --select-page <select-page> 選擇指定分p或分p範圍：(-p 8 或 -p 1,2 或 -p 3-5 或 -p ALL)
  --audio-only 僅下載音頻
  --video-only 僅下載視頻
  --sub-only 僅下載字幕
  --no-padding-page-num 不給分P序號補零
  --debug 輸出調試日誌
  --skip-mux 跳過混流步驟
  --language <language> 設置混流的音頻語言(代碼)，如chi, jpn等
  -token, --access-token <access-token> 設置access_token用以下載TV/APP接口的會員內容
  --version Show version information
  -?, -h, --help Show help and usage information

Commands:
  login 通過APP掃描二維碼以登錄您的WEB賬號
  logintv 通過APP掃描二維碼以登錄您的TV賬號
```

# 功能
- [x] 番劇下載(Web|TV)
- [x] 課程下載(Web)
- [x] 普通內容下載(Web|TV) `(TV介面可以下載部分UP主的無浮水印內容)`
- [x] 多分P自動下載
- [x] 選擇指定分P進行下載
- [x] 選擇指定清晰度進行下載
- [x] 下載外掛字幕並轉換為srt格式
- [x] 自動合併音訊+影片軌+字幕流
- [x] 二維碼登錄帳號
- [x] **多執行緒下載**
- [x] 支持調用aria2c下載
- [x] 支持至高4K HDR清晰度下載
- [x] 可自行設定檢查更新間隔時間
- [x] 可自行設定影片下載目錄
- [x] 可自行設定下載檔名前綴後綴
- [x] 自動增加簡轉繁字幕 (編譯需要自行下載 [OpenCC](https://github.com/RyanL-29/OpenCC-NET))
- [x] 解析度將會自動加上至檔名
- [x] 全自動執行(暫時透過 batch script 來實現)
- [x] 可以批次下載
- [x] 實現整段命令行參數在Config.json設置 (請參考 [BBDownAutoExtension](https://github.com/RyanL-29/BBDownAutoExtension))
- [x] 真正全自動執行 (請參考 [BBDownAutoExtension](https://github.com/RyanL-29/BBDownAutoExtension))

# 改善
- [x] 除了影片分片外快取資料夾不會被剷除
- [x] 解決字幕重複下載問題
- [x] 解決快取資料夾被剷除後再次下載問題
- [x] 所有快取資料夾都會被放在 temp 裡面

# TODO
- [ ] 支持更多自訂選項
- [ ] 自動刷新cookie
- [ ] 跟著主線更新

# 已知問題
- [ ] 部分新番下載時會出現下載錯誤 (不會影響自動程序)
- [x] 部分新番無法下載字幕 (請重新獲取 cookie)

# 使用範例

掃碼登錄網頁帳號：
```
BBDown login
```
掃碼登錄雲視聽小電視帳號：
```
BBDown logintv
```
 
*PS: 如果登錄報錯`The type initializer for 'Gdip' threw an exception`，請參考 [#37](https://github.com/nilaoda/BBDown/issues/37) 解決*

手動載入雲視聽小電視token：
```
BBDown -a "access_token=******" "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
下載普通影片：
```
BBDown "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
使用TV介面下載(粉絲量大的UP主基本上是無浮水印片源)：
```
BBDown -tv "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
當分P過多時，預設會隱藏展示全部的分P訊息，你可以使用如下命令來顯示所有每一個分P。
```
BBDown --show-all "https://www.bilibili.com/video/BV1At41167aj"
```
選擇下載某些分P的三種情況：
* 單個分P：10
```
BBDown "https://www.bilibili.com/video/BV1At41167aj?p=10"
```
```
BBDown -p 10 "https://www.bilibili.com/video/BV1At41167aj"
```
* 多個分P：1,2,10
```
BBDown -p 1,2,10 "https://www.bilibili.com/video/BV1At41167aj"
```
* 範圍分P：1-10
```
BBDown -p 1-10 "https://www.bilibili.com/video/BV1At41167aj"
```
下載番劇全集：
```
BBDown -p ALL "https://www.bilibili.com/bangumi/play/ss33073"
```
下載番劇最新一集：
```
BBDown -p LATEST "https://www.bilibili.com/bangumi/play/ss33073"
```

# 使用流程

 Step1: 請下載 [BBDownCore](https://github.com/RyanL-29/BBDown/releases)
 
 Step2: 請下載 [BBDownEX](https://github.com/RyanL-29/BBDownAutoExtension/releases)
 
 Step3: 下載下來後把所有東西放在同一個資料夾
 
 Step4: 你資料夾內的東西應該和以下圖片一樣
 
![image](https://user-images.githubusercontent.com/48479346/126116001-b11e190f-5eb2-4bdf-8df4-1a40d97bb3b7.png)

 Step5: 設置config.json
 
 Step6: 使用take_cookie.bat, 取得SESSDATA, 取得的內容將會自動寫到cookie.txt
 
 Step7: 設定list.txt, 把你想下載的影片網址全部放進去, 請依照上面的教學設置
 
 Step8: 按BBDownAutoCore.exe, 啟動自動程式

