using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MookStoryScript
{
    /// <summary>
    /// 脚本模式
    /// </summary>
    public static class ScriptPatterns
    {
        /// <summary>
        /// 赋值模式
        /// </summary>
        public static readonly Regex AssignmentPattern = new(@"^([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(.+)$", RegexOptions.Compiled);

        /// <summary>
        /// 插值表达式正则表达式
        /// </summary>
        public static readonly Regex InterpolationPattern = new(@"\{\s*(?<expression>.*?)\s*\}", RegexOptions.Compiled);

        /// <summary>
        /// 本地化键正则表达式
        /// </summary>
        public static readonly Regex LocalizationKeyPattern = new(@"\{\s*l\(\s*(?:[""](?<key1>.*?)[""]|[''](?<key2>.*?)['']|(?<key3>[^)""']+))\s*\)\s*\}", RegexOptions.Compiled);

        /// <summary>
        /// 标点符号正则表达式
        /// </summary>
        public static readonly Regex PunctuationPattern = new(@"[\p{P}\s]", RegexOptions.Compiled);

    }

    /// <summary>
    /// 基于Span的解析器
    /// </summary>
    public ref struct SpanBasedParser
    {
        private readonly ReadOnlySpan<char> _line;

        public int Position { get; private set; }

        public SpanBasedParser(ReadOnlySpan<char> line)
        {
            _line = line;
            Position = 0;
        }

        /// <summary>
        /// 跳过空白字符
        /// </summary>
        /// <returns></returns>
        public bool SkipWhitespace()
        {
            while (Position < _line.Length && char.IsWhiteSpace(_line[Position]))
            {
                Position++;
            }
            return Position < _line.Length;
        }

        /// <summary>
        /// 判断是否以指定字符串开头
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool StartsWith(string value)
        {
            if (Position + value.Length > _line.Length) return false;
            return _line.Slice(Position, value.Length).SequenceEqual(value.AsSpan());
        }

        /// <summary>
        /// 读取直到指定字符
        /// </summary>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public ReadOnlySpan<char> ReadUntil(char delimiter)
        {
            int start = Position;
            while (Position < _line.Length && _line[Position] != delimiter)
            {
                Position++;
            }
            return _line.Slice(start, Position - start);
        }

        /// <summary>
        /// 读取直到指定字符串
        /// </summary>
        /// <param name="delimiters"></param>
        /// <returns></returns>
        public ReadOnlySpan<char> ReadUntilAny(ReadOnlySpan<char> delimiters)
        {
            int start = Position;
            while (Position < _line.Length && delimiters.IndexOf(_line[Position]) < 0)
            {
                Position++;
            }
            return _line.Slice(start, Position - start);
        }

        /// <summary>
        /// 尝试读取指定字符
        /// </summary>
        /// <param name="expected"></param>
        /// <returns></returns>
        public bool TryReadChar(char expected)
        {
            if (Position < _line.Length && _line[Position] == expected)
            {
                Position++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取剩余字符串
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<char> Rest()
        {
            return _line.Slice(Position);
        }

        /// <summary>
        /// 前进指定数量字符
        /// </summary>
        /// <param name="count"></param>
        public void Advance(int count = 1)
        {
            Position = Math.Min(Position + count, _line.Length);
        }

        /// <summary>
        /// 尝试查看下一个字符
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool TryPeek(out char c)
        {
            if (Position < _line.Length)
            {
                c = _line[Position];
                return true;
            }
            c = default;
            return false;
        }
    }

    /// <summary>
    /// 对话栈项目
    /// </summary>
    public readonly struct DialogueStackItem
    {
        private readonly object _item;
        public int Level { get; }
        public DialogueItemType ItemType { get; }

        public DialogueNode AsNode => ItemType == DialogueItemType.Node ? (DialogueNode)_item : throw new InvalidOperationException("这不是一个节点");
        public DialogueBlock AsBlock => ItemType == DialogueItemType.Block ? (DialogueBlock)_item : throw new InvalidOperationException("这不是一个对话块");
        public DialogueOption AsOption => ItemType == DialogueItemType.Option ? (DialogueOption)_item : throw new InvalidOperationException("这不是一个选项");

        public DialogueStackItem(DialogueNode node, int level)
        {
            _item = node;
            Level = level;
            ItemType = DialogueItemType.Node;
        }

        public DialogueStackItem(DialogueBlock block, int level)
        {
            _item = block;
            Level = level;
            ItemType = DialogueItemType.Block;
        }

        public DialogueStackItem(DialogueOption option, int level)
        {
            _item = option;
            Level = level;
            ItemType = DialogueItemType.Option;
        }

        public void Deconstruct(out object item, out int level)
        {
            item = _item;
            level = Level;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DialogueStackItem other)
                return false;

            return Level == other.Level &&
                   ItemType == other.ItemType &&
                   _item == other._item;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Level, ItemType, _item);
        }
    }

    /// <summary>
    /// 解析上下文
    /// </summary>
    public class ParsingContext
    {
        public int CurrentIndentLevel { get; set; }
        public Stack<DialogueStackItem> DialogueStack { get; } = new();

        /// <summary>
        /// 查找父级元素
        /// </summary>
        /// <param name="currentIndentLevel">当前缩进级别</param>
        /// <param name="types">允许的类型枚举列表</param>
        /// <returns>父级元素</returns>
        /// <exception cref="InvalidOperationException">找不到合适的父级元素</exception>
        public DialogueStackItem? FindParent(int currentIndentLevel, params DialogueItemType[] types)
        {
            // 先丢弃所有比当前缩进级别大的项
            while (DialogueStack.Count > 0 && DialogueStack.Peek().Level > currentIndentLevel)
            {
                DialogueStack.Pop();
            }

            // 临时存储栈，用于保存遍历过的项
            var tempStack = new Stack<DialogueStackItem>();
            DialogueStackItem? parent = null;

            // 查找合适的父级
            while (DialogueStack.Count > 0)
            {
                var stackItem = DialogueStack.Pop();
                tempStack.Push(stackItem);

                // 如果当前项的缩进级别小于等于当前级别，并且是允许的类型之一
                if (stackItem.Level <= currentIndentLevel && types.Contains(stackItem.ItemType))
                {
                    parent = stackItem;
                    break;
                }
            }

            // 如果找到了父级，恢复栈到父级位置（包括父级）
            if (parent != null)
            {
                while (tempStack.Count > 0)
                {
                    var item = tempStack.Pop();
                    DialogueStack.Push(item);

                    // 检查当前项是否是我们找到的父级
                    if (item.Equals(parent))
                    {
                        return parent;
                    }
                }
            }

            // 如果没找到父级，返回 null
            return null;
        }

        /// <summary>
        /// 查找父级节点名称
        /// </summary>
        public string FindParentNodeName(int currentIndentLevel)
        {
            // 临时存储栈，用于保存遍历过的项
            var tempStack = new Stack<DialogueStackItem>();
            string? parentNodeName = null;

            // 遍历栈查找父级节点
            while (DialogueStack.Count > 0)
            {
                var stackItem = DialogueStack.Pop();
                tempStack.Push(stackItem);

                // 如果当前项的缩进级别小于当前级别，检查是否是节点
                if (stackItem.Level < currentIndentLevel && stackItem.ItemType == DialogueItemType.Node)
                {
                    var node = stackItem.AsNode;
                    if (node != null)
                    {
                        parentNodeName = node.Name;
                        break;
                    }
                }
            }

            // 恢复栈
            while (tempStack.Count > 0)
            {
                DialogueStack.Push(tempStack.Pop());
            }

            return parentNodeName ?? string.Empty;
        }
    }

    /// <summary>
    /// 条件层
    /// </summary>
    internal class ConditionLayer
    {
        public List<string> Conditions { get; } = new();
        public bool HasElse { get; set; }
        public int ConditionLevel { get; set; } // 记录条件层级
    }

}
