using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZzzSwitch
{
    // 极简分级日志：同时写 switch.log 与内存环形缓冲（供 UI 显示尾部）
    public static class Log
    {
        static readonly object Sync = new object();
        static readonly List<string> Tail = new List<string>();
        const int TailMax = 200;

        public static string FilePath = null;

        public static void Info(string msg) { Write("INFO ", msg); }
        public static void Warn(string msg) { Write("WARN ", msg); }
        public static void Error(string msg) { Write("ERROR", msg); }

        static void Write(string level, string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + (msg == null ? "" : msg);
            lock (Sync)
            {
                Tail.Add(line);
                if (Tail.Count > TailMax) Tail.RemoveAt(0);
                if (!string.IsNullOrEmpty(FilePath))
                {
                    try { File.AppendAllText(FilePath, line + "\r\n", new UTF8Encoding(false)); }
                    catch { }
                }
            }
        }

        public static string[] GetTail(int count)
        {
            lock (Sync)
            {
                int start = Tail.Count - count;
                if (start < 0) start = 0;
                return Tail.GetRange(start, Tail.Count - start).ToArray();
            }
        }
    }
}
