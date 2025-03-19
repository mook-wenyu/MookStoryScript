using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using StoryScript;

namespace MookStoryScript
{
    /// <summary>
    /// 对话返回点结构体
    /// </summary>
    public struct ReturnPoint
    {
        public string NodeName { get; set; }
        public int BlockIndex { get; set; }

        public ReturnPoint(string nodeName, int blockIndex)
        {
            NodeName = nodeName;
            BlockIndex = blockIndex;
        }
    }

    public class DialogueManager
    {
        public ExpressionManager ExpressionManagers { get; private set; }
        public VariableManager VariableManagers { get; private set; }
        public FunctionManager FunctionManagers { get; private set; }
        public StoryScriptParser StoryScriptParsers { get; private set; }
        public LocalizationManager LocalizationManagers { get; private set; }

        /// <summary>
        /// 对话加载器
        /// </summary>
        public IDialogueLoader DialogueLoaders { get; private set; }

        /// <summary>
        /// 是否正在执行命令
        /// </summary>
        public bool IsExecuting { get; private set; }

        public string CurrentSectionId { get; set; } = string.Empty;
        public string CurrentNodeName { get; set; } = string.Empty;
        public int CurrentBlockIndex { get; set; }
        public DialogueNode? CurrentNode { get; set; }
        public DialogueBlock? CurrentBlock { get; set; }

        /// <summary>
        /// 对话进度，包含返回点栈和历史记录
        /// </summary>
        public DialogueProgress DialogueProgresses { get; set; }

        // 对话事件
        public event Action? OnDialogueStarted;
        public event Action<DialogueNode>? OnNodeStarted;
        public event Action<DialogueBlock>? OnDialogueUpdated;
        public event Action<CommandType>? OnCommandExecuted;
        public event Action<int>? OnOptionSelected;
        public event Action? OnDialogueCompleted;
        // 本地化事件
        public event Action<string>? OnLanguageChanged;


        public DialogueManager() : this(new DefaultDialogueLoader())
        {
        }

        public DialogueManager(string rootDir) : this(new DefaultDialogueLoader(rootDir))
        {
        }

        public DialogueManager(IDialogueLoader dialogueLoader)
        {
            Logger.Log("Initializing DialogueManager...");
            DialogueLoaders = dialogueLoader;
            ExpressionManagers = new ExpressionManager();
            VariableManagers = new VariableManager(ExpressionManagers);
            LocalizationManagers = new LocalizationManager();
            FunctionManagers = new FunctionManager(ExpressionManagers, this);

            StoryScriptParsers = new StoryScriptParser();

            DialogueProgresses = new DialogueProgress();
        }

        /// <summary>
        /// 注册对话数据
        /// </summary>
        public void RegisterDialogueNode(DialogueNode dialogueNode)
        {
            // 注册对话
            DialogueLoaders.RegisterDialogueNode(dialogueNode);
        }

        /// <summary>
        /// 获取对话数据
        /// </summary>
        public DialogueNode? GetDialogueNode(string nodeName)
        {
            return DialogueLoaders.GetDialogueNode(nodeName);
        }

        /// <summary>
        /// 加载对话进度
        /// </summary>
        public void LoadProgress(DialogueProgress dialogueProgress, Dictionary<string, object> variables)
        {
            DialogueProgresses = dialogueProgress;
            VariableManagers.LoadVariables(variables);
        }

        public DialogueProgress GetProgress()
        {
            return DialogueProgresses;
        }

        public Dictionary<string, object> GetVariables()
        {
            return VariableManagers.GetVariables();
        }

        /// <summary>
        /// 加载本地化文件
        /// </summary>
        /// <param name="language">语言代码</param>
        /// <param name="localizationTexts">本地化文本</param>
        public void LoadLocalization(string language, Dictionary<string, string> localizationTexts)
        {
            LocalizationManagers.LoadLocalization(language, localizationTexts);
        }

        /// <summary>
        /// 切换当前语言
        /// </summary>
        /// <param name="language">语言代码</param>
        public void SwitchLanguage(string language)
        {
            LocalizationManagers.SwitchLanguage(language);
            OnLanguageChanged?.Invoke(language);
        }

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        /// <param name="key">本地化键</param>
        /// <returns>本地化文本</returns>
        public string GetLocalizedText(string key)
        {
            return LocalizationManagers.GetText(key);
        }

        /// <summary>
        /// 同步开始对话
        /// </summary>
        public void Say(string nodeName, int blockIndex = 0)
        {
            _ = SayAsync(nodeName, blockIndex);
        }

