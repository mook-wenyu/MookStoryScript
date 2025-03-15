using System;

namespace MookStoryScript
{
    /// <summary>
    /// 命令类型枚举
    /// </summary>
    [Serializable]
    public enum CommandType
    {
        Var,
        Set,
        Add,
        Sub,
        Call,
        Wait,
    }

    /// <summary>
    /// 条件类型枚举
    /// </summary>
    [Serializable]
    public enum ConditionType
    {
        If,
        Elif,
        Else,
        Endif
    }

    /// <summary>
    /// 对话栈项目类型枚举
    /// </summary>
    [Serializable]
    public enum DialogueItemType
    {
        Node,
        Block,
        Option
    }


}
