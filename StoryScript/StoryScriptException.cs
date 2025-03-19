using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StoryScript;

namespace MookStoryScript
{
    /// <summary>
    /// 故事脚本错误
    /// </summary>
    public class StoryScriptError
    {
        public StoryErrorType Type { get; set; }
        public StoryErrorSeverity Severity { get; set; }
        public int Line { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"第{Line}行: {Message} {ErrorCode} \n{Context}";
        }
    }

    /// <summary>
    /// 故事脚本错误类型
    /// </summary>
    public enum StoryErrorType
    {
        /// <summary>
        /// 语法错误
        /// </summary>
        Syntax, // 语法错误
        /// <summary>
        /// 引用错误
        /// </summary>
        Reference, // 引用错误
        /// <summary>
        /// 验证错误
        /// </summary>
        Validation, // 验证错误
        /// <summary>
        /// 解析错误
        /// </summary>
        Parse, // 解析错误
        /// <summary>
        /// 运行时错误
        /// </summary>
        Runtime, // 运行时错误
        /// <summary>
        /// 资源加载错误
        /// </summary>
        ResourceLoad, // 资源加载错误
        /// <summary>
        /// 格式错误
        /// </summary>
        Format, // 格式错误
        /// <summary>
        /// 结构错误
        /// </summary>
        Structure // 结构错误
    }

    /// <summary>
    /// 故事脚本错误严重性
    /// </summary>
    public enum StoryErrorSeverity
    {
        Warning,
        Error,
        Fatal
    }

    public class StoryScriptException
    {
        private readonly Dictionary<string, List<StoryScriptError>> _errors = new();
        private string _sourceName = "未知来源";
        private string[] _scriptLines = Array.Empty<string>();

        public string SourceName => _sourceName;

        public void Initialize(string sourceName, string[] scriptLines)
        {
            _sourceName = sourceName;
            _scriptLines = scriptLines;
        }

        public void AddError(string message, int line, StoryErrorSeverity severity, StoryErrorType type, string? errorCode = null)
        {
            var error = new StoryScriptError
            {
                Line = line,
                Message = message,
                Severity = severity,
                Type = type,
                Context = GetErrorContext(line, _scriptLines),
                ErrorCode = errorCode ?? string.Empty
            };

            if (!_errors.TryGetValue(_sourceName, out var errors))
            {
                errors = new List<StoryScriptError>();
                _errors[_sourceName] = errors;
            }
            errors.Add(error);

            // 根据错误严重程度记录日志
            string logMessage = $"[{_sourceName}] [{Enum.GetName(typeof(StoryErrorSeverity), error.Severity)}] [{Enum.GetName(typeof(StoryErrorType), error.Type)}] {error}";
            switch (severity)
            {
                case StoryErrorSeverity.Warning:
                case StoryErrorSeverity.Error:
                case StoryErrorSeverity.Fatal:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
            Logger.Log(logMessage);
        }

        public Dictionary<string, List<StoryScriptError>> GetErrors()
        {
            return _errors;
        }

        public bool HasFatalError(string sourceName)
        {
            if (!_errors.TryGetValue(sourceName, out var errors))
                return false;
            return errors.Any(e => e.Severity == StoryErrorSeverity.Fatal);
        }

        private static string GetErrorContext(int lineNumber, string[] lines, int contextLines = 2)
        {
            // 行号是从1开始的，需要转换为0基索引
            int actualLineNumber = lineNumber - 1;
            if (actualLineNumber < 0 || actualLineNumber >= lines.Length)
                return string.Empty;

            var contextBuilder = new StringBuilder();
            int startLine = Math.Max(0, actualLineNumber - contextLines);
            int endLine = Math.Min(lines.Length - 1, actualLineNumber + contextLines);

            string linePrefixFormat = "{0,4} | ";
            string errorIndicator = new string(' ', 5) + new string('^', lines[actualLineNumber].Length);

            for (int i = startLine; i <= endLine; i++)
            {
                string linePrefix = string.Format(linePrefixFormat, i + 1);
                contextBuilder.AppendLine(i == actualLineNumber ? $">>> {linePrefix}{lines[i]}" : $"    {linePrefix}{lines[i]}");
                if (i == actualLineNumber)
                {
                    contextBuilder.AppendLine(errorIndicator);
                }
            }

            return contextBuilder.ToString();
        }
    }
}
