using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MopsDownloader
{
    /// <summary>
    /// 下載財務報告書 (mtype=A)，只下載中文版合併財報
    /// 最新年度下載全部季別，非最新年度只下載第四季
    /// </summary>
    public class FinancialReportDownloader
    {
        private static readonly Dictionary<string, int> QuarterMap = new Dictionary<string, int>
        {
            { "第一季", 1 },
            { "第二季", 2 },
            { "第三季", 3 },
            { "第四季", 4 },
        };

        private readonly TwseDocClient _client;

        public FinancialReportDownloader(TwseDocClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 下載指定股票、年度的各季中文版合併財報
        /// </summary>
        /// <param name="coId">股票代號</param>
        /// <param name="year">民國年度</param>
        /// <param name="outputDir">輸出資料夾</param>
        /// <param name="overwrite">檔案已存在時是否覆寫</param>
        /// <param name="isLatest">是否為最新年度：是則下載全部季別，否則只下載第四季</param>
        /// <returns>成功下載 (或已存在) 的檔案數</returns>
        public async Task<int> DownloadAsync(string coId, int year, string outputDir, bool overwrite, bool isLatest)
        {
            Console.WriteLine("[財報] {0} {1} 年 查詢中...", coId, year);

            var entries = await _client.GetListAsync(coId, year, "A");

            Console.WriteLine(isLatest
                ? "  {0} 年為最新年度，下載全部季別"
                : "  {0} 年非最新年度，只下載第四季", year);

            var targets = entries
                .Where(e => e.CoId == coId)
                .Where(IsChineseConsolidated)
                .Select(e => new { Entry = e, Quarter = GetQuarter(e.DataYear) })
                .Where(x => x.Quarter > 0)
                .Where(x => isLatest || x.Quarter == 4)
                // 同一季若有多筆，取最後上傳者
                .GroupBy(x => x.Quarter)
                .Select(g => g.OrderBy(x => x.Entry.UploadDate, StringComparer.Ordinal).Last())
                .OrderBy(x => x.Quarter)
                .ToList();

            if (targets.Count == 0)
            {
                Console.WriteLine(isLatest
                    ? "[財報] {0} {1} 年 查無中文版合併財報"
                    : "[財報] {0} {1} 年 查無第四季中文版合併財報", coId, year);
                return 0;
            }

            var count = 0;
            foreach (var t in targets)
            {
                var suffix = t.Quarter == 4 ? "Y" : "Q" + t.Quarter;
                var ext = Path.GetExtension(t.Entry.FileName);
                var destName = string.Format("{0}_{1}{2}{3}", coId, year, suffix, ext);
                var destPath = Path.Combine(outputDir, destName);

                if (await DownloadOneAsync(t.Entry, destPath, overwrite))
                {
                    count++;
                }
            }

            return count;
        }

        private async Task<bool> DownloadOneAsync(DocEntry entry, string destPath, bool overwrite)
        {
            if (File.Exists(destPath) && !overwrite)
            {
                Console.WriteLine("  略過 (已存在) {0}", Path.GetFileName(destPath));
                return true;
            }

            try
            {
                Console.Write("  下載 {0} -> {1} ... ", entry.FileName, Path.GetFileName(destPath));
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

        /// <summary>
        /// 中文版合併財報：說明含「合併」且不含「英文」(例如「IFRSs合併財報」)
        /// </summary>
        private static bool IsChineseConsolidated(DocEntry e)
        {
            return e.Description.Contains("合併")
                && !e.Description.Contains("英文");
        }

        /// <summary>
        /// 由「115 年 第一季」取得季別，無法判斷時回傳 0
        /// </summary>
        private static int GetQuarter(string dataYear)
        {
            foreach (var kv in QuarterMap)
            {
                if (dataYear.Contains(kv.Key))
                {
                    return kv.Value;
                }
            }
            return 0;
        }
    }
}
