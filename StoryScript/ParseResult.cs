using System;

namespace MookStoryScript
{
    /// <summary>
    /// 节点解析结果
    /// </summary>
    public readonly ref struct NodeParseResult
    {
        public ReadOnlySpan<char> NodeId { get; }
        public bool IsValid { get; }

        public NodeParseResult(ReadOnlySpan<char> nodeId, bool isValid)
        {
            NodeId = nodeId;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// 空块解析结果
    /// </summary>
    public readonly struct EmptyBlockParseResult
    {
        public bool IsValid { get; }

        public EmptyBlockParseResult(bool isValid)
        {
            IsValid = isValid;
        }
    }

    /// <summary>
    /// 对话解析结果
    /// </summary>
    public readonly ref struct DialogueParseResult
    {
        public bool IsValid { get; }
        public ReadOnlySpan<char> Speaker { get; }
        public ReadOnlySpan<char> Emotion { get; }
        public ReadOnlySpan<char> Content { get; }

        public DialogueParseResult(bool isValid, ReadOnlySpan<char> speaker, ReadOnlySpan<char> emotion, ReadOnlySpan<char> content)
        {
            IsValid = isValid;
            Speaker = speaker;
            Emotion = emotion;
            Content = content;
        }
    }

    /// <summary>
    /// 选项解析结果
    /// </summary>
    public readonly ref struct ChoiceParseResult
    {
        public ReadOnlySpan<char> Text { get; }
        public ReadOnlySpan<char> Condition { get; }
        public bool IsValid { get; }

        public ChoiceParseResult(ReadOnlySpan<char> text, ReadOnlySpan<char> condition, bool isValid)
        {
            Text = text;
            Condition = condition;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// 条件解析结果
    /// </summary>
    public readonly ref struct ConditionParseResult
    {
        public ConditionType Type { get; }
        public ReadOnlySpan<char> Condition { get; }
        public bool IsValid { get; }

        public ConditionParseResult(ConditionType type, ReadOnlySpan<char> condition, bool isValid)
        {
            Type = type;
            Condition = condition;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// 跳转解析结果
    /// </summary>
    public readonly ref struct JumpParseResult
    {
        public ReadOnlySpan<char> Target { get; }
        public bool IsValid { get; }

        public JumpParseResult(ReadOnlySpan<char> target, bool isValid)
        {
            Target = target;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// 命令解析结果
    /// </summary>
    public readonly ref struct CommandParseResult
    {
        public CommandType Type { get; }
        public ReadOnlySpan<char> Content { get; }
        public bool IsValid { get; }

        public CommandParseResult(CommandType type, ReadOnlySpan<char> content, bool isValid)
        {
            Type = type;
            Content = content;
            IsValid = isValid;
        }
    }

}
