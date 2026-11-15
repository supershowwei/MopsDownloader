using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace MopsDownloader
{
    /// <summary>
    /// 解析 t57sb01 step=1 回傳的清單 HTML
    /// </summary>
    public static class DocListParser
    {
        private static readonly Regex RowRegex = new Regex(
            @"<tr[^>]*>(.*?)</tr>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CellRegex = new Regex(
            @"<td[^>]*>(.*?)</td>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // javascript:readfile2("A","8436","202601_8436_AI1.pdf");
        private static readonly Regex LinkRegex = new Regex(
            @"readfile2?\(\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        private static readonly Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        // 欄位順序：證券代號, 資料年度, 資料類型, 結案類型, 性質, 資料細節說明, 備註, 電子檔案, 檔案大小, 上傳日期, 更(補)正
        private const int ColCoId = 0;
        private const int ColDataYear = 1;
        private const int ColDescription = 5;
        private const int ColFile = 7;
        private const int ColUploadDate = 9;

        public static List<DocEntry> Parse(string html)
        {
            var result = new List<DocEntry>();
            if (string.IsNullOrEmpty(html))
            {
                return result;
            }

            foreach (Match row in RowRegex.Matches(html))
            {
                var cells = CellRegex.Matches(row.Groups[1].Value)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                if (cells.Count <= ColUploadDate)
                {
                    continue;
                }

                var link = LinkRegex.Match(cells[ColFile]);
                if (!link.Success)
                {
                    continue;
                }

                result.Add(new DocEntry
                {
                    CoId = CleanText(cells[ColCoId]),
                    DataYear = CleanText(cells[ColDataYear]),
                    Description = CleanText(cells[ColDescription]),
                    Kind = link.Groups[1].Value,
                    FileName = link.Groups[3].Value,
                    UploadDate = CleanText(cells[ColUploadDate]),
                });
            }

            return result;
        }

        private static string CleanText(string html)
        {
            var text = WebUtility.HtmlDecode(TagRegex.Replace(html, string.Empty));
            // &nbsp; 解碼後為 U+00A0
            text = text.Replace(' ', ' ');
            return SpaceRegex.Replace(text, " ").Trim();
        }
    }
}
