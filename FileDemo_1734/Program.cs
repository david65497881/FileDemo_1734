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
        /// 儲存檔案快照和最後讀取的行數。string為鍵，代表檔案的名稱、int為質，代表檔案的行數快照
        /// </summary>
        private static ConcurrentDictionary<string, int> FileContentLineCounts = new ConcurrentDictionary<string, int>();
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
            // 讀取config.json，並將config.json反序列化成config物件
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            // 設定監控位置。使用@來避免使用跳脫字符
            string monitorDirectory = @"C:\temp\TEST";
            config.DirectoryPath = monitorDirectory;

            // 檢查並建立目錄以及檔案
            FolderFileCreate(monitorDirectory, config.FilesToMonitor);

            Console.WriteLine($"正在監控目錄: {config.DirectoryPath}");
            DisplayMonitoredFiles(config.FilesToMonitor);

            // 初始化每個檔案的快照行數
            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);
                if (File.Exists(filePath))
                {
                    int lineCount = ReadFileLines(filePath).Count;
                    FileContentLineCounts[filePath] = lineCount;
                }
                else
                {
                    FileContentLineCounts[filePath] = 0;
                }
            }

            // 啟動定時器，每隔CheckInterval所設定的秒數檢查一次檔案
            checkFilesTimer = new Timer(CheckFileChange, config, TimeSpan.Zero, CheckInterval);

            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;

            checkFilesTimer?.Dispose();
        }

        /// <summary>
        /// 建立資料夾檔案
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="filesToMonitor"></param>
        private static void FolderFileCreate(string directoryPath, string[] filesToMonitor)
        {
            //Directory.Exists()會傳回布林值。Directory可以用來執行與目錄相關的操作，如新增刪除移動以及檢查目錄存在等
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"已建立目錄: {directoryPath}");
            }

            foreach (var file in filesToMonitor)
            {
                string filePath = Path.Combine(directoryPath, file);
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                    Console.WriteLine($"已建立檔案: {filePath}");
                }
            }
        }

        /// <summary>
        /// 印出監控檔案字串
        /// </summary>
        /// <param name="fileToMonitor"></param>
        private static void DisplayMonitoredFiles(string[] fileToMonitor)
        {
            foreach (var file in fileToMonitor)
            {
                Console.WriteLine($"正在監控檔案: {file}");
            }
        }

        /// <summary>
        /// 逐行讀取檔案，減少記憶體佔用
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static List<string> ReadFileLines(string filePath)
        {
            var lines = new List<string>();
            //使用StreamReader以讀取模式開啟檔案
            using (var reader = new StreamReader(filePath))
            {
                string line;
                //使用 while 迴圈逐行讀取檔案內容，ReadLine()方法每次讀取一行，直到讀到檔案末尾返回null
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        /// <summary>
        /// 監測檔案變化的方法
        /// </summary>
        /// <param name="state"></param>
        private static void CheckFileChange(object state)
        {
            try
            {
                Config config = (Config)state;

                foreach (var file in config.FilesToMonitor)
                {
                    string filePath = Path.Combine(config.DirectoryPath, file);

                    // 使用File.Exists 方法來檢查 filePath 所指向的檔案是否存在
                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"正在檢查檔案: {filePath}");

                        var newContent = ReadFileLines(filePath);
                        //.TryGetValue用來取得FileContentLineCounts裡最後讀取的行數
                        if (FileContentLineCounts.TryGetValue(filePath, out int previousLineCount))
                        {
                            // 檢查新增加的行
                            if (newContent.Count > previousLineCount)
                            {
                                for (int i = previousLineCount; i < newContent.Count; i++)
                                {
                                    Console.WriteLine($"新增的行: {newContent[i]}");
                                }

                                // 更新快照中的行數
                                FileContentLineCounts[filePath] = newContent.Count;
                            }
                        }
                        else
                        {
                            // 如果檔案在快照中不存在，初始化其行數
                            FileContentLineCounts[filePath] = newContent.Count;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發生錯誤: {ex.Message}");
            }
        }
    }
}
