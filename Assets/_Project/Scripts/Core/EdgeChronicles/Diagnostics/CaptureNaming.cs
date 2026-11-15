using System;
using System.Globalization;
using System.IO;

namespace EdgeChronicles.Core.Diagnostics
{
    /// <summary>
    /// 录制的文件命名规则：<b>日志与截图同源取值</b>。
    /// <para>
    /// 规则：所有产物文件名里的帧号段都取自 <see cref="GlobalFrameId"/> 的同一个
    /// <see cref="FrameId"/>，通过 <see cref="FrameId.ToToken"/> 渲染成固定宽度文本。
    /// 日志与截图<b>不得各自编号</b>——否则事后无法把「第 37 行日志」和「第 37 张截图」对上。
    /// </para>
    /// </summary>
    public static class CaptureNaming
    {
        /// <summary>产物名的统一前缀（一次录制一段）。</summary>
        public const string Prefix = "ec";

        /// <summary>统一的基础名（不含扩展名）：<c>ec_F000000123</c>。</summary>
        public static string BaseName(FrameId id)
        {
            return Prefix + "_" + id.ToToken();
        }

        /// <summary>截图文件名：<c>ec_F000000123_shot.png</c>。</summary>
        public static string ShotFileName(FrameId id, string tag = null)
        {
            return BaseName(id) + "_shot" + TagSuffix(tag) + ".png";
        }

        /// <summary>单帧日志分片文件名：<c>ec_F000000123_log.txt</c>。</summary>
        public static string LogChunkFileName(FrameId id, string tag = null)
        {
            return BaseName(id) + "_log" + TagSuffix(tag) + ".txt";
        }

        /// <summary>整段录制的日志文件名（按录制起始帧号命名）。</summary>
        public static string SessionLogFileName(FrameId sessionStartId)
        {
            return BaseName(sessionStartId) + "_session.log";
        }

        private static string TagSuffix(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return string.Empty;
            return "_" + tag;
        }
    }

    /// <summary>把日志写进内存（供自检与单测读取，不落盘）。</summary>
    public sealed class MemoryCombatLogSink : ICombatLogSink
    {
        private readonly System.Collections.Generic.List<string> _lines;

        public MemoryCombatLogSink(int capacity = 256)
        {
            _lines = new System.Collections.Generic.List<string>(capacity);
        }

        /// <summary>已写出的行（按写入顺序）。</summary>
        public System.Collections.Generic.IReadOnlyList<string> Lines => _lines;

        public void Write(in CombatLogRecord record)
        {
            _lines.Add(CombatLogFormatter.Format(record));
        }

        public void Flush()
        {
        }

        public void Clear()
        {
            _lines.Clear();
        }
    }

    /// <summary>
    /// 把日志写进文件（每次录制一个文件，行内含帧号）。
    /// <para>
    /// 路径由调用方注入：Core 不引用 UnityEngine，取不到 <c>Application.persistentDataPath</c>。
    /// 由 Binding / Flow 层在运行时把 Unity 的持久化目录传进来。
    /// </para>
    /// </summary>
    public sealed class FileCombatLogSink : ICombatLogSink
    {
        private readonly string _path;
        private StreamWriter _writer;

        /// <param name="path">日志文件绝对路径。</param>
        /// <param name="append">是否追加（同一路径重开时用 true）。</param>
        public FileCombatLogSink(string path, bool append = false)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            _path = path;

            // 注意：本类有同名的 Path 属性，必须用全限定名 System.IO.Path，否则被成员遮蔽。
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }

        /// <summary>日志文件路径。</summary>
        public string Path => _path;

        public void Write(in CombatLogRecord record)
        {
            if (_writer == null) return;
            _writer.WriteLine(CombatLogFormatter.Format(record));
        }

        public void Flush()
        {
            _writer?.Flush();
        }

        /// <summary>关闭并释放文件句柄。</summary>
        public void Close()
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }
    }

    /// <summary>
    /// 帧号 → 文本 的统一渲染（供文件名与日志共用）。
    /// </summary>
    public static class FrameIdText
    {
        /// <summary>固定宽度渲染：<c>F000000123</c>。</summary>
        public static string Token(FrameId id)
        {
            return "F" + id.Value.ToString("D9", CultureInfo.InvariantCulture);
        }
    }
}
