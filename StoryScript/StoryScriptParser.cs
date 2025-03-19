using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using StoryScript;

namespace MookStoryScript
{

    public class StoryScriptParser
    {
        private Dictionary<string, DialogueNode> _nodes;
        private int _currentLineNumber;
        private string _currentLine;
        private DialogueNode? _currentNode;
        private Dictionary<int, int> _lineNumberMap = new(); // 行号映射
        private readonly StoryScriptException _storyScriptException;

        private readonly Stack<ConditionLayer> _conditionLayers = new(); // 条件层
        private readonly ParsingContext _parsingContext = new();         // 解析上下文
        private const int INDENT_SIZE = 4;                               // 定义缩进大小

        public StoryScriptParser()
        {
            Logger.Log("Initializing StoryScriptParser...");
            _storyScriptException = new StoryScriptException();
            _nodes = new Dictionary<string, DialogueNode>();
            _currentLineNumber = 0;
            _currentLine = string.Empty;
        }

        private int IsEmptyOrComment(ReadOnlySpan<char> line)
        {
            int i = 0;
            // 跳过前导空白
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            // 空行检查
            if (i >= line.Length)
                return 1; // 返回1表示空行

            // 注释检查
            if (i < line.Length - 1 && line[i] == '/' && line[i + 1] == '/')
                return 2; // 返回2表示注释行

            return 0; // 返回0表示非空行非注释行
        }