        /// <summary>
        /// 同步跳转到当前节点的指定块
        /// </summary>
        public void Say(int blockIndex)
        {
            _ = SayAsync(null, blockIndex);
        }

        /// <summary>
        /// 同步继续对话
        /// </summary>
        public void ContinueSay()
        {
            _ = ContinueSayAsync();
        }

        /// <summary>
        /// 同步执行选择
        /// </summary>
        public void SelectOption(int optionIndex)
        {
            _ = SelectOptionAsync(optionIndex);
        }

        /// <summary>
        /// 结束对话
        /// </summary>
        /// <param name="force">是否强制结束，如果为true则即使在执行命令时也会结束对话</param>
        public void EndSay(bool force = false)
        {
            if (IsExecuting && !force)
            {
                Logger.Log("Executing command, please wait...");
                return;
            }

            if (string.IsNullOrEmpty(CurrentSectionId))
            {
                return;
            }

            CurrentSectionId = string.Empty;
            CurrentNodeName = string.Empty;
            CurrentNode = null;
            CurrentBlockIndex = 0;
            CurrentBlock = null;
            DialogueProgresses.ClearReturnPointStack(); // 清空返回栈

            if (force && IsExecuting)
            {
                IsExecuting = false;
                Logger.Log("Force end dialogue and interrupt command execution");
            }

            OnDialogueCompleted?.Invoke();
        }

        /// <summary>
        /// 异步开始或继续对话
        /// </summary>
        /// <param name="nodeName">节点ID，如果为null则使用当前节点</param>
        /// <param name="blockIndex">块索引</param>
        public async Task SayAsync(string? nodeName = null, int blockIndex = 0)
        {
            if (IsExecuting)
            {
                Logger.Log("Executing command, please wait...");
                return;
            }

            DialogueNode? targetNode;

            if (nodeName == null)
            {
                // 使用当前节点
                if (CurrentNode == null)
                {
                    Logger.Log("No active dialogue node");
                    if (!string.IsNullOrEmpty(CurrentSectionId))
                    {
                        EndSay();
                    }
                    return;
                }
                targetNode = CurrentNode;
            }
            else
            {
                // 使用指定节点
                if (string.IsNullOrEmpty(nodeName))
                {
                    Logger.Log("Dialogue node name cannot be empty");
                    if (!string.IsNullOrEmpty(CurrentSectionId))
                    {
                        EndSay();
                    }
                    return;
                }

                targetNode = DialogueLoaders.GetDialogueNode(nodeName);
                if (targetNode == null)
                {
                    Logger.Log($"Dialogue node not found: {nodeName}");
                    if (!string.IsNullOrEmpty(CurrentSectionId))
                    {
                        EndSay();
                    }
                    return;
                }
            }

            // 验证节点的完整性
            if (targetNode.Blocks.Count == 0)
            {
                Logger.Log($"Dialogue node {nodeName ?? CurrentNodeName} does not contain valid content");
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }

            // 验证块索引的有效性
            if (blockIndex < 0 || blockIndex >= targetNode.Blocks.Count)
            {
                Logger.Log($"Invalid block index: {blockIndex}, node {nodeName ?? CurrentNodeName} has {targetNode.Blocks.Count} blocks");
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }

            // 只有在没有对话时才初始化会话ID
            if (string.IsNullOrEmpty(CurrentSectionId))
            {
                CurrentSectionId = Guid.NewGuid().ToString("N");
                DialogueProgresses.RecordSection(CurrentSectionId);
            }

            // 如果是新节点，更新节点相关信息
            if (nodeName != null)
            {
                CurrentNodeName = nodeName;
                CurrentNode = targetNode;
                OnDialogueStarted?.Invoke();
                ProcessCurrentNode();
            }

            CurrentBlockIndex = blockIndex;
            CurrentBlock = null;

            await ProcessDialogueBlock();
        }

        /// <summary>
        /// 继续对话
        /// </summary>
        public async Task ContinueSayAsync()
        {
            if (IsExecuting)
            {
                Logger.Log("Executing command, please wait...");
                return;
            }

            if (string.IsNullOrEmpty(CurrentSectionId) || CurrentNode == null)
            {
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }

            // 如果当前块有选项，提示用户选择
            if (CurrentBlock?.Options.Count > 0)
            {
                Logger.Log("Please select an option!");
                return;
            }

            // 如果当前块指定了下一个节点，则跳转到该节点（最高优先级）
            if (!string.IsNullOrEmpty(CurrentBlock?.NextNodeName))
            {
                await TransitionToNext(CurrentBlock.NextNodeName);
                return;
            }

            // 检查是否是内部节点的最后一个块
            if (CurrentNode.IsInternal &&
                CurrentBlockIndex >= CurrentNode.Blocks.Count - 1)
            {
                await ReturnToOriginalNode();
                return;
            }

            // 否则继续处理下一个块
            await ProcessDialogueBlock(CurrentBlockIndex + 1);
        }

