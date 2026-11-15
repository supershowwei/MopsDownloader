namespace MopsDownloader
{
    /// <summary>
    /// 電子資料查詢作業清單中的一筆檔案資料
    /// </summary>
    public class DocEntry
    {
        /// <summary>證券代號</summary>
        public string CoId { get; set; }

        /// <summary>資料年度欄位原文，例如「115 年 第一季」、「114 年」</summary>
        public string DataYear { get; set; }

        /// <summary>資料細節說明，例如「IFRSs合併財報」、「股東會年報(股東會後修訂本)」</summary>
        public string Description { get; set; }

        /// <summary>readfile2 的 kind 參數 (A=財報, F=股東會)</summary>
        public string Kind { get; set; }

        /// <summary>伺服器端檔名，例如 202601_8436_AI1.pdf</summary>
        public string FileName { get; set; }

        /// <summary>上傳日期原文，例如 115/05/08 19:01:11</summary>
        public string UploadDate { get; set; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", CoId, DataYear, Description, FileName);
        }
    }
}
