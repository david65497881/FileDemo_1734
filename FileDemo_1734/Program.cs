using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading;
using FileDemo_1734.Class;
using System.Collections.Generic;
using System.Linq;

namespace FileDemo_1734
{
    public class Program
    {
        /// <summary>
        /// 最後變動時間
        /// </summary>
        private static ConcurrentDictionary<string, DateTime> LastChangedTimes = new ConcurrentDictionary<string, DateTime>();
        /// <summary>
        /// 避免檔案的change事件在500豪秒內重複觸發
        /// </summary>
        private static readonly int EventSuppressTimeoutMs = 500;
        /// <summary>
        /// 計時器
        /// </summary>
        private static Timer displayTimer;

        /// <summary>
        /// 儲存每個檔案的內容快照
        /// </summary>
        private static ConcurrentDictionary<string, List<string>> FileContentSnapshots = new ConcurrentDictionary<string, List<string>>();

        static void Main(string[] args)
        {
            //讀取config.json，並將config.json反序列化成config物件
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            // 設定監控目錄位置。@符號表示這是一個逐字字串，可以直接使用反斜線而不用加上跳脫字符
            string monitorDirectory = @"C:\temp\TEST";
            config.DirectoryPath = monitorDirectory;

            // 檢查並建立目錄和檔案
            FolderFileCreate(monitorDirectory, config.FilesToMonitor);

            Console.WriteLine("正在監控目錄: " + config.DirectoryPath);
            DisplayMonitoredFiles(config.FilesToMonitor);

            // 初始化每個檔案的快照
            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);
                //file.Exists用來檢查指定的檔案路徑是否存在
                if (File.Exists(filePath))
                {
                    FileContentSnapshots[filePath] = File.ReadAllLines(filePath).ToList();
                }
                else
                {
                    FileContentSnapshots[filePath] = new List<string>();
                }
            }

            //FileSystemWatcher是用於監控檔案系統變化的類別
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = config.DirectoryPath,//指定目錄
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size//狀態變化
            };

            //Lambda表達式，+=用於訂閱 (source, e)是Lambda表達式的參數，OnChanged(e, config.FilesToMonitor)是Lambda表達式的主體(執行動作)
            // =>代表執行的意思
            watcher.Changed += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.Created += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.Deleted += (source, e) => OnDeleted(e, config.FilesToMonitor);
            watcher.EnableRaisingEvents = true;

            displayTimer = new Timer(DisplayFiles, config.FilesToMonitor, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;
        }

        /// <summary>
        /// 檢查目錄、檔案是否存在
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="filesToMonitor"></param>
        private static void FolderFileCreate(string directoryPath, string[] filesToMonitor)
        {
            // 檢查並建立目錄
            if (!Directory.Exists(directoryPath))
            {
                //若指定的檔案路徑不存在，CreateDirectory會自動建立
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"已建立目錄: {directoryPath}");
            }

            // 檢查並建立檔案 
            foreach (var file in filesToMonitor)
            {
                //使用 Path.Combine 方法安全地將目錄路徑和檔案名稱組合成完整的檔案路徑
                string filePath = Path.Combine(directoryPath, file);
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose(); // 建立檔案並立即釋放
                    Console.WriteLine($"已建立檔案: {filePath}");
                }
            }
        }

        /// <summary>
        /// 每30秒顯示一次監控檔案
        /// </summary>
        /// <param name="state"></param>
        private static void DisplayFiles(object state)
        {
            string[] filesToMonitor = (string[])state;
            Console.WriteLine("\n----- 每30秒顯示監控檔案 -----");
            DisplayMonitoredFiles(filesToMonitor);
        }

        /// <summary>
        /// 正在監控檔案
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
        /// 檔案的變動方法
        /// </summary>
        /// <param name="e"></param>
        /// <param name="filesToMonitor"></param>
        private static void OnChanged(FileSystemEventArgs e, string[] filesToMonitor)
        {
            //Array.Exists 用來檢查 filesToMonitor 陣列中是否存在符合條件的檔案
            //file => file == Path.GetFileName(e.FullPath)用來比較檔案名稱是否相等
            if (Array.Exists(filesToMonitor, file => file == Path.GetFileName(e.FullPath)))
            {
                //避免短時間多次觸發方法
                DateTime lastChangeTime = LastChangedTimes.GetOrAdd(e.FullPath, DateTime.MinValue);
                if ((DateTime.Now - lastChangeTime).TotalMilliseconds > EventSuppressTimeoutMs)
                {
                    Console.WriteLine($"檔案: {e.FullPath} 變動類型: {e.ChangeType}");

                    Thread.Sleep(100); // 確保檔案變動完成
                    int retryCount = 3;
                    //使用for迴圈，如果檔案被佔用，最多重試 3 次
                    for (int i = 0; i < retryCount; i++)
                    {
                        try
                        {
                            //讀取檔案的當前內容，並轉為 List<string> 格式
                            var newContent = File.ReadAllLines(e.FullPath).ToList();
                            //取得先前儲存的檔案快照，若不存在，使用List<string>作為預設值
                            var oldContent = FileContentSnapshots.GetOrAdd(e.FullPath, new List<string>());

                            // 使用 HashSet 來比對被刪除的行
                            var oldLinesSet = new HashSet<string>(oldContent);
                            var newLinesSet = new HashSet<string>(newContent);

                            // 找出被刪除的行
                            var deletedLines = oldLinesSet.Except(newLinesSet);
                            foreach (var line in deletedLines)
                            {
                                Console.WriteLine($"刪除的行: {line}");
                            }

                            // 找出新增的行
                            if (newContent.Count > oldContent.Count)
                            {
                                for (int j = oldContent.Count; j < newContent.Count; j++)
                                {
                                    Console.WriteLine($"新增的行: {newContent[j]}");
                                }
                            }
                            
                            // 找出修改的行
                            else
                            {
                                for (int j = 0; j < newContent.Count; j++)
                                {
                                    if (newContent[j] != oldContent[j])
                                    {
                                        Console.WriteLine($"修改的行: 原內容 - {oldContent[j]}, 新內容 - {newContent[j]}");
                                    }
                                }
                            }

                            //將檔案的新內容更新至快照中，以便下次檢測變動時進行比較
                            FileContentSnapshots[e.FullPath] = newContent;
                            break;
                        }
                        catch (IOException)
                        {
                            if (i == retryCount - 1)
                            {
                                Console.WriteLine("無法讀取檔案內容，檔案正被佔用：" + e.FullPath);
                            }
                            else
                            {
                                Thread.Sleep(100); // 等待後重試
                            }
                        }
                    }

                    //更新LastChangedTimes中的變動時間，以便下次變動檢測時進行抑制
                    LastChangedTimes[e.FullPath] = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 檔案的刪除
        /// </summary>
        /// <param name="e"></param>
        /// <param name="filesToMonitor"></param>
        private static void OnDeleted(FileSystemEventArgs e, string[] filesToMonitor)
        {
            //Array.Exists 用來檢查 filesToMonitor 陣列中是否存在符合條件的檔案
            //file => file == Path.GetFileName(e.FullPath)用來比較檔案名稱是否相等
            if (Array.Exists(filesToMonitor, file => file == Path.GetFileName(e.FullPath)))
            {
                Console.WriteLine($"檔案: {e.FullPath} 已刪除");

                // 刪除快照中的檔案紀錄
                FileContentSnapshots.TryRemove(e.FullPath, out _);
                LastChangedTimes.TryRemove(e.FullPath, out _);
            }
        }
    }
}
