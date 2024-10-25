using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading;
using FileDemo_1734.Class;

namespace FileDemo_1734
{
    public class Program
    {
        // 用來記錄最後觸發事件的時間
        private static ConcurrentDictionary<string, DateTime> LastChangedTimes = new ConcurrentDictionary<string, DateTime>();
        private static readonly int EventSuppressTimeoutMs = 500; // 500 毫秒內不重複觸發事件
        private static Timer displayTimer; // 用來每30秒顯示監控檔案

        static void Main(string[] args)
        {
            // 讀取並反序列化 JSON 設定檔
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            // 顯示正在監控的目錄與檔案
            Console.WriteLine("正在監控目錄: " + config.DirectoryPath);
            DisplayMonitoredFiles(config.FilesToMonitor);

            // 創建 FileSystemWatcher 來監控目錄與檔案
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = config.DirectoryPath, // 設定要監控的目錄
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            // 訂閱檔案變動的事件
            watcher.Changed += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.Created += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.EnableRaisingEvents = true; // 啟用監控

            // 設定一個 Timer 每30秒重新顯示一次監控檔案
            displayTimer = new Timer(DisplayFiles, config.FilesToMonitor, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ; // 持續執行程式直到按下 'q'
        }

        /// <summary>
        /// 定義 Timer 的回呼函數，每30秒顯示一次監控檔案
        /// </summary>
        /// <param name="state"></param>
        private static void DisplayFiles(object state)
        {
            string[] filesToMonitor = (string[])state;
            Console.WriteLine("\n----- 每30秒顯示監控檔案 -----");
            DisplayMonitoredFiles(filesToMonitor);
        }

        /// <summary>
        /// 顯示目前正在監控的檔案
        /// </summary>
        /// <param name="filesToMonitor"></param>
        private static void DisplayMonitoredFiles(string[] filesToMonitor)
        {
            foreach (var file in filesToMonitor)
            {
                Console.WriteLine("正在監控檔案: " + file);
            }
        }

        /// <summary>
        /// 處理檔案變動的事件
        /// </summary>
        /// <param name="e"></param>
        /// <param name="filesToMonitor"></param>
        private static void OnChanged(FileSystemEventArgs e, string[] filesToMonitor)
        {
            // 檢查變動的檔案是否在監控清單中
            if (Array.Exists(filesToMonitor, file => file == Path.GetFileName(e.FullPath)))
            {
                // 確保不會在短時間內多次處理同一個檔案的變動
                DateTime lastChangeTime = LastChangedTimes.GetOrAdd(e.FullPath, DateTime.MinValue);
                if ((DateTime.Now - lastChangeTime).TotalMilliseconds > EventSuppressTimeoutMs)
                {
                    Console.WriteLine($"檔案: {e.FullPath} 變動類型: {e.ChangeType}");
                    LastChangedTimes[e.FullPath] = DateTime.Now; // 更新最後變動時間
                }
            }
        }
    }
}