        /// <summary>
        /// 转换到下一个块或节点
        /// </summary>
        private async Task TransitionToNext(string nextNodeName)
        {
            if (string.IsNullOrEmpty(nextNodeName))
            {
                EndSay();
                return;
            }

            var nextNode = DialogueLoaders.GetDialogueNode(nextNodeName);
            if (nextNode != null)
            {
                // 如果是内部节点，保存当前位置到返回栈
                if (nextNode.IsInternal)
                {
                    DialogueProgresses.PushReturnPoint(CurrentNodeName, CurrentBlockIndex + 1);
                }

                CurrentNodeName = nextNodeName;
                CurrentNode = nextNode;
                CurrentBlockIndex = 0;

                ProcessCurrentNode();
                await ProcessDialogueBlock();
            }
            else
            {
                Logger.Log($"Next dialogue node not found: {nextNodeName}");
                EndSay();
            }
        }

        /// <summary>
        /// 处理当前节点
        /// </summary>
        private void ProcessCurrentNode()
        {
            if (CurrentNode == null)
            {
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }

            // 记录当前节点到对话进度
            DialogueProgresses.RecordNode(CurrentNodeName);

            OnNodeStarted?.Invoke(CurrentNode);
        }

        /// <summary>
        /// 处理对话块
        /// </summary>
        private async Task ProcessDialogueBlock(int? blockIndex = null)
        {
            if (CurrentNode == null)
            {
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }
            var blocks = CurrentNode.Blocks;
            var startIndex = blockIndex ?? CurrentBlockIndex;

            if (startIndex >= blocks.Count)
            {
                // 如果是内部节点的最后一个块，返回到原始节点
                if (CurrentNode.IsInternal)
                {
                    await ReturnToOriginalNode();
                }
                else
                {
                    EndSay();
                }
                return;
            }

            CurrentBlock = blocks[startIndex];
            CurrentBlockIndex = startIndex;

            // 处理条件块
            if (!string.IsNullOrEmpty(CurrentBlock.Condition))
            {
                try
                {
                    // 直接评估条件表达式
                    if (!ExpressionManagers.EvaluateCondition(CurrentBlock.Condition))
                    {
                        // 条件不满足，跳到下一个块
                        await ProcessDialogueBlock(startIndex + 1);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Condition calculation error: {CurrentBlock.Condition}\n{ex.Message}");
                }
            }

            // 处理文本插值
            if (!string.IsNullOrEmpty(CurrentBlock.Text))
            {
                CurrentBlock.Text = ProcessInterpolation(CurrentBlock.Text);
            }

            // 处理选项
            if (CurrentBlock.Options.Count > 0)
            {
                var validOptions = CurrentBlock.Options
                    .Where(choice => string.IsNullOrEmpty(choice.Condition) ||
                                     ExpressionManagers.EvaluateCondition(choice.Condition))
                    .ToList();

                CurrentBlock.Options = validOptions;
            }

            // 执行命令
            try
            {
                await ExecuteBlockCommands(CurrentBlock.Commands);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error executing command: {ex.Message}");
            }

            // 记录当前对话块到对话进度
            DialogueProgresses.RecordBlock(CurrentBlockIndex);

            OnDialogueUpdated?.Invoke(CurrentBlock);
        }

        /// <summary>
        /// 返回原始节点
        /// </summary>
        private async Task ReturnToOriginalNode()
        {
            if (CurrentNode == null || string.IsNullOrEmpty(CurrentNode.ReturnNode))
            {
                EndSay();
                return;
            }

            string returnNodeName = CurrentNode.ReturnNode;
            var returnNode = DialogueLoaders.GetDialogueNode(returnNodeName);
            if (returnNode != null)
            {
                CurrentNodeName = returnNodeName;
                CurrentNode = returnNode;

                // 从返回栈获取返回位置
                if (!DialogueProgresses.IsReturnPointStackEmpty())
                {
                    var returnPoint = DialogueProgresses.PopReturnPoint();
                    // 验证返回栈的正确性
                    if (returnPoint.NodeName == returnNodeName)
                    {
                        CurrentBlockIndex = returnPoint.BlockIndex;
                    }
                    else
                    {
                        // 如果栈信息不匹配，从头开始
                        CurrentBlockIndex = 0;
                        Logger.Log($"Return stack information does not match: expected {returnNodeName}, actual {returnPoint.NodeName}");
                    }
                }
                else
                {
                    // 如果没有栈信息，从头开始
                    CurrentBlockIndex = 0;
                }

                CurrentBlock = null;
                await ProcessDialogueBlock();
            }
            else
            {
                EndSay();
            }
        }

        /// <summary>
        /// 异步执行选择
        /// </summary>
        private async Task SelectOptionAsync(int optionIndex)
        {
            if (IsExecuting)
            {
                Logger.Log("Executing command, please wait...");
                return;
            }

            if (string.IsNullOrEmpty(CurrentSectionId) ||
                CurrentNode == null ||
                CurrentBlockIndex >= CurrentNode.Blocks.Count ||
                optionIndex >= CurrentNode.Blocks[CurrentBlockIndex].Options.Count)
            {
                if (!string.IsNullOrEmpty(CurrentSectionId))
                {
                    EndSay();
                }
                return;
            }

            // 获取选项
            var option = CurrentNode.Blocks[CurrentBlockIndex].Options[optionIndex];

            // 检查选项条件
            if (!string.IsNullOrEmpty(option.Condition))
            {
                try
                {
                    // 如果条件不满足，返回 null
                    if (!ExpressionManagers.EvaluateCondition(option.Condition))
                    {
                        Logger.Log($"Option condition not met: {option.Text} [if {option.Condition}]");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Option condition calculation error: {ex.Message}");
                    return;
                }
            }

            // 处理选项文本插值
            if (!string.IsNullOrEmpty(option.Text))
            {
                option.Text = ProcessInterpolation(option.Text);
            }

            // 处理选项命令
            if (option.Commands.Count > 0)
            {
                try
                {
                    await ExecuteBlockCommands(option.Commands);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error executing option command: {ex.Message}");
                }
            }

            // 记录选项选择到对话进度
            DialogueProgresses.RecordChoice(optionIndex);

            // 触发选项选择事件
            OnOptionSelected?.Invoke(optionIndex);

            // 如果选项指定了下一个节点，则跳转到该节点
            if (!string.IsNullOrEmpty(option.NextNodeName))
            {
                await TransitionToNext(option.NextNodeName);
            }
            else
            {
                // 如果没有指定下一个节点，继续执行下一个对话块
                await ProcessDialogueBlock(CurrentBlockIndex + 1);
            }
        }


        /// <summary>
        /// 检查当前块之后是否还有可执行的块或节点
        /// </summary>
        /// <returns>如果有下一个可执行的块或节点，则返回true；否则返回false</returns>
        public bool HasNextExecutableBlock()
        {
            if (CurrentNode == null || CurrentBlock == null)
            {
                return false;
            }

            // 检查当前块是否指定了下一个节点
            if (!string.IsNullOrEmpty(CurrentBlock.NextNodeName))
            {
                var nextNode = DialogueLoaders.GetDialogueNode(CurrentBlock.NextNodeName);
                if (nextNode is {Blocks: {Count: > 0}})
                {
                    // 检查下一个节点的第一个块是否有符合条件的
                    var firstBlock = nextNode.Blocks[0];
                    if (string.IsNullOrEmpty(firstBlock.Condition) ||
                        ExpressionManagers.EvaluateCondition(firstBlock.Condition))
                    {
                        return true;
                    }

                    // 检查下一个节点是否有其他符合条件的块
                    for (int i = 1; i < nextNode.Blocks.Count; i++)
                    {
                        var block = nextNode.Blocks[i];
                        if (string.IsNullOrEmpty(block.Condition) ||
                            ExpressionManagers.EvaluateCondition(block.Condition))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // 检查当前节点是否为内部节点且是最后一个块
            if (CurrentNode.IsInternal && CurrentBlockIndex >= CurrentNode.Blocks.Count - 1)
            {
                // 检查返回栈是否有内容
                if (!DialogueProgresses.IsReturnPointStackEmpty())
                {
                    // 获取栈顶元素但不移除
                    var returnPoint = DialogueProgresses.PeekReturnPoint();
                    var returnNode = DialogueLoaders.GetDialogueNode(returnPoint.NodeName);

                    // 检查直接返回的节点是否有可执行块
                    if (returnNode != null && returnPoint.BlockIndex < returnNode.Blocks.Count)
                    {
                        var nextBlock = returnNode.Blocks[returnPoint.BlockIndex];
                        if (string.IsNullOrEmpty(nextBlock.Condition) ||
                            ExpressionManagers.EvaluateCondition(nextBlock.Condition))
                        {
                            return true;
                        }

                        // 检查返回节点的后续块
                        for (int i = returnPoint.BlockIndex + 1; i < returnNode.Blocks.Count; i++)
                        {
                            nextBlock = returnNode.Blocks[i];
                            if (string.IsNullOrEmpty(nextBlock.Condition) ||
                                ExpressionManagers.EvaluateCondition(nextBlock.Condition))
                            {
                                return true;
                            }
                        }
                    }

                    // 如果直接返回的节点没有可执行块，且它也是内部节点，则需要检查整个返回栈
                    if (returnNode is {IsInternal: true})
                    {
                        // 复制返回栈以便遍历
                        var stackCopy = DialogueProgresses.GetReturnPointStack();

                        // 移除已检查的栈顶元素
                        stackCopy.Pop();

                        // 递归检查返回栈中的每个节点
                        while (stackCopy.Count > 0)
                        {
                            var nextReturnPoint = stackCopy.Pop();
                            var nextReturnNode = DialogueLoaders.GetDialogueNode(nextReturnPoint.NodeName);

                            if (nextReturnNode != null && nextReturnPoint.BlockIndex < nextReturnNode.Blocks.Count)
                            {
                                // 检查该返回点是否有可执行块
                                var nextReturnBlock = nextReturnNode.Blocks[nextReturnPoint.BlockIndex];
                                if (string.IsNullOrEmpty(nextReturnBlock.Condition) ||
                                    ExpressionManagers.EvaluateCondition(nextReturnBlock.Condition))
                                {
                                    return true;
                                }

                                // 检查该返回节点的后续块
                                for (int i = nextReturnPoint.BlockIndex + 1; i < nextReturnNode.Blocks.Count; i++)
                                {
                                    var block = nextReturnNode.Blocks[i];
                                    if (string.IsNullOrEmpty(block.Condition) ||
                                        ExpressionManagers.EvaluateCondition(block.Condition))
                                    {
                                        return true;
                                    }
                                }

                                // 如果该返回节点不是内部节点，则停止检查
                                if (!nextReturnNode.IsInternal)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                return false;
            }

            // 检查当前节点中是否还有下一个块
            if (CurrentBlockIndex < CurrentNode.Blocks.Count - 1)
            {
                for (int i = CurrentBlockIndex + 1; i < CurrentNode.Blocks.Count; i++)
                {
                    var nextBlock = CurrentNode.Blocks[i];
                    if (string.IsNullOrEmpty(nextBlock.Condition) ||
                        ExpressionManagers.EvaluateCondition(nextBlock.Condition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// <summary>
        /// 执行块命令
        /// </summary>
        private async Task ExecuteBlockCommands(List<CommandData> commands)
        {
            if (commands.Count == 0) return;
            if (IsExecuting) return;

            IsExecuting = true;

            try
            {
                foreach (var command in commands)
                {
                    await ExecuteCommand(command);
                }
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 执行单个命令
        /// </summary>
        private async Task ExecuteCommand(CommandData command)
        {
            try
            {
                switch (command.CommandType)
                {
                    case CommandType.Var:
                    {
                        // 解析变量声明
                        var varMatch = ScriptPatterns.AssignmentPattern.Match(command.CsExpression);
                        if (!varMatch.Success)
                        {
                            Logger.Log($"Invalid variable declaration: {command.CsExpression}");
                            return;
                        }

                        string varName = varMatch.Groups[1].Value;
                        string varValue = varMatch.Groups[2].Value;

                        // 检查变量是否已存在
                        if (VariableManagers.Get<object>(varName) != null)
                        {
                            return;
                        }

                        // 计算初始值并声明变量
                        var value = ExpressionManagers.Evaluate(varValue);
                        if (value != null) VariableManagers.Set(varName, value);
                        // 触发事件
                        OnCommandExecuted?.Invoke(command.CommandType);
                        break;
                    }
                    case CommandType.Set:
                    {
                        // 解析赋值语句
                        var setMatch = ScriptPatterns.AssignmentPattern.Match(command.CsExpression);
                        if (!setMatch.Success)
                        {
                            Logger.Log($"Invalid assignment statement: {command.CsExpression}");
                            return;
                        }

                        string varName = setMatch.Groups[1].Value;
                        string setExpression = setMatch.Groups[2].Value;

                        // 计算表达式并设置变量值
                        object? value = ExpressionManagers.Evaluate(setExpression);
                        if (value != null)
                        {
                            VariableManagers.Set(varName, value);
                            // 触发事件
                            OnCommandExecuted?.Invoke(command.CommandType);
                        }
                        break;
                    }
                    case CommandType.Add:
                    case CommandType.Sub:
                        var opMatch = ScriptPatterns.AssignmentPattern.Match(command.CsExpression);
                        if (!opMatch.Success)
                        {
                            Logger.Log($"Invalid assignment statement: {command.CsExpression}");
                            return;
                        }

                        string variableName = opMatch.Groups[1].Value;
                        string expression = opMatch.Groups[2].Value;

                        // 计算表达式
                        object? result = ExpressionManagers.Evaluate(expression);
                        // 使用 VariableManagers 设置变量
                        if (result != null)
                        {
                            VariableManagers.Set(variableName, result);
                            // 触发事件
                            OnCommandExecuted?.Invoke(command.CommandType);
                        }
                        break;

                    case CommandType.Wait:
                        object? waitResult = ExpressionManagers.Evaluate(command.CsExpression);
                        float waitTime = float.Parse(waitResult!.ToString()!);
                        if (waitTime > 0)
                        {
                            // 触发事件
                            OnCommandExecuted?.Invoke(command.CommandType);
                            await Task.Delay(TimeSpan.FromSeconds(waitTime));
                        }
                        else
                        {
                            Logger.Log($"Wait time must be a numeric type: {command.CsExpression}");
                        }
                        break;

                    case CommandType.Call:
                        // 函数调用，直接执行表达式
                        ExpressionManagers.Evaluate(command.CsExpression);
                        // 触发事件
                        OnCommandExecuted?.Invoke(command.CommandType);
                        break;

                    default:
                        Logger.Log($"Unknown command type: {command.CommandType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error executing command: {command.CsExpression}\n{ex.Message}");
            }
        }

        /// <summary>
        /// 处理文本插值
        /// </summary>
        private string ProcessInterpolation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 处理整行替换的本地化文本（以#开头的键）
            if (text.StartsWith("#"))
            {
                string key = text.Substring(1).Trim();
                return LocalizationManagers.GetText(key);
            }

            // 处理插值表达式
            return ScriptPatterns.InterpolationPattern.Replace(text, match =>
            {
                string expression = match.Groups["expression"].Value.Trim();
                try
                {
                    // 处理本地化函数调用 l("key")
                    if (expression.StartsWith("l(") && expression.EndsWith(")"))
                    {
                        string key = expression.Substring(2, expression.Length - 3).Trim();
                        // 去除可能的引号
                        if ((key.StartsWith("\"") && key.EndsWith("\"")) ||
                            (key.StartsWith("'") && key.EndsWith("'")))
                        {
                            key = key.Substring(1, key.Length - 2);
                        }
                        // 此时 key 可能是没有引号的变量名或直接的键名
                        return LocalizationManagers.GetText(key);
                    }
                    else if (expression.Contains("("))
                    {
                        // 处理其他函数调用
                        var result = ExpressionManagers.Evaluate(expression);
                        return result?.ToString() ?? expression;
                    }
                    else
                    {
                        // 处理变量
                        object? value = VariableManagers.Get<object>(expression);
                        return value?.ToString() ?? expression;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to process interpolation expression: {expression}\n{ex.Message}");
                    return $"[Error: {expression}]";
                }
            });
        }


        /// <summary>
        /// 自动收集已加载对话节点中的本地化文本
        /// </summary>
        /// <param name="language">目标语言</param>
        /// <param name="filePath">文件路径，如果为null则使用默认路径</param>
        public Dictionary<string, string> CollectAndSaveLocalizationTextsFromLoadedNodes(string language, string? filePath = null)
        {
            CollectLocalizationTextsFromLoadedNodes(language);
            return LocalizationManagers.GetLocalizationTexts(language);
        }

        /// <summary>
        /// 自动收集已加载对话节点中的本地化文本并添加到指定语言
        /// </summary>
        /// <param name="language">目标语言</param>
        public void CollectLocalizationTextsFromLoadedNodes(string language)
        {
            LocalizationManagers.CollectLocalizationTextsFromNodes(DialogueLoaders.DialogueNodes, language);
        }

    }
}
