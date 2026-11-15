using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MopsDownloader
{
    /// <summary>
    /// doc.twse.com.tw 電子資料查詢作業 (t57sb01) 用戶端
    /// </summary>
    public class TwseDocClient : IDisposable
    {
        private const string BaseUrl = "https://doc.twse.com.tw";
        private const string QueryPath = "/server-java/t57sb01";
        private const int MaxRetry = 3;

        private const string ThrottleMessage = "查詢過量";
        private const int MaxThrottleRetry = 5;
        private const int ThrottleWaitSeconds = 15;

        // 網站使用 Big5 編碼
        private static readonly Encoding Big5 = Encoding.GetEncoding(950);

        // 電子檔案：<a href='/pdf/202601_8436_AI1_20261115_121030.pdf'>202601_8436_AI1.pdf</a>
        private static readonly Regex PdfLinkRegex = new Regex(
            @"href\s*=\s*['""](/pdf/[^'""]+)['""]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>民國年 = 西元年 - 1911</summary>
        public const int RocYearOffset = 1911;

        private readonly HttpClient _http;

        /// <summary>每次請求之間的間隔，避免請求過於頻繁被網站阻擋</summary>
        public TimeSpan RequestInterval { get; set; } = TimeSpan.FromSeconds(2);

        private DateTime _lastRequestTime = DateTime.MinValue;

        public TwseDocClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        }

        /// <summary>
        /// 西元年轉民國年 (網站查詢參數與資料年度欄位皆為民國年)
        /// </summary>
        public static int ToRocYear(int year)
        {
            return year - RocYearOffset;
        }

        /// <summary>
        /// 查詢檔案清單
        /// </summary>
        /// <param name="coId">股票代號</param>
        /// <param name="urlYear">網址的 year 參數 (民國年)</param>
        /// <param name="mtype">A=財務報告書, F=股東會相關資料</param>
        public async Task<List<DocEntry>> GetListAsync(string coId, int urlYear, string mtype)
        {
            var url = string.Format(
                "{0}{1}?step=1&colorchg=1&co_id={2}&year={3}&seamon=&mtype={4}&",
                BaseUrl, QueryPath, Uri.EscapeDataString(coId), urlYear, Uri.EscapeDataString(mtype));

            var html = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url));
            return DocListParser.Parse(html);
        }

        /// <summary>
        /// 下載檔案並存成指定路徑
        /// </summary>
        public async Task DownloadAsync(DocEntry entry, string destPath)
        {
            // 第一步：送出 step=9，取得伺服器暫存的實際檔案路徑
            var form = new Dictionary<string, string>
            {
                { "colorchg", "1" },
                { "step", "9" },
                { "kind", entry.Kind },
                { "co_id", entry.CoId },
                { "filename", entry.FileName },
            };

            string pdfPath = null;
            for (var attempt = 1; ; attempt++)
            {
                var html = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Post, BaseUrl + QueryPath)
                {
                    Content = new FormUrlEncodedContent(form),
                });

                var m = PdfLinkRegex.Match(html);
                if (m.Success)
                {
                    pdfPath = m.Groups[1].Value;
                    break;
                }

                if (attempt >= MaxRetry)
                {
                    throw new InvalidOperationException(
                        "無法取得檔案下載連結：" + entry.FileName + Environment.NewLine + StripTags(html));
                }

                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }

            // 第二步：下載實際檔案
            var dir = Path.GetDirectoryName(Path.GetFullPath(destPath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = destPath + ".part";
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await WaitIntervalAsync();
                    using (var resp = await _http.GetAsync(BaseUrl + pdfPath, HttpCompletionOption.ResponseHeadersRead))
                    {
                        resp.EnsureSuccessStatusCode();
                        using (var src = await resp.Content.ReadAsStreamAsync())
                        using (var dst = File.Create(tempPath))
                        {
                            await src.CopyToAsync(dst);
                        }
                    }

                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Move(tempPath, destPath);
                    return;
                }
                catch (Exception) when (attempt < MaxRetry)
                {
                    TryDelete(tempPath);
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
                }
                catch
                {
                    TryDelete(tempPath);
                    throw;
                }
            }
        }

        private async Task<string> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory)
        {
            var throttled = 0;
            for (var attempt = 1; ; attempt++)
            {
                string html;
                try
                {
                    await WaitIntervalAsync();
                    using (var req = requestFactory())
                    using (var resp = await _http.SendAsync(req))
                    {
                        resp.EnsureSuccessStatusCode();
                        var bytes = await resp.Content.ReadAsByteArrayAsync();
                        html = Big5.GetString(bytes);
                    }
                }
                catch (Exception) when (attempt < MaxRetry)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
                    continue;
                }

                // 網站限流時回傳「查詢過量，請稍後再查詢!」，需等待後重試
                if (html.Contains(ThrottleMessage))
                {
                    throttled++;
                    if (throttled > MaxThrottleRetry)
                    {
                        throw new InvalidOperationException("網站回應查詢過量，已重試 " + MaxThrottleRetry + " 次仍失敗");
                    }

                    var wait = TimeSpan.FromSeconds(ThrottleWaitSeconds * throttled);
                    Console.Error.WriteLine("  (網站回應查詢過量，{0} 秒後重試 {1}/{2})",
                        wait.TotalSeconds, throttled, MaxThrottleRetry);
                    await Task.Delay(wait);
                    attempt = 0;
                    continue;
                }

                return html;
            }
        }

        private async Task WaitIntervalAsync()
        {
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            if (elapsed < RequestInterval)
            {
                await Task.Delay(RequestInterval - elapsed);
            }
            _lastRequestTime = DateTime.UtcNow;
        }

        private static string StripTags(string html)
        {
            var text = Regex.Replace(html ?? string.Empty, @"<script.*?</script>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", " ");
            return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 忽略暫存檔刪除失敗
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
