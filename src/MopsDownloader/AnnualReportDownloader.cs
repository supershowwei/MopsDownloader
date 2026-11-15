using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MopsDownloader
{
    /// <summary>
    /// 下載股東會年報 (mtype=F)
    /// 優先下載中文版股東會後修訂本 (F11)，若無則下載中文版尚未適用永續揭露準則 (F04)
    /// </summary>
    public class AnnualReportDownloader
    {
        // 依優先順序排列的檔名代碼
        private static readonly string[] PreferredCodes = { "F11", "F04" };

        private static readonly Regex YearRegex = new Regex(@"^\s*(\d+)\s*年", RegexOptions.Compiled);

        private readonly TwseDocClient _client;

        public AnnualReportDownloader(TwseDocClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 下載指定股票、年度的股東會年報
        /// </summary>
        /// <param name="coId">股票代號</param>
        /// <param name="year">年報所屬民國年度 (網址會帶入 year + 1 查詢)</param>
        /// <param name="outputDir">輸出資料夾</param>
        /// <param name="overwrite">檔案已存在時是否覆寫</param>
        /// <returns>是否成功下載 (或已存在)</returns>
        public async Task<bool> DownloadAsync(string coId, int year, string outputDir, bool overwrite)
        {
            Console.WriteLine("[年報] {0} {1} 年 查詢中...", coId, year);

            // 網址帶入的年度為股東會召開年度，其中的年報屬於前一年度，
            // 因此要取得 year 年度的年報需查詢 year + 1
            var entries = await _client.GetListAsync(coId, year + 1, "F");

            var candidates = entries
                .Where(e => e.CoId == coId)
                .Where(e => GetYear(e.DataYear) == year)
                .Where(e => !e.Description.Contains("英文"))
                .ToList();

            foreach (var code in PreferredCodes)
            {
                var entry = candidates
                    .Where(e => Path.GetFileNameWithoutExtension(e.FileName)
                        .EndsWith(code, StringComparison.OrdinalIgnoreCase))
                    // 若有多筆 (例如臨時會)，取最後上傳者
                    .OrderBy(e => e.UploadDate, StringComparer.Ordinal)
                    .LastOrDefault();

                if (entry == null)
                {
                    continue;
                }

                var ext = Path.GetExtension(entry.FileName);
                var destName = string.Format("{0}_{1}{2}{3}", coId, year, code, ext);
                var destPath = Path.Combine(outputDir, destName);

                if (File.Exists(destPath) && !overwrite)
                {
                    Console.WriteLine("  略過 (已存在) {0}", destName);
                    return true;
                }

                try
                {
                    Console.Write("  下載 {0} -> {1} ... ", entry.FileName, destName);
                    await _client.DownloadAsync(entry, destPath);
                    Console.WriteLine("完成");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("失敗");
                    Console.Error.WriteLine("  " + ex.Message);
                    return false;
                }
            }

            Console.WriteLine("[年報] {0} {1} 年 查無中文版股東會年報 (F11/F04)", coId, year);
            return false;
        }

        /// <summary>
        /// 由「114 年」取得年度，無法判斷時回傳 0
        /// </summary>
        private static int GetYear(string dataYear)
        {
            var m = YearRegex.Match(dataYear ?? string.Empty);
            int y;
            return m.Success && int.TryParse(m.Groups[1].Value, out y) ? y : 0;
        }
    }
}
