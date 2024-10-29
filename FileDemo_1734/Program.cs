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
        /// 儲存檔案快照
        /// </summary>
        private static ConcurrentDictionary<string,List<string>> FileContentSnapshots = new ConcurrentDictionary<string,List<string>>();
        /// <summary>
        /// 定時器
        /// </summary>
        private static Timer checkFilesTimer;
        /// <summary>
        /// 5秒檢查一次檔案
        /// </summary>
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        static void Main(string[] args) 
        {
            //讀取config.json，並將config.json反序列化成config物件
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            //設定監控位置。使用@來避免使用跳脫字符
            string monitorDirectory = @"C:\temp\TEST";
            config.DirectoryPath = monitorDirectory;

            //檢查並建立目錄以及檔案
            FolderFileCreate(monitorDirectory, config.FilesToMonitor);

            Console.WriteLine($"正在監控目錄:{config.DirectoryPath}");
            DisplayMonitoredFiles(config.FilesToMonitor);

            //初始化每個檔案的快照
            foreach(var file in config.FilesToMonitor)
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

            //啟動定時器，每隔CheckInterval所設定的秒數檢查一次檔案
            checkFilesTimer = new Timer(CheckFileChange, config, TimeSpan.Zero, CheckInterval);

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;

            checkFilesTimer?.Dispose();
        }

        private static void FolderFileCreate(string directoryPath, string[] filesToMonitor) 
        {
            if(!Directory.Exists(directoryPath)) 
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"已建立目錄:{directoryPath}");
            }

            foreach (var file in filesToMonitor) 
            {
                string filePath = Path.Combine(directoryPath, file);
                if (!File.Exists(filePath)) 
                {
                    File.Create(filePath).Dispose();
                    Console.WriteLine($"已建立檔案:{filePath}");
                }
            }
        }

        private static void DisplayMonitoredFiles(string[] fileToMonitor) 
        {
            foreach (var file in fileToMonitor)
            {
                Console.WriteLine($"正在監控檔案:{file}");
            }
        }

        private static void CheckFileChange(object state)
        {
            Config config = (Config)state;

            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);

                //使用File.Exists 方法來檢查 filePath 所指向的檔案是否存在
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"正在檢查檔案: {filePath}");

                    //使用 File.ReadAllLines 方法讀取檔案的每一行，並將結果存儲在 List<string> 中
                    var newContent = File.ReadAllLines(filePath).ToList();
                    //使用 FileContentSnapshot取得舊的快照，GetOrAdd會檢查是否已有此檔案的快照，若有則返回舊內容。沒有就賦予一個新的List<string>作為預設值
                    var oldContent = FileContentSnapshots.GetOrAdd(filePath, new List<string>());

                    //HashSet<>用於查詢刪除插入具有較高的性能
                    var newContentSet = new HashSet<string>(newContent);
                    var oldContentSet = new HashSet<string>(oldContent);

                    // 找出新增的行（在newContentSet中存在但不在oldContentSet中）
                    foreach (var line in newContentSet.Except(oldContentSet))
                    {
                        Console.WriteLine($"新增的行: {line}");
                    }

                    // 找出修改的行
                    if (newContent.Count == oldContent.Count)
                    {
                        for (int j = 0; j < newContent.Count; j++)
                        {
                            if (newContent[j] != oldContent[j])
                            {
                                Console.WriteLine($"修改的行: 原內容 - {oldContent[j]}, 新內容 - {newContent[j]}");
                            }
                        }
                    }

                    // 更新快照
                    FileContentSnapshots[filePath] = newContent;
                }
            }
        }


    }
}
