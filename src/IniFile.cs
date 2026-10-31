using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ZzzSwitch
{
    // 无损 INI：按行保存，保留原有 CRLF、段名大小写、行序、BOM 与末尾空行。
    // 只做「定位 + 改单行 + 插一行 + 删一行」，从不重新序列化整份文件。
    public sealed class IniFile
    {
        static readonly Regex SectionRx = new Regex(@"^\s*\[([^\]]*)\]\s*$");
        static readonly Regex KeyRx = new Regex(@"^\s*([^=;#\[\]]+?)\s*=(.*)$");

        readonly List<string> _lines = new List<string>();
        string _eol = "\r\n";
        bool _trailingEol = true;
        bool _bom;

        public static readonly string[] ChannelSections = new string[] { "General", "general", "hyp" };

        public string Eol { get { return _eol; } }
        public int LineCount { get { return _lines.Count; } }
        public bool HasBom { get { return _bom; } }

        public static IniFile FromFile(string path)
        {
            return FromBytes(File.ReadAllBytes(path));
        }

        public static IniFile FromBytes(byte[] bytes)
        {
            IniFile ini = new IniFile();
            int off = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                ini._bom = true;
                off = 3;
            }
            string text = new UTF8Encoding(false, false).GetString(bytes, off, bytes.Length - off);
            string eol = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? "\r\n"
                : (text.IndexOf('\n') >= 0 ? "\n" : "\r\n");
            ini._eol = eol;
            ini._trailingEol = text.EndsWith(eol, StringComparison.Ordinal);
            string body = (ini._trailingEol && text.Length >= eol.Length)
                ? text.Substring(0, text.Length - eol.Length)
                : text;
            if (body.Length > 0) ini._lines.AddRange(body.Split(new string[] { eol }, StringSplitOptions.None));
            return ini;
        }

        public byte[] ToBytes()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++)
            {
                sb.Append(_lines[i]);
                if (i < _lines.Count - 1 || _trailingEol) sb.Append(_eol);
            }
            byte[] raw = new UTF8Encoding(false).GetBytes(sb.ToString());
            if (!_bom) return raw;
            byte[] all = new byte[3 + raw.Length];
            all[0] = 0xEF; all[1] = 0xBB; all[2] = 0xBF;
            Buffer.BlockCopy(raw, 0, all, 3, raw.Length);
            return all;
        }

        // 返回 { headerIndex, firstContentLine, endLineExclusive }，找不到段返回 null
        int[] FindSection(string name)
        {
            int header = -1;
            for (int i = 0; i < _lines.Count; i++)
            {
                Match m = SectionRx.Match(_lines[i]);
                if (!m.Success) continue;
                if (header < 0)
                {
                    if (string.Equals(m.Groups[1].Value.Trim(), name, StringComparison.OrdinalIgnoreCase)) header = i;
                }
                else
                {
                    return new int[] { header, header + 1, i };
                }
            }
            if (header < 0) return null;
            return new int[] { header, header + 1, _lines.Count };
        }

        int IndexOfKey(string section, string key)
        {
            int[] b = FindSection(section);
            if (b == null) return -1;
            for (int i = b[1]; i < b[2]; i++)
            {
                Match m = KeyRx.Match(_lines[i]);
                if (m.Success && string.Equals(m.Groups[1].Value.Trim(), key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public string Get(string section, string key)
        {
            int idx = IndexOfKey(section, key);
            if (idx < 0) return null;
            Match m = KeyRx.Match(_lines[idx]);
            return m.Groups[2].Value.Trim();
        }

        public bool HasKey(string section, string key) { return IndexOfKey(section, key) >= 0; }
        public bool HasSection(string name) { return FindSection(name) != null; }

        public bool Set(string section, string key, string value)
        {
            int idx = IndexOfKey(section, key);
            if (idx < 0) return false;
            Match m = KeyRx.Match(_lines[idx]);
            _lines[idx] = m.Groups[1].Value.Trim() + "=" + value;
            return true;
        }

        public void SetOrAdd(string section, string key, string value, string afterKey)
        {
            if (Set(section, key, value)) return;
            int[] b = FindSection(section);
            if (b == null)
            {
                if (_lines.Count > 0 && _lines[_lines.Count - 1].Trim().Length != 0) _lines.Add("");
                _lines.Add("[" + section + "]");
                _trailingEol = true;
                _lines.Add(key + "=" + value);
                return;
            }
            int insertAt = b[2];
            if (!string.IsNullOrEmpty(afterKey))
            {
                int a = IndexOfKey(section, afterKey);
                if (a >= 0) insertAt = a + 1;
            }
            _lines.Insert(insertAt, key + "=" + value);
        }

        public bool Remove(string section, string key)
        {
            int idx = IndexOfKey(section, key);
            if (idx < 0) return false;
            _lines.RemoveAt(idx);
            return true;
        }

        public int CountKeys(string section)
        {
            int[] b = FindSection(section);
            if (b == null) return 0;
            int n = 0;
            for (int i = b[1]; i < b[2]; i++) if (KeyRx.IsMatch(_lines[i])) n++;
            return n;
        }

        public string GetFirst(string key)
        {
            for (int i = 0; i < ChannelSections.Length; i++)
            {
                string v = Get(ChannelSections[i], key);
                if (v != null) return v;
            }
            return null;
        }

        public string SectionHolding(string key)
        {
            for (int i = 0; i < ChannelSections.Length; i++)
                if (HasKey(ChannelSections[i], key)) return ChannelSections[i];
            return ChannelSections[0];
        }
    }
}
