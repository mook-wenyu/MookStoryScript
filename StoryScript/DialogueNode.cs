using System;
using System.Collections.Generic;

namespace MookStoryScript
{

    /// <summary>
    /// 命令数据
    /// </summary>
    [Serializable]
    public class CommandData
    {
        // 命令类型
        public CommandType CommandType { get; set; }

        // C#表达式，用于实际执行
        public string CsExpression { get; set; } = string.Empty;
    }

    /// <summary>
    /// 空块基类
    /// </summary>
    [Serializable]
    public abstract class EmptyBlockBase
    {
        public string Condition { get; set; } = string.Empty;
        public string NextNodeName { get; set; } = string.Empty;
        public List<CommandData> Commands { get; set; } = new();

        public virtual bool IsEmpty()
        {
            return string.IsNullOrEmpty(Condition) &&
                   string.IsNullOrEmpty(NextNodeName) &&
                   Commands.Count == 0;
        }
    }

    /// <summary>
    /// 对话节点
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string Name { get; set; } = string.Empty;
        public bool IsInternal { get; set; } // 标记是否是内部生成的节点
        public string? ReturnNode { get; set; } // 需要返回的原始节点
        public List<DialogueBlock> Blocks { get; set; } = new();
    }

    /// <summary>
    /// 对话块
    /// </summary>
    [Serializable]
    public class DialogueBlock : EmptyBlockBase
    {
        public string Speaker { get; set; } = string.Empty;
        public string Emotion { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<DialogueOption> Options { get; set; } = new();

        public override bool IsEmpty()
        {
            return base.IsEmpty() &&
                   string.IsNullOrEmpty(Speaker) &&
                   string.IsNullOrEmpty(Emotion) &&
                   string.IsNullOrEmpty(Text) &&
                   Options.Count == 0;
        }
    }

    /// <summary>
    /// 对话选项
    /// </summary>
    [Serializable]
    public class DialogueOption : EmptyBlockBase
    {
        public string Text { get; set; } = string.Empty;

        public override bool IsEmpty()
        {
            return base.IsEmpty() && string.IsNullOrEmpty(Text);
        }
    }
}
