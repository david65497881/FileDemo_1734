using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FileDemo_1734.Class;
using System.Threading;

namespace FileDemo_1734
{
    public class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILE_ID_INFO
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdHigh;
            public ulong FileIdLow;
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandleEx(
            IntPtr hFile,
            int fileInformationClass,
            out FILE_ID_INFO lpFileInformation,
            uint dwBufferSize);

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;

        private static ConcurrentDictionary<FILE_ID_INFO, List<string>> FileContentSnapshots = new ConcurrentDictionary<FILE_ID_INFO, List<string>>();
        private static Timer checkFilesTimer;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

        static void Main(string[] args)
        {
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);

            string monitorDirectory = @"C:\temp\TEST";
            config.DirectoryPath = monitorDirectory;

            FolderFileCreate(monitorDirectory, config.FilesToMonitor);

            Console.WriteLine($"正在監控目錄:{config.DirectoryPath}");
            DisplayMonitoredFiles(config.FilesToMonitor);

            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);
                if (File.Exists(filePath))
                {
                    var fileId = GetFileId(filePath);
                    if (fileId != null)
                    {
                        FileContentSnapshots[fileId.Value] = ReadFileLines(filePath);
                    }
                }
            }

            checkFilesTimer = new Timer(CheckFileChange, config, TimeSpan.Zero, CheckInterval);
            Console.WriteLine("按下 'q' 鍵結束程式。");
            while (Console.Read() != 'q') ;

            checkFilesTimer?.Dispose();
        }

        private static FILE_ID_INFO? GetFileId(string filePath)
        {
            IntPtr handle = CreateFile(filePath, GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            FILE_ID_INFO fileIdInfo;
            if (GetFileInformationByHandleEx(handle, 18, out fileIdInfo, (uint)Marshal.SizeOf<FILE_ID_INFO>()))
            {
                return fileIdInfo;
            }

            return null;
        }

        private static void FolderFileCreate(string directoryPath, string[] filesToMonitor)
        {
            if (!Directory.Exists(directoryPath))
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

        private static List<string> ReadFileLines(string filePath)
        {
            var lines = new List<string>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        private static void CheckFileChange(object state)
        {
            try
            {
                Config config = (Config)state;

                foreach (var file in config.FilesToMonitor)
                {
                    string filePath = Path.Combine(config.DirectoryPath, file);
                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"正在檢查檔案: {filePath}");

                        var fileId = GetFileId(filePath);
                        if (fileId != null)
                        {
                            var newContent = ReadFileLines(filePath);
                            var oldContent = FileContentSnapshots.GetOrAdd(fileId.Value, new List<string>());

                            var newContentSet = new HashSet<string>(newContent);
                            var oldContentSet = new HashSet<string>(oldContent);

                            foreach (var line in newContentSet.Except(oldContentSet))
                            {
                                Console.WriteLine($"新增的行: {line}");
                            }

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

                            FileContentSnapshots[fileId.Value] = newContent;
                        }
                    }
                }

                if (FileContentSnapshots.Count > 10)
                {
                    foreach (var key in FileContentSnapshots.Keys.Take(5))
                    {
                        FileContentSnapshots.TryRemove(key, out _);
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
