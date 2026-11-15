using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MopsDownloader
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Options opt;
            try
            {
                opt = Options.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine();
                PrintUsage();
                return 2;
            }

            if (opt == null)
            {
                PrintUsage();
                return 2;
            }

            var allOk = true;
            using (var client = new TwseDocClient())
            {
                foreach (var coId in opt.CoIds)
                {
                    try
                    {
                        if (opt.Command == "fin")
                        {
                            var downloader = new FinancialReportDownloader(client);
                            var n = await downloader.DownloadAsync(coId, opt.Year, opt.OutputDir, opt.Overwrite);
                            allOk &= n > 0;
                        }
                        else
                        {
                            var downloader = new AnnualReportDownloader(client);
                            allOk &= await downloader.DownloadAsync(coId, opt.Year, opt.OutputDir, opt.Overwrite);
                        }
                    }
                    catch (Exception ex)
                    {
                        allOk = false;
                        Console.Error.WriteLine("[錯誤] {0}：{1}", coId, ex.Message);
                    }
                }
            }

            return allOk ? 0 : 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  MopsDownloader fin    <股票代號> <年度> [-o 輸出資料夾] [-f]");
            Console.WriteLine("  MopsDownloader annual <股票代號> <年度> [-o 輸出資料夾] [-f]");
            Console.WriteLine();
            Console.WriteLine("指令：");
            Console.WriteLine("  fin      下載財務報告書 (中文版合併財報)，檔名：代號_年度Q1/Q2/Q3/Y");
            Console.WriteLine("  annual   下載股東會年報 (F11 優先，否則 F04)，檔名：代號_年度F11/F04");
            Console.WriteLine();
            Console.WriteLine("參數：");
            Console.WriteLine("  股票代號  可用逗號分隔多檔，例如 2330,8436");
            Console.WriteLine("  年度      民國年，例如 114");
            Console.WriteLine("  -o        輸出資料夾，預設為目前目錄下的 downloads");
            Console.WriteLine("  -f        檔案已存在時覆寫 (預設略過)");
            Console.WriteLine();
            Console.WriteLine("範例：");
            Console.WriteLine("  MopsDownloader fin 8436 115");
            Console.WriteLine("  MopsDownloader annual 1582 114 -o D:\\reports");
        }

        private class Options
        {
            public string Command { get; private set; }
            public List<string> CoIds { get; private set; }
            public int Year { get; private set; }
            public string OutputDir { get; private set; }
            public bool Overwrite { get; private set; }

            /// <summary>
            /// 解析命令列參數，參數不足時回傳 null
            /// </summary>
            public static Options Parse(string[] args)
            {
                var positional = new List<string>();
                var opt = new Options { OutputDir = Path.Combine(Environment.CurrentDirectory, "downloads") };

                for (var i = 0; i < args.Length; i++)
                {
                    var a = args[i];
                    switch (a.ToLowerInvariant())
                    {
                        case "-o":
                        case "--output":
                            if (i + 1 >= args.Length)
                            {
                                throw new ArgumentException("-o 需要指定輸出資料夾");
                            }
                            opt.OutputDir = Path.GetFullPath(args[++i]);
                            break;
                        case "-f":
                        case "--force":
                            opt.Overwrite = true;
                            break;
                        case "-h":
                        case "--help":
                        case "/?":
                            return null;
                        default:
                            positional.Add(a);
                            break;
                    }
                }

                if (positional.Count < 3)
                {
                    return null;
                }

                var cmd = positional[0].ToLowerInvariant();
                if (cmd == "financial")
                {
                    cmd = "fin";
                }
                if (cmd != "fin" && cmd != "annual")
                {
                    throw new ArgumentException("未知的指令：" + positional[0]);
                }
                opt.Command = cmd;

                opt.CoIds = positional[1]
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Distinct()
                    .ToList();
                if (opt.CoIds.Count == 0)
                {
                    throw new ArgumentException("請指定股票代號");
                }

                int year;
                if (!int.TryParse(positional[2], out year) || year <= 0)
                {
                    throw new ArgumentException("年度格式錯誤：" + positional[2]);
                }
                opt.Year = year;

                return opt;
            }
        }
    }
}
