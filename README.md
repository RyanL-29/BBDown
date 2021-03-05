[![img](https://img.shields.io/github/stars/RyanL-29/BBDown?label=%E7%82%B9%E8%B5%9E)](https://github.com/RyanL-29/BBDown)  [![img](https://img.shields.io/github/last-commit/RyanL-29/BBDown?label=%E6%9C%80%E8%BF%91%E6%8F%90%E4%BA%A4)](https://github.com/RyanL-29/BBDown)  [![img](https://img.shields.io/github/release/RyanL-29/BBDown?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC)](https://github.com/RyanL-29/BBDown/releases)  [![img](https://img.shields.io/github/license/RyanL-29/BBDown?label=%E8%AE%B8%E5%8F%AF%E8%AF%81)](https://github.com/RyanL-29/BBDown)

# BBDown Server Edition
BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟件. 24/7 version

此版本適用於伺服器及NAS上面使用, 可以全自動運行

# 下载
https://github.com/nilaoda/BBDown/releases

# 开始使用
目前命令行参数支持情况
```
BBDown:
  BBDown是一個免費且便捷高效的嗶哩嗶哩下載/解析軟件.

Usage:
  BBDown [options] <url> [command]

Arguments:
  <url>    影片地址 或 av|bv|BV|ep|ss

Options:
  -tv, --use-tv-api                    使用TV端解析模式
  -intl, --use-intl-api                使用國際版解析模式
  -hevc, --only-hevc                   下載hevc編碼
  -info, --only-show-info              只解析不下載
  -hs, --hide-streams                  不要顯示所有可用音視頻流
  -ia, --interactive                   交互式選擇清晰度
  --show-all                           展示所有分P資訊
  --use-aria2c                        使用aria2c下載(你需要自行準備二進制可執行文件)
  -mt, --multi-thread                  使用多線程下載
  -p, --select-page <select-page>      選擇指定分p或分p範圍
  --audio-only                         只下載音訊
  --video-only                          只下載視訊
  --debug                              輸出調試日誌
  --skip-mux                           跳過混流步驟
  --language <language>                設置混流的音頻語言(代碼)，如chi, jpn等
  -a, --access-token <access-token>    設置access_token用以下載TV接口的會員內容
  --version                            Show version information
  -?, -h, --help                       Show help and usage information

Commands:
  login      通過APP掃描二維碼以登錄您的WEB賬號
  logintv    通過APP掃描二維碼以登錄您的TV賬號
```

# 功能
- [x] 番剧下载(Web|TV)
- [x] 课程下载(Web)
- [x] 普通内容下载(Web|TV) `(TV接口可以下载部分UP主的无水印内容)`
- [x] 多分P自动下载
- [x] 选择指定分P进行下载
- [x] 选择指定清晰度进行下载
- [x] 下载外挂字幕并转换为srt格式
- [x] 自动合并音频+视频流+字幕流
- [x] 二维码登录账号
- [x] **多线程下载**
- [x] 支持调用aria2c下载
- [x] 支持至高4K HDR清晰度下载
- [x] 可自行設定檢查更新間隔時間
- [x] 可自行設定影片下載目錄
- [x] 可自行設定下載檔名前綴後綴
- [x] 自動增加簡轉繁字幕 (編譯需要自行下載 OpenCC)
- [x] 解像度將會自動加上至檔名
- [x] 全自動執行(暫時透過 batch script 來實現)
- [x] 可以批量下載

#改善
- [x] 除了影片分片外快取資料夾不會被剷除
- [x] 解決字幕重複下載問題
- [x] 解決快取資料夾被剷除後再次下載問題
- [x] 所有快取資料夾都會被放在 temp 裏面

# TODO
- [ ] 支持更多自定义选项
- [ ] 自动刷新cookie
- [ ] 跟著主線更新
- [ ] 實現整段命令行參數在Config.json設置
- [ ] 真正全自動執行

# 使用示例

扫码登录网页账号：
```
BBDown login
```
扫码登录云视听小电视账号：
```
BBDown logintv
```
 
*PS: 如果登录报错`The type initializer for 'Gdip' threw an exception`，请参考 [#37](https://github.com/nilaoda/BBDown/issues/37) 解决*

手动加载云视听小电视token：
```
BBDown -a "access_token=******" "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
下载普通视频：
```
BBDown "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
使用TV接口下载(粉丝量大的UP主基本上是无水印片源)：
```
BBDown -tv "https://www.bilibili.com/video/BV1qt4y1X7TW"
```
当分P过多时，默认会隐藏展示全部的分P信息，你可以使用如下命令来显示所有每一个分P。
```
BBDown --show-all "https://www.bilibili.com/video/BV1At41167aj"
```
选择下载某些分P的三种情况：
* 单个分P：10
```
BBDown "https://www.bilibili.com/video/BV1At41167aj?p=10"
```
```
BBDown -p 10 "https://www.bilibili.com/video/BV1At41167aj"
```
* 多个分P：1,2,10
```
BBDown -p 1,2,10 "https://www.bilibili.com/video/BV1At41167aj"
```
* 范围分P：1-10
```
BBDown -p 1-10 "https://www.bilibili.com/video/BV1At41167aj"
```
下载番剧全集：
```
BBDown -p ALL "https://www.bilibili.com/bangumi/play/ss33073"
```

------------


## **bilibilidown.bat 是主要執行檔, 設置好再按他就可以執行, 如非必要請勿更改檔名, 更改後不作任何設置100%不能用**

#Config.json 設置

```
{
 "dir": "C:/Users/Ryan/Desktop/test",  #影片檔案存放位置
 "prefix": "[Test]", #檔名前綴
 "suffix": "[Bilibili][test]" #檔名後綴
}
```
#cookie.txt
```
SESSDATA=xxxxxx%xxxxxxxxx%xxxxxxx%xxxx
#BBDown.exe login 命令行取得的cookie
```
#更新間隔時間設置 (bilibilidown.bat)
```
@echo off

:loop

call config.bat
pause
timeout /T 300 /NOBREAK > nul  #300是停止秒數 = 5 分鐘執行一次

goto loop
```
#自動更新及批量下載設置(config.bat)
```
  REM 五等分的新娘∬
  BBDown.exe -mt -p 8 https://www.bilibili.com/bangumi/play/ss37808

  #參數基本上和主線的那個一樣, 只是不需要在這裏設置cookie
```

# 演示
![1](https://raw.githubusercontent.com/RyanL-29/BBDown/master/ScreenShot/2021-03-05%2021-13-40.gif)

下载完毕后在上面你自訂的的目录查看MP4文件：

![2](https://raw.githubusercontent.com/RyanL-29/BBDown/master/ScreenShot/Screenshot%202021-03-05%20211752.png)