        public Dictionary<string, DialogueNode> ParseScript(string scriptContent, string sourceName = "Unknown source")
        {
            try
            {
                // 初始化解析状态
                _nodes = new Dictionary<string, DialogueNode>();
                _lineNumberMap = new Dictionary<int, int>();
                _storyScriptException.Initialize(sourceName, scriptContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));

                var processedLines = new List<string>();
                var stringBuilder = new StringBuilder(capacity: (int)(scriptContent.Length * 1.5));
                int originalLineNumber = 0;
                int processedLineNumber = 0;

                // 分割原始内容为行
                ReadOnlySpan<char> contentSpan = scriptContent.AsSpan();
                int start = 0;
                bool inString = false;
                char stringChar = '\0';

                // 第一层循环：处理字符串内容，分割成语句
                for (int i = 0; i < contentSpan.Length; i++)
                {
                    char c = contentSpan[i];

                    // 处理字符串
                    if (c is '"' or '\'')
                    {
                        // 检查是否是转义字符
                        if (i > 0 && contentSpan[i - 1] == '\\')
                        {
                            continue; // 跳过转义字符的处理
                        }

                        if (!inString)
                        {
                            inString = true;
                            stringChar = c;
                        }
                        else if (c == stringChar)
                        {
                            inString = false;
                            stringChar = '\0';
                        }
                    }

                    // 检测行结束或分号
                    if ((c is '\n' or '\r') || (c is ';' or '；' && !inString))
                    {
                        // 如果是换行符，检查字符串是否闭合
                        if (c is '\n' or '\r')
                        {
                            // 处理 \r\n
                            if (c == '\r' && i + 1 < contentSpan.Length && contentSpan[i + 1] == '\n')
                            {
                                i++;
                            }
                            originalLineNumber++;

                            // 如果字符串未闭合，添加错误并重置字符串状态
                            if (inString)
                            {
                                inString = false;
                                stringChar = '\0';
                                _storyScriptException.AddError($"字符串未闭合", originalLineNumber,
                                    StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNCLOSED_STRING");
                            }
                        }

                        // 处理当前语句
                        var statement = contentSpan[start..i];
                        ProcessLine(statement, stringBuilder, processedLines, originalLineNumber, ref processedLineNumber);
                        start = i + 1;
                    }
                }

                // 处理最后一个语句
                if (start < contentSpan.Length)
                {
                    var lastStatement = contentSpan[start..];
                    ProcessLine(lastStatement, stringBuilder, processedLines, originalLineNumber, ref processedLineNumber);
                }

                // 设置处理后的行
                _currentLineNumber = 0;

                // 第二层循环：解析每个语句
                while (_currentLineNumber < processedLines.Count)
                {
                    _currentLine = processedLines[_currentLineNumber];

                    // 解析各种类型的语句
                    var line = _currentLine.AsSpan();
                    UpdateIndentationLevel(line);

                    // 按优先级依次尝试解析
                    if (!TryParseLine(line))
                    {
                        _storyScriptException.AddError($"无法识别的行内容: {_currentLine}", _lineNumberMap[_currentLineNumber],
                            StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNRECOGNIZED_LINE");
                    }

                    _currentLineNumber++;
                }

                // 保存最后一个节点
                SaveCurrentNode();
            }
            catch (Exception ex)
            {
                _storyScriptException.AddError($"解析脚本时发生致命错误: {ex.Message}", _lineNumberMap[_currentLineNumber],
                    StoryErrorSeverity.Fatal, StoryErrorType.Parse, "FATAL_ERROR");
            }
            finally
            {
                // 检查未闭合的条件语句
                if (_conditionLayers.Count > 0)
                {
                    _storyScriptException.AddError($"存在未闭合的if语句（缺少{_conditionLayers.Count}个endif）",
                        _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNCLOSED_IF");
                }
            }

            return _nodes;
        }

        private void ProcessLine(ReadOnlySpan<char> line, StringBuilder stringBuilder, List<string> processedLines,
            int originalLineNumber, ref int processedLineNumber)
        {
            // 检查是否是空行（包括空行、注释行、只有空白字符的行）
            int isEmptyLine = IsEmptyOrComment(line);
            switch (isEmptyLine)
            {
                // 如果是空行，直接添加一个空行（缩进为0）
                // 检查前一行是否也是空行，如果是则跳过
                case 1 when processedLines.Count > 0 && string.IsNullOrWhiteSpace(processedLines[^1]):
                    return;
                case 1:
                    _lineNumberMap[processedLineNumber] = originalLineNumber;
                    processedLines.Add(string.Empty);
                    processedLineNumber++;
                    return;
                // 如果是注释行，直接忽略
                case 2:
                    return;
            }

            // 获取并规范化缩进
            int indentCount = 0;
            int contentStart = 0;
            for (; contentStart < line.Length; contentStart++)
            {
                if (!char.IsWhiteSpace(line[contentStart])) break;
                if (line[contentStart] == ' ') indentCount++;
            }

            // 规范化缩进
            int normalizedIndent = indentCount == 0 ? 0 : ((indentCount - 1) / INDENT_SIZE + 1) * INDENT_SIZE;

            // 处理实际内容
            var content = line[contentStart..].Trim();
            if (!content.IsEmpty)
            {
                stringBuilder.Clear();
                stringBuilder.Append(' ', normalizedIndent);
                stringBuilder.Append(content);

                string processedLine = stringBuilder.ToString();
                _lineNumberMap[processedLineNumber] = originalLineNumber;
                processedLines.Add(processedLine);
                processedLineNumber++;
            }
        }

        private bool TryParseLine(ReadOnlySpan<char> line)
        {
            // 按优先级依次尝试解析

            var emptyBlockResult = ParseEmptyBlock(line);
            if (emptyBlockResult.IsValid)
            {
                ProcessEmptyBlock(emptyBlockResult);
                return true;
            }

            var nodeResult = ParseNode(line);
            if (nodeResult.IsValid)
            {
                ProcessNodeHeader(nodeResult);
                return true;
            }

            var choiceResult = ParseChoice(line);
            if (choiceResult.IsValid)
            {
                ProcessChoice(choiceResult);
                return true;
            }

            var conditionResult = ParseCondition(line);
            if (conditionResult.IsValid)
            {
                ProcessConditionBlock(conditionResult);
                return true;
            }

            var jumpResult = ParseJump(line);
            if (jumpResult.IsValid)
            {
                ProcessJump(jumpResult);
                return true;
            }

            var commandResult = ParseCommand(line);
            if (commandResult.IsValid)
            {
                ProcessCommand(commandResult);
                return true;
            }

            // 如果以上都不是，则作为对话处理
            var dialogueResult = ParseDialogue(line);
            if (dialogueResult.IsValid)
            {
                ProcessDialogue(dialogueResult);
                return true;
            }

            return false;
        }

        // 保存当前节点
        private void SaveCurrentNode()
        {
            if (_currentNode != null)
            {
                _nodes[_currentNode.Name] = _currentNode;
            }
        }

        private void UpdateIndentationLevel(ReadOnlySpan<char> line)
        {
            int indentLevel = 0;
            foreach (char c in line)
            {
                if (c == ' ')
                {
                    indentLevel++;
                }
                else
                {
                    break;
                }
            }
            _parsingContext.CurrentIndentLevel = indentLevel / INDENT_SIZE;
        }

        private static EmptyBlockParseResult ParseEmptyBlock(ReadOnlySpan<char> line)
        {
            // 检查是否只包含空白字符
            bool isEmpty = true;
            foreach (char t in line)
            {
                if (!char.IsWhiteSpace(t))
                {
                    isEmpty = false;
                    break;
                }
            }
            return new EmptyBlockParseResult(isEmpty);
        }

        private void ProcessEmptyBlock(EmptyBlockParseResult result)
        {
            if (!result.IsValid) return;

            // 检查是否为空栈（空行不能是第一个）
            if (_parsingContext.DialogueStack.Count == 0)
            {
                return;
            }

            // 检查栈顶是否为节点（空行不能跟在节点后面）
            if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Node)
            {
                return;
            }

            // 检查栈顶是否为空行，如果是则直接返回
            if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
            {
                return;
            }

            // 空块只作为分隔符，直接添加到DialogueStack
            var emptyBlock = new DialogueBlock();
            _parsingContext.DialogueStack.Push(new DialogueStackItem(emptyBlock, 0));
        }

        private static NodeParseResult ParseNode(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);
            parser.SkipWhitespace();

            // 检查节点标识符（:: 或 ：：）
            if (!parser.StartsWith("::") && !parser.StartsWith("：："))
            {
                return new NodeParseResult(default, false);
            }
            parser.Advance(2);
            parser.SkipWhitespace();

            // 读取整行内容作为节点ID
            var nodeId = parser.Rest().Trim();
            if (nodeId.IsEmpty)
            {
                return new NodeParseResult(default, false);
            }

            return new NodeParseResult(nodeId, true);
        }

        private void ProcessNodeHeader(NodeParseResult result)
        {
            // 检查是否为空栈（空行不能出现在节点定义之前）
            if (_parsingContext.DialogueStack.Count > 0)
            {
                // 检查最后一个元素是否为空行，如果是则删除
                if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                    _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
                {
                    _parsingContext.DialogueStack.Pop();
                }
            }

            string nodeName = result.NodeId.ToString();

            var newNode = new DialogueNode
            {
                Name = nodeName,
                Blocks = new List<DialogueBlock>()
            };

            if (!newNode.IsInternal)
            {
                // 清空栈并将新节点入栈
                _parsingContext.DialogueStack.Clear();
                _parsingContext.DialogueStack.Push(new DialogueStackItem(newNode, 0));
            }
            else
            {
                // 内部节点直接入栈
                _parsingContext.DialogueStack.Push(new DialogueStackItem(newNode, _parsingContext.CurrentIndentLevel));
            }

            _nodes[nodeName] = newNode;
            _currentNode = newNode;
        }

