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
        /// 30秒檢查一次檔案
        /// </summary>
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        static void main(string[] args) 
        {
            //設定監控位置，將json文件反序列化為C#物件
            string jsonConfig = File.ReadAllText("config.json");
            Config config = JsonSerializer.Deserialize<Config>(jsonConfig);
        }
    }
}
