#if STICKER_DEBUG
using System;
using System.Text;
using UnityEngine;

namespace StickerFwk.Core.Debug.Pages
{
    /// <summary>
    /// Built-in debug page that tails recent <c>UnityEngine.Debug</c> log output. Subscribes to
    /// <see cref="Application.logMessageReceivedThreaded"/> at construction (the underlying buffer
    /// is guarded by a lock so off-main-thread logs are safe), keeps the most recent
    /// <see cref="Capacity"/> entries in a ring buffer, and rebuilds the rich-text display only
    /// when the buffer or the level filters change.
    /// </summary>
    public sealed class LogsDebugPage : IDebugPage, IDisposable
    {
        private const int Capacity = 200;

        private readonly Entry[] _entries = new Entry[Capacity];
        private readonly object _lock = new object();
        private readonly StringBuilder _stringBuilder = new StringBuilder(4096);

        private int _head;
        private int _count;
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;
        private string _cached = string.Empty;
        private int _cachedRevision = -1;
        private int _revision;

        public LogsDebugPage()
        {
            Application.logMessageReceivedThreaded += OnLog;
        }

        public string Title => "Logs";
        public string Id => "stickerfwk.logs";
        public int Order => 1000;

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        public void Build(IDebugPageBuilder builder)
        {
            builder.Toggle("Show Info", () => _showInfo, v => { _showInfo = v; _cachedRevision = -1; })
                .Toggle("Show Warnings", () => _showWarning, v => { _showWarning = v; _cachedRevision = -1; })
                .Toggle("Show Errors", () => _showError, v => { _showError = v; _cachedRevision = -1; })
                .Button("Clear", Clear)
                .Button("Copy to clipboard", CopyToClipboard)
                .Separator();
            ((DebugPageBuilder)builder).Custom(RenderEntries);
        }

        private void RenderEntries(DebugMenuRenderContext ctx)
        {
            lock (_lock)
            {
                var start = (_head - _count + Capacity) % Capacity;
                var first = true;
                for (var i = 0; i < _count; i++)
                {
                    var entry = _entries[(start + i) % Capacity];
                    if (!ShouldShow(entry.Type))
                    {
                        continue;
                    }
                    if (!first)
                    {
                        DrawSeparator(ctx);
                    }
                    first = false;
                    DrawEntry(entry, ctx);
                }
            }
        }

        private static void DrawEntry(Entry entry, DebugMenuRenderContext ctx)
        {
            string color;
            switch (entry.Type)
            {
                case LogType.Warning:
                    color = "#ffff00";
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    color = "#ff0000";
                    break;
                default:
                    color = "#ffffff";
                    break;
            }
            var style = ctx.Styles.Label;
            var content = new GUIContent("<color=" + color + ">" + entry.Message + "</color>");
            var rect = GUILayoutUtility.GetRect(content, style, GUILayout.ExpandWidth(true));
            ctx.Styles.DrawOutlinedLabel(rect, content, style);
        }

        private static void DrawSeparator(DebugMenuRenderContext ctx)
        {
            GUILayout.Space(6f);
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, ctx.Styles.SeparatorColor, 0f, 0f);
            GUILayout.Space(6f);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _entries[_head] = new Entry { Message = condition, Type = type };
                _head = (_head + 1) % Capacity;
                if (_count < Capacity)
                {
                    _count++;
                }
                _revision++;
            }
        }

        private void Clear()
        {
            lock (_lock)
            {
                _count = 0;
                _head = 0;
                _revision++;
            }
        }

        private void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = BuildLogText();
        }

        private string BuildLogText()
        {
            lock (_lock)
            {
                if (_cachedRevision == _revision)
                {
                    return _cached;
                }
                _stringBuilder.Clear();
                var start = (_head - _count + Capacity) % Capacity;
                for (var i = 0; i < _count; i++)
                {
                    var entry = _entries[(start + i) % Capacity];
                    if (!ShouldShow(entry.Type))
                    {
                        continue;
                    }
                    _stringBuilder.Append(entry.Message);
                    _stringBuilder.Append('\n');
                }
                _cached = _stringBuilder.ToString();
                _cachedRevision = _revision;
                return _cached;
            }
        }

        private bool ShouldShow(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return _showInfo;
                case LogType.Warning:
                    return _showWarning;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return _showError;
                default:
                    return true;
            }
        }

        private struct Entry
        {
            public string Message;
            public LogType Type;
        }
    }
}
#endif
