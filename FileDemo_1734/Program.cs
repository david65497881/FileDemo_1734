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
        private static ConcurrentDictionary<string, DateTime> LastChangedTimes = new ConcurrentDictionary<string, DateTime>();
        private static readonly int EventSuppressTimeoutMs = 500;
        private static Timer displayTimer;

        // 儲存每個檔案的內容快照
        private static ConcurrentDictionary<string, List<string>> FileContentSnapshots = new ConcurrentDictionary<string, List<string>>();

        static void Main(string[] args)
        {
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            Console.WriteLine("正在監控目錄: " + config.DirectoryPath);
            DisplayMonitoredFiles(config.FilesToMonitor);

            // 初始化每個檔案的快照
            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);
                if (File.Exists(filePath))
                {
                    FileContentSnapshots[filePath] = File.ReadAllLines(filePath).ToList();
                }
                else
                {
                    FileContentSnapshots[filePath] = new List<string>();
                }
            }

            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = config.DirectoryPath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.Created += (source, e) => OnChanged(e, config.FilesToMonitor);
            watcher.Deleted += (source, e) => OnDeleted(e, config.FilesToMonitor);
            watcher.EnableRaisingEvents = true;

            displayTimer = new Timer(DisplayFiles, config.FilesToMonitor, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;
        }

        private static void DisplayFiles(object state)
        {
            string[] filesToMonitor = (string[])state;
            Console.WriteLine("\n----- 每30秒顯示監控檔案 -----");
            DisplayMonitoredFiles(filesToMonitor);
        }

        private static void DisplayMonitoredFiles(string[] filesToMonitor)
        {
            foreach (var file in filesToMonitor)
            {
                Console.WriteLine("正在監控檔案: " + file);
            }
        }

        private static void OnChanged(FileSystemEventArgs e, string[] filesToMonitor)
        {
            if (Array.Exists(filesToMonitor, file => file == Path.GetFileName(e.FullPath)))
            {
                DateTime lastChangeTime = LastChangedTimes.GetOrAdd(e.FullPath, DateTime.MinValue);
                if ((DateTime.Now - lastChangeTime).TotalMilliseconds > EventSuppressTimeoutMs)
                {
                    Console.WriteLine($"檔案: {e.FullPath} 變動類型: {e.ChangeType}");

                    Thread.Sleep(100); // 確保檔案變動完成
                    int retryCount = 3;
                    for (int i = 0; i < retryCount; i++)
                    {
                        try
                        {
                            var newContent = File.ReadAllLines(e.FullPath).ToList();
                            var oldContent = FileContentSnapshots.GetOrAdd(e.FullPath, new List<string>());

                            // 找出新增的行
                            if (newContent.Count > oldContent.Count)
                            {
                                for (int j = oldContent.Count; j < newContent.Count; j++)
                                {
                                    Console.WriteLine($"新增的行: {newContent[j]}");
                                }
                            }
                            // 找出刪除的行
                            else if (newContent.Count < oldContent.Count)
                            {
                                for (int j = newContent.Count; j < oldContent.Count; j++)
                                {
                                    Console.WriteLine($"刪除的行: {oldContent[j]}");
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

                            FileContentSnapshots[e.FullPath] = newContent; // 更新快照
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

                    LastChangedTimes[e.FullPath] = DateTime.Now;
                }
            }
        }

        private static void OnDeleted(FileSystemEventArgs e, string[] filesToMonitor)
        {
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