        private static ConditionParseResult ParseCondition(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);
            parser.SkipWhitespace();

            if (parser.StartsWith("if "))
            {
                parser.Advance(3); // 跳过 "if "
                parser.SkipWhitespace();
                return new ConditionParseResult(ConditionType.If, parser.Rest(), true);
            }
            else if (parser.StartsWith("elif "))
            {
                parser.Advance(5); // 跳过 "elif "
                parser.SkipWhitespace();
                return new ConditionParseResult(ConditionType.Elif, parser.Rest(), true);
            }
            else if (parser.StartsWith("else"))
            {
                return new ConditionParseResult(ConditionType.Else, default, true);
            }
            else if (parser.StartsWith("endif"))
            {
                return new ConditionParseResult(ConditionType.Endif, default, true);
            }

            return new ConditionParseResult(default, default, false);
        }

        private void ProcessConditionBlock(ConditionParseResult result)
        {
            switch (result.Type)
            {
                case ConditionType.Endif:
                    if (_conditionLayers.Count == 0)
                    {
                        _storyScriptException.AddError("多余的endif语句", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNMATCHED_ENDIF");
                        return;
                    }
                    _conditionLayers.Pop();
                    break;

                case ConditionType.If:
                    if (_currentNode == null)
                    {
                        _storyScriptException.AddError("条件语句必须在节点内部", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "CONDITION_OUTSIDE_NODE");
                        return;
                    }
                    var newLayer = new ConditionLayer();
                    newLayer.Conditions.Add(PreprocessExpression(result.Condition.ToString(), true));
                    newLayer.ConditionLevel = _parsingContext.CurrentIndentLevel; // 记录条件层级为当前层级
                    _conditionLayers.Push(newLayer);
                    break;

                case ConditionType.Elif:
                    if (_conditionLayers.Count == 0)
                    {
                        _storyScriptException.AddError("elif语句缺少对应的if", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNMATCHED_ELIF");
                        return;
                    }
                    var currentLayer = _conditionLayers.Peek();
                    if (currentLayer.Conditions.Count == 0)
                    {
                        _storyScriptException.AddError("elif必须跟在if或其他elif之后", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "ELIF_WITHOUT_IF");
                        return;
                    }
                    currentLayer.Conditions.Add(PreprocessExpression(result.Condition.ToString(), true));
                    // 对于elif，保持与if相同的作用层级
                    break;

                case ConditionType.Else:
                    if (_conditionLayers.Count == 0)
                    {
                        _storyScriptException.AddError("else语句缺少对应的if", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "UNMATCHED_ELSE");
                        return;
                    }
                    var layer = _conditionLayers.Peek();
                    if (layer.Conditions.Count == 0)
                    {
                        _storyScriptException.AddError("else必须跟在if/elif之后", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "ELSE_WITHOUT_IF");
                        return;
                    }
                    if (layer.HasElse)
                    {
                        _storyScriptException.AddError("重复的else语句", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Syntax, "DUPLICATE_ELSE");
                        return;
                    }
                    layer.HasElse = true;
                    // 对于else，保持与if相同的作用层级
                    break;
            }
        }

        private static DialogueParseResult ParseDialogue(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);

            // 跳过前导空白
            parser.SkipWhitespace();

            // 读取说话者名称（直到遇到非字符串内的冒号或方括号）
            int start = parser.Position;
            bool inString = false;
            char stringChar = '\0';

            while (parser.TryPeek(out char c))
            {
                // 处理字符串
                if (c is '"' or '\'')
                {
                    // 检查是否是转义字符
                    if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                    {
                        parser.Advance();
                        continue; // 跳过转义字符的处理
                    }

                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                }

                // 如果不在字符串中，检查是否是分隔符
                if (!inString && (c == '[' || c == ':' || c == '：'))
                {
                    break;
                }

                parser.Advance();
            }

            ReadOnlySpan<char> speaker = line.Slice(start, parser.Position - start).Trim();

            // 如果没有找到说话者，说明是普通对话
            if (speaker.IsEmpty)
            {
                return new DialogueParseResult(true, default, default, line.Trim());
            }

            // 检查是否有表情标记
            ReadOnlySpan<char> emotion = default;
            if (parser.TryPeek(out var nextChar) && nextChar == '[')
            {
                parser.Advance();        // 跳过 '['
                parser.SkipWhitespace(); // 跳过[后的空格
                start = parser.Position;

                // 读取表情内容（直到遇到非字符串内的]）
                inString = false;
                stringChar = '\0';

                while (parser.TryPeek(out char c))
                {
                    // 处理字符串
                    if (c is '"' or '\'')
                    {
                        // 检查是否是转义字符
                        if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                        {
                            parser.Advance();
                            continue; // 跳过转义字符的处理
                        }

                        if (!inString)
                        {
                            inString = true;
                            stringChar = c;
                        }
                        else if (c == stringChar)
                        {
                            inString = false;
                        }
                    }

                    // 如果不在字符串中且遇到]，则结束表情
                    if (!inString && c == ']')
                    {
                        break;
                    }

                    parser.Advance();
                }

                emotion = line.Slice(start, parser.Position - start).Trim();
                if (!parser.TryReadChar(']'))
                {
                    // 如果表情标记解析失败，将整行作为普通对话
                    return new DialogueParseResult(true, default, default, line.Trim());
                }
                parser.SkipWhitespace();
            }

            // 检查并跳过冒号
            if (!parser.TryReadChar(':') && !parser.TryReadChar('：'))
            {
                // 如果没有冒号，将整行作为普通对话
                return new DialogueParseResult(true, default, default, line.Trim());
            }

            // 读取对话内容
            parser.SkipWhitespace();
            ReadOnlySpan<char> content = parser.Rest().Trim();

            return new DialogueParseResult(true, speaker, emotion, content);
        }

        private void ProcessDialogue(DialogueParseResult result)
        {
            // 检查对话栈的最后一个元素是否为空行，如果是则删除
            if (_parsingContext.DialogueStack.Count > 0 &&
                _parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
            {
                _parsingContext.DialogueStack.Pop();
            }

            var block = new DialogueBlock
            {
                Speaker = UnescapeString(result.Speaker),
                Emotion = UnescapeString(result.Emotion),
                Text = UnescapeString(result.Content),
                Condition = BuildConditionString()
            };

            int currentIndentLevel = _parsingContext.CurrentIndentLevel;

            // 处理对话块的父子关系
            if (_parsingContext.DialogueStack.Count > 0)
            {
                // 先尝试找到合适的父级元素（可以是节点或选项）
                var parent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Node, DialogueItemType.Option);

                if (parent != null)
                {
                    switch (parent.Value.ItemType)
                    {
                        case DialogueItemType.Node:
                            {
                                var parentNode = parent.Value.AsNode;
                                // 对话块直接属于节点
                                parentNode.Blocks.Add(block);
                                _currentNode = parentNode;
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(block, currentIndentLevel));
                                break;
                            }
                        case DialogueItemType.Option:
                            {
                                var parentOption = parent.Value.AsOption;
                                // 对话块属于选项，需要创建内部节点
                                var internalNode = new DialogueNode
                                {
                                    Name = GenerateInternalNodeName(parentOption, currentIndentLevel),
                                    IsInternal = true,
                                    ReturnNode = _parsingContext.FindParentNodeName(currentIndentLevel),
                                    Blocks = new List<DialogueBlock> { block }
                                };
                                _nodes[internalNode.Name] = internalNode;
                                parentOption.NextNodeName = internalNode.Name;
                                _currentNode = internalNode;

                                // 将内部节点加入到DialogueStack
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(internalNode, currentIndentLevel));
                                // 然后再加入对话块
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(block, currentIndentLevel));
                                break;
                            }
                        default:
                            _storyScriptException.AddError("对话块的层级关系不正确", _lineNumberMap[_currentLineNumber],
                                StoryErrorSeverity.Error, StoryErrorType.Syntax, "INVALID_DIALOGUE_HIERARCHY");
                            break;
                    }
                }
                else
                {
                    _storyScriptException.AddError("对话块的层级关系不正确", _lineNumberMap[_currentLineNumber],
                        StoryErrorSeverity.Error, StoryErrorType.Syntax, "INVALID_DIALOGUE_HIERARCHY");
                }
            }
            else
            {
                _storyScriptException.AddError("对话块必须在节点内部", _lineNumberMap[_currentLineNumber],
                    StoryErrorSeverity.Error, StoryErrorType.Syntax, "DIALOGUE_OUTSIDE_NODE");
            }
        }

        /// <summary>
        /// 生成内部节点的唯一标识符
        /// </summary>
        private string GenerateInternalNodeName(DialogueOption parentOption, int currentIndentLevel)
        {
            // 收集上下文信息
            var contextBuilder = new StringBuilder();

            // 1. 添加父节点信息
            contextBuilder.Append(_currentNode?.Name ?? "root");

            // 2. 添加选项文本（这是固定的）
            contextBuilder.Append("_").Append(parentOption.Text);

            // 3. 添加缩进级别（反映了结构位置）
            contextBuilder.Append("_").Append(currentIndentLevel);

            // 4. 添加当前条件上下文（如果有）
            if (_conditionLayers.Count > 0)
            {
                contextBuilder.Append("_").Append(BuildConditionString());
            }

            // 使用确定性哈希算法
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] contextBytes = Encoding.UTF8.GetBytes(contextBuilder.ToString());
            byte[] hashBytes = sha256.ComputeHash(contextBytes);

            // 只使用前8个字节生成一个短的十六进制字符串
            string shortHash = BitConverter.ToUInt64(hashBytes, 0).ToString("x");

            return $"internal_{shortHash}";
        }

        private static ChoiceParseResult ParseChoice(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);
            parser.SkipWhitespace();

            // 检查选项标识符（-> 或 -》）
            if (!parser.StartsWith("->") && !parser.StartsWith("-》"))
            {
                return new ChoiceParseResult(default, default, false);
            }
            parser.Advance(2);
            parser.SkipWhitespace();

            // 读取选项文本（直到[if或行尾）
            int start = parser.Position;
            bool inString = false;
            char stringChar = '\0';

            while (parser.TryPeek(out char c))
            {
                // 处理字符串
                if (c is '"' or '\'')
                {
                    // 检查是否是转义字符
                    if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                    {
                        parser.Advance();
                        continue; // 跳过转义字符的处理
                    }

                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                }

                // 如果不在字符串中，检查是否是条件开始
                if (!inString && c == '[')
                {
                    var tempParser = parser;
                    tempParser.Advance();
                    if (tempParser.StartsWith("if "))
                    {
                        break;
                    }
                }

                parser.Advance();
            }

            ReadOnlySpan<char> text = line.Slice(start, parser.Position - start).TrimEnd();
            if (text.IsEmpty)
            {
                return new ChoiceParseResult(default, default, false);
            }

            // 检查是否有条件
            ReadOnlySpan<char> condition = default;
            if (parser.TryPeek(out char nextChar) && nextChar == '[')
            {
                parser.Advance();        // 跳过 '['
                parser.SkipWhitespace(); // 跳过[后的空格

                if (!parser.StartsWith("if"))
                {
                    return new ChoiceParseResult(default, default, false);
                }
                parser.Advance(2); // 跳过 "if"

                // 必须确保if后面跟着空格
                if (!parser.TryPeek(out char afterIf) || !char.IsWhiteSpace(afterIf))
                {
                    return new ChoiceParseResult(default, default, false);
                }

                parser.SkipWhitespace();

                // 读取条件内容（直到遇到非字符串内的]）
                start = parser.Position;
                inString = false;
                stringChar = '\0';

                while (parser.TryPeek(out char c))
                {
                    // 处理字符串
                    if (c is '"' or '\'')
                    {
                        // 检查是否是转义字符
                        if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                        {
                            parser.Advance();
                            continue; // 跳过转义字符的处理
                        }

                        if (!inString)
                        {
                            inString = true;
                            stringChar = c;
                        }
                        else if (c == stringChar)
                        {
                            inString = false;
                        }
                    }

                    // 如果不在字符串中且遇到]，则结束条件
                    if (!inString && c == ']')
                    {
                        break;
                    }

                    parser.Advance();
                }

                condition = line.Slice(start, parser.Position - start).Trim(); // 去除条件前后的空格
                if (!parser.TryReadChar(']'))
                {
                    return new ChoiceParseResult(default, default, false);
                }
            }

            return new ChoiceParseResult(text, condition, true);
        }

        private void ProcessChoice(ChoiceParseResult result)
        {
            var choice = new DialogueOption
            {
                Text = UnescapeString(result.Text),
                Condition = !result.Condition.IsEmpty ?
                    PreprocessExpression(result.Condition.ToString(), true) :
                    string.Empty
            };

            int currentIndentLevel = _parsingContext.CurrentIndentLevel;

            // 处理选项的父子关系
            if (_parsingContext.DialogueStack.Count > 0)
            {
                // 如果栈顶是空块，弹出它并创建新的对话块
                if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                    _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
                {
                    _parsingContext.DialogueStack.Pop();
                    // 创建新的对话块
                    var nodeParent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Node);
                    if (nodeParent != null)
                    {
                        var parentNode = nodeParent.Value.AsNode;
                        var newBlock = new DialogueBlock
                        {
                            Speaker = string.Empty,
                            Emotion = string.Empty,
                            Text = string.Empty,
                            Condition = BuildConditionString(),
                            NextNodeName = string.Empty,
                            Commands = new List<CommandData>(),
                            Options = new List<DialogueOption> { choice }
                        };
                        parentNode.Blocks.Add(newBlock);
                        _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                        _parsingContext.DialogueStack.Push(new DialogueStackItem(choice, currentIndentLevel));
                        return;
                    }
                    else
                    {
                        _storyScriptException.AddError("选项必须在对话块或节点内部", _lineNumberMap[_currentLineNumber],
                            StoryErrorSeverity.Error, StoryErrorType.Syntax, "CHOICE_OUTSIDE_SCOPE");
                    }
                }

                // 选项可以属于对话块或节点
                var parent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Block, DialogueItemType.Node);

                if (parent != null)
                {
                    switch (parent.Value.ItemType)
                    {
                        case DialogueItemType.Block:
                            {
                                var parentBlock = parent.Value.AsBlock;
                                parentBlock.Options.Add(choice);
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(choice, currentIndentLevel));
                                break;
                            }
                        case DialogueItemType.Node:
                            {
                                var parentNode = parent.Value.AsNode;
                                // 如果父级是节点，创建一个新的对话块来包含这个选项
                                var newBlock = new DialogueBlock
                                {
                                    Speaker = string.Empty,
                                    Emotion = string.Empty,
                                    Text = string.Empty,
                                    Condition = BuildConditionString(),
                                    NextNodeName = string.Empty,
                                    Commands = new List<CommandData>(),
                                    Options = new List<DialogueOption> { choice }
                                };
                                parentNode.Blocks.Add(newBlock);
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(choice, currentIndentLevel));
                                break;
                            }
                        default:
                            _storyScriptException.AddError("选项必须在对话块或节点内部", _lineNumberMap[_currentLineNumber],
                                StoryErrorSeverity.Error, StoryErrorType.Syntax, "CHOICE_OUTSIDE_SCOPE");
                            break;
                    }
                }
                else
                {
                    _storyScriptException.AddError("选项必须在对话块或节点内部", _lineNumberMap[_currentLineNumber],
                        StoryErrorSeverity.Error, StoryErrorType.Syntax, "CHOICE_OUTSIDE_SCOPE");
                }
            }
            else
            {
                _storyScriptException.AddError("选项必须在对话块或节点内部", _lineNumberMap[_currentLineNumber],
                    StoryErrorSeverity.Error, StoryErrorType.Syntax, "CHOICE_OUTSIDE_SCOPE");
            }
        }

        private static JumpParseResult ParseJump(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);
            parser.SkipWhitespace();

            // 检查跳转标识符
            if (parser.StartsWith("=>") || parser.StartsWith("=》"))
            {
                parser.Advance(2);
            }
            else if (parser.StartsWith("jump "))
            {
                parser.Advance(5);
            }
            else
            {
                return new JumpParseResult(default, false);
            }

            parser.SkipWhitespace();

            // 读取目标节点名称
            int start = parser.Position;
            bool inString = false;
            char stringChar = '\0';

            while (parser.TryPeek(out char c))
            {
                // 处理字符串
                if (c is '"' or '\'')
                {
                    // 检查是否是转义字符
                    if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                    {
                        parser.Advance();
                        continue; // 跳过转义字符的处理
                    }

                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                }

                // 如果不在字符串中，检查是否是空格
                if (!inString && char.IsWhiteSpace(c))
                {
                    break;
                }

                parser.Advance();
            }

            ReadOnlySpan<char> target = line.Slice(start, parser.Position - start).Trim();

            return new JumpParseResult(target, !target.IsEmpty);
        }

        private void ProcessJump(JumpParseResult result)
        {
            string target = result.Target.ToString();
            int currentIndentLevel = _parsingContext.CurrentIndentLevel;

            // 处理跳转的父子关系
            if (_parsingContext.DialogueStack.Count > 0)
            {
                // 如果栈顶是空块，弹出它并创建新的对话块
                if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                    _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
                {
                    _parsingContext.DialogueStack.Pop();
                    // 创建新的对话块
                    var nodeParent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Node);
                    if (nodeParent != null)
                    {
                        var parentNode = nodeParent.Value.AsNode;
                        var newBlock = new DialogueBlock
                        {
                            Speaker = string.Empty,
                            Emotion = string.Empty,
                            Text = string.Empty,
                            Condition = BuildConditionString(),
                            NextNodeName = target,
                            Commands = new List<CommandData>(),
                            Options = new List<DialogueOption>()
                        };
                        parentNode.Blocks.Add(newBlock);
                        _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                        return;
                    }
                    else
                    {
                        _storyScriptException.AddError("跳转语句必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                            StoryErrorSeverity.Error, StoryErrorType.Syntax, "JUMP_OUTSIDE_SCOPE");
                    }
                }

                // 跳转可以属于对话块、选项或节点
                var parent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Block, DialogueItemType.Option, DialogueItemType.Node);

                if (parent != null)
                {
                    switch (parent.Value.ItemType)
                    {
                        case DialogueItemType.Block:
                            {
                                var parentBlock = parent.Value.AsBlock;
                                parentBlock.NextNodeName = target;
                                break;
                            }
                        case DialogueItemType.Option:
                            {
                                var parentOption = parent.Value.AsOption;
                                parentOption.NextNodeName = target;
                                break;
                            }
                        case DialogueItemType.Node:
                            {
                                var parentNode = parent.Value.AsNode;
                                // 如果父级是节点，创建一个新的对话块来包含这个跳转
                                var newBlock = new DialogueBlock
                                {
                                    Speaker = string.Empty,
                                    Emotion = string.Empty,
                                    Text = string.Empty,
                                    Condition = BuildConditionString(),
                                    NextNodeName = target,
                                    Commands = new List<CommandData>(),
                                    Options = new List<DialogueOption>()
                                };
                                parentNode.Blocks.Add(newBlock);
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                                break;
                            }
                        default:
                            _storyScriptException.AddError("跳转语句必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                                StoryErrorSeverity.Error, StoryErrorType.Syntax, "JUMP_OUTSIDE_SCOPE");
                            break;
                    }
                }
                else
                {
                    _storyScriptException.AddError("跳转语句必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                        StoryErrorSeverity.Error, StoryErrorType.Syntax, "JUMP_OUTSIDE_SCOPE");
                }
            }
            else
            {
                _storyScriptException.AddError("跳转语句必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                    StoryErrorSeverity.Error, StoryErrorType.Syntax, "JUMP_OUTSIDE_SCOPE");
            }
        }

        private static CommandParseResult ParseCommand(ReadOnlySpan<char> line)
        {
            var parser = new SpanBasedParser(line);
            parser.SkipWhitespace();

            // 读取命令类型
            int start = parser.Position;
            bool inString = false;
            char stringChar = '\0';

            while (parser.TryPeek(out char c))
            {
                // 处理字符串
                if (c is '"' or '\'')
                {
                    // 检查是否是转义字符
                    if (parser.Position > 0 && line[parser.Position - 1] == '\\')
                    {
                        parser.Advance();
                        continue; // 跳过转义字符的处理
                    }

                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                }

                // 如果不在字符串中，检查是否是空格
                if (!inString && char.IsWhiteSpace(c))
                {
                    break;
                }

                parser.Advance();
            }

            ReadOnlySpan<char> cmdTypeStr = line.Slice(start, parser.Position - start).Trim();
            if (!Enum.TryParse<CommandType>(cmdTypeStr.ToString(), true, out var type))
            {
                return new CommandParseResult(default, default, false);
            }

            parser.SkipWhitespace();
            return new CommandParseResult(type, parser.Rest().Trim(), true);
        }

        private void ProcessCommand(CommandParseResult result)
        {
            string commandContent = result.Content.ToString().Trim();
            if (string.IsNullOrEmpty(commandContent))
            {
                _storyScriptException.AddError($"{result.Type}命令不能为空", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Validation, $"{result.Type.ToString().ToUpper()}_COMMAND_EMPTY");
                return;
            }

            ValidateCommand(result.Type, commandContent);

            var command = new CommandData
            {
                CommandType = result.Type,
                CsExpression = CommandHandler(result.Type, commandContent)
            };

            int currentIndentLevel = _parsingContext.CurrentIndentLevel;

            // 处理命令的父子关系
            if (_parsingContext.DialogueStack.Count > 0)
            {
                // 如果栈顶是空块，弹出它并创建新的对话块
                if (_parsingContext.DialogueStack.Peek().ItemType == DialogueItemType.Block &&
                    _parsingContext.DialogueStack.Peek().AsBlock.IsEmpty())
                {
                    _parsingContext.DialogueStack.Pop();
                    // 创建新的对话块
                    var nodeParent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Node);
                    if (nodeParent != null)
                    {
                        var parentNode = nodeParent.Value.AsNode;
                        var newBlock = new DialogueBlock
                        {
                            Speaker = string.Empty,
                            Emotion = string.Empty,
                            Text = string.Empty,
                            Condition = BuildConditionString(),
                            NextNodeName = string.Empty,
                            Commands = new List<CommandData> { command },
                            Options = new List<DialogueOption>()
                        };
                        parentNode.Blocks.Add(newBlock);
                        _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                        return;
                    }
                    else
                    {
                        _storyScriptException.AddError("命令必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                            StoryErrorSeverity.Error, StoryErrorType.Syntax, "COMMAND_OUTSIDE_SCOPE");
                    }
                }

                // 命令可以属于对话块、选项或节点
                var parent = _parsingContext.FindParent(currentIndentLevel, DialogueItemType.Block, DialogueItemType.Option, DialogueItemType.Node);

                if (parent != null)
                {
                    switch (parent.Value.ItemType)
                    {
                        case DialogueItemType.Block:
                            {
                                var parentBlock = parent.Value.AsBlock;
                                parentBlock.Commands.Add(command);
                                break;
                            }
                        case DialogueItemType.Option:
                            {
                                var parentOption = parent.Value.AsOption;
                                parentOption.Commands.Add(command);
                                break;
                            }
                        case DialogueItemType.Node:
                            {
                                var parentNode = parent.Value.AsNode;
                                var newBlock = new DialogueBlock
                                {
                                    Speaker = string.Empty,
                                    Emotion = string.Empty,
                                    Text = string.Empty,
                                    Condition = BuildConditionString(),
                                    NextNodeName = string.Empty,
                                    Commands = new List<CommandData> { command },
                                    Options = new List<DialogueOption>()
                                };
                                parentNode.Blocks.Add(newBlock);
                                _parsingContext.DialogueStack.Push(new DialogueStackItem(newBlock, currentIndentLevel));
                                break;
                            }
                        default:
                            _storyScriptException.AddError("命令必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                                StoryErrorSeverity.Error, StoryErrorType.Syntax, "INVALID_COMMAND_HIERARCHY");
                            break;
                    }
                }
                else
                {
                    _storyScriptException.AddError("命令必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                        StoryErrorSeverity.Error, StoryErrorType.Syntax, "COMMAND_OUTSIDE_SCOPE");
                }
            }
            else
            {
                _storyScriptException.AddError("命令必须在节点、对话块或选项内部", _lineNumberMap[_currentLineNumber],
                    StoryErrorSeverity.Error, StoryErrorType.Syntax, "COMMAND_OUTSIDE_SCOPE");
            }
        }

        private void ValidateCommand(CommandType commandType, string commandContent)
        {
            if (string.IsNullOrEmpty(commandContent))
            {
                _storyScriptException.AddError($"{Enum.GetName(typeof(CommandType), commandType)}命令不能为空", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Validation, $"{Enum.GetName(typeof(CommandType), commandType)?.ToUpper()}_COMMAND_EMPTY");
                return;
            }

            switch (commandType)
            {
                case CommandType.Var:
                    string[] varParts = commandContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (varParts.Length < 2)
                    {
                        _storyScriptException.AddError($"{Enum.GetName(typeof(CommandType), commandType)}命令格式错误：需要提供变量名和初始值", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Validation, "VAR_COMMAND_FORMAT_ERROR");
                    }
                    break;
                case CommandType.Set:
                case CommandType.Add:
                case CommandType.Sub:
                    string[] parts = commandContent.Split(new[]
                    {
                        ' '
                    }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        _storyScriptException.AddError($"{Enum.GetName(typeof(CommandType), commandType)}命令格式错误：需要提供变量名和值", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Validation,
                            $"{Enum.GetName(typeof(CommandType), commandType)?.ToUpper()}_COMMAND_FORMAT_ERROR");
                    }
                    break;
                case CommandType.Call:
                    if (!commandContent.Contains('('))
                    {
                        _storyScriptException.AddError("call命令必须包含函数调用", _lineNumberMap[_currentLineNumber], StoryErrorSeverity.Error, StoryErrorType.Validation, "CALL_COMMAND_FORMAT_ERROR");
                    }
                    break;
                case CommandType.Wait:
                    // 支持所有C#表达式
                    break;
            }
        }

        /// <summary>
        /// 构建条件字符串
        /// </summary>
        /// <returns>条件字符串</returns>
        private string BuildConditionString()
        {
            if (_conditionLayers.Count == 0) return string.Empty;

            var conditions = new List<string>();
            int currentIndentLevel = _parsingContext.CurrentIndentLevel;

            // 从最外层到最内层遍历
            foreach (var layer in _conditionLayers.Reverse())
            {
                if (layer.Conditions.Count == 0) continue;

                // 检查当前缩进级别是否小于条件层级
                // 如果是，则该条件对当前元素无效（已经退出了条件块的范围）
                if (currentIndentLevel < layer.ConditionLevel)
                {
                    continue;
                }

                if (layer.HasElse)
                {
                    // Else条件：!(C1 || C2...)
                    conditions.Add($"!({string.Join(" || ", layer.Conditions)})");
                }
                else
                {
                    // 正常条件：!C1 && !C2 && ... && Cn
                    var layerExpression = string.Join(" && ",
                        layer.Conditions.Select((c, idx) =>
                            idx == layer.Conditions.Count - 1 ?
                                $"({c})" :
                                $"!({c})"));

                    conditions.Add(layerExpression);
                }
            }

            // 组合各层条件：外层 AND 内层
            return string.Join(" && ", conditions.Where(c => !string.IsNullOrEmpty(c)));
        }

        /// <summary>
        /// 处理命令字符串内容
        /// </summary>
        /// <param name="cmdType">命令类型</param>
        /// <param name="content">命令内容</param>
        /// <returns>处理后的命令内容</returns>
        private string CommandHandler(CommandType cmdType, string content)
        {
            return cmdType switch
            {
                CommandType.Var => ProcessVarCommand(content),
                CommandType.Set => ProcessSetCommand(content),
                CommandType.Add => ProcessAddCommand(content),
                CommandType.Sub => ProcessSubCommand(content),
                CommandType.Call => content,
                CommandType.Wait => ProcessWaitCommand(content),
                _ => string.Empty,
            };

        }

        private string ProcessSetCommand(string content)
        {
            content = PreprocessExpression(content);

            // 如果已经包含 = 号，说明是完整表达式
            if (content.Contains('='))
            {
                return content;
            }

            // 处理 set identifier value 格式
            string[] parts = content.Split(new[]
            {
                ' '
            }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string identifier = parts[0];
                string value = string.Join(" ", parts.Skip(1));
                return $"{identifier} = {value}";
            }

            return content;
        }

        private string ProcessAddCommand(string content)
        {
            content = PreprocessExpression(content);

            string[] parts = content.Split(new[]
            {
                ' '
            }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string identifier = parts[0];
                string value = string.Join(" ", parts.Skip(1));
                return $"{identifier} = {identifier} + {value}";
            }
            return content;
        }

        private string ProcessSubCommand(string content)
        {
            content = PreprocessExpression(content);

            string[] parts = content.Split(new[]
            {
                ' '
            }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string identifier = parts[0];
                string value = string.Join(" ", parts.Skip(1));
                return $"{identifier} = {identifier} - {value}";
            }
            return content;
        }

        private string ProcessWaitCommand(string content)
        {
            content = PreprocessExpression(content);
            return content;
        }

        private string ProcessVarCommand(string content)
        {
            content = PreprocessExpression(content);

            // 如果已经包含 = 号，说明是完整表达式
            if (content.Contains('='))
            {
                return content;
            }

            // 处理 var identifier value 格式
            string[] parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string identifier = parts[0];
                string value = string.Join(" ", parts.Skip(1));
                return $"{identifier} = {value}";
            }
            return content;
        }

        /// <summary>
        /// 预处理表达式，包括条件表达式和赋值表达式等
        /// </summary>
        /// <param name="expression">要处理的表达式</param>
        /// <param name="isCondition">是否是条件表达式</param>
        /// <returns>处理后的表达式</returns>
        private string PreprocessExpression(string expression, bool isCondition = false)
        {
            expression = expression.Trim();
            if (string.IsNullOrEmpty(expression)) return expression;

            // 替换逻辑运算符关键字为对应的 C# 运算符
            if (isCondition)
            {
                expression = Regex.Replace(expression, @"\band\b", " && ");
                expression = Regex.Replace(expression, @"\bor\b", " || ");
                expression = Regex.Replace(expression, @"\bnot\b", " ! ");
                expression = Regex.Replace(expression, @"\bxor\b", " ^ ");
                expression = Regex.Replace(expression, @"\beq\b", " == ");
                expression = Regex.Replace(expression, @"\bneq\b", " != ");
                expression = Regex.Replace(expression, @"\bgt\b", " > ");
                expression = Regex.Replace(expression, @"\blt\b", " < ");
                expression = Regex.Replace(expression, @"\bgte\b", " >= ");
                expression = Regex.Replace(expression, @"\blte\b", " <= ");

                // 清理多余的空格
                expression = Regex.Replace(expression, @"\s+", " ").Trim();
            }

            return expression;
        }

        /// <summary>
        /// 处理转义字符
        /// </summary>
        /// <param name="text">要处理的文本</param>
        /// <returns>处理后的文本</returns>
        private string UnescapeString(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty) return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool isEscaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (isEscaped)
                {
                    switch (c)
                    {
                        case '\'':
                        case '"':
                        case '\\':
                            sb.Append(c);
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'v':
                            sb.Append('\v');
                            break;
                        case '0':
                            sb.Append('\0');
                            break;
                        default:
                            // 如果不是有效的转义字符，保留原字符
                            sb.Append('\\').Append(c);
                            break;
                    }
                    isEscaped = false;
                }
                else if (c == '\\')
                {
                    isEscaped = true;
                }
                else
                {
                    sb.Append(c);
                }
            }

            // 如果最后一个字符是反斜杠，添加它
            if (isEscaped)
            {
                sb.Append('\\');
            }

            return sb.ToString();
        }

    }
}
