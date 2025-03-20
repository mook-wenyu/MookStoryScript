using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// 对话状态
    /// </summary>
    public class DialogueState
    {
        public string SectionId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public int BlockIndex { get; set; }
        public int? ChoiceIndex { get; set; }
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// 对话进度
    /// </summary>
    public class DialogueProgress
    {
        // 当前对话小节ID
        public string currentSectionId = string.Empty;

        // 当前对话节点ID
        public string currentDialogueNodeId = string.Empty;

        // 当前对话块索引
        public int currentDialogueBlockIndex = 0;

        // 按时序记录的对话状态列表
        public readonly List<DialogueState> dialogueHistory = new();

        // 记录时间戳
        public long currentTimestamp = 0;

        // 返回点栈，用于记录内部节点的返回位置
        public Stack<ReturnPoint> returnPointStack = new();

        /// <summary>
        /// 记录小节
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        public void RecordSection(string sectionId)
        {
            currentSectionId = sectionId;

            dialogueHistory.Add(new DialogueState
            {
                SectionId = sectionId,
                NodeId = string.Empty,
                BlockIndex = -1,
                Timestamp = currentTimestamp++
            });
        }

        /// <summary>
        /// 记录节点
        /// </summary>
        /// <param name="dialogueNodeId">对话节点ID</param>
        public void RecordNode(string dialogueNodeId)
        {
            currentDialogueNodeId = dialogueNodeId;

            dialogueHistory.Add(new DialogueState
            {
                SectionId = currentSectionId,
                NodeId = dialogueNodeId,
                BlockIndex = -1,
                Timestamp = currentTimestamp++
            });
        }

        /// <summary>
        /// 记录当前节点对话块
        /// </summary>
        /// <param name="dialogueBlockIndex">对话块索引</param>
        public void RecordBlock(int dialogueBlockIndex)
        {
            currentDialogueBlockIndex = dialogueBlockIndex;

            dialogueHistory.Add(new DialogueState
            {
                SectionId = currentSectionId,
                NodeId = currentDialogueNodeId,
                BlockIndex = dialogueBlockIndex,
                Timestamp = currentTimestamp++
            });
        }

        /// <summary>
        /// 记录当前节点当前块选项
        /// </summary>
        /// <param name="choiceIndex">选项索引</param>
        public void RecordChoice(int choiceIndex)
        {
            dialogueHistory.Add(new DialogueState
            {
                SectionId = currentSectionId,
                NodeId = currentDialogueNodeId,
                BlockIndex = currentDialogueBlockIndex,
                ChoiceIndex = choiceIndex,
                Timestamp = currentTimestamp++
            });
        }

        /// <summary>
        /// 将返回点压入栈
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="blockIndex">块索引</param>
        public void PushReturnPoint(string nodeName, int blockIndex)
        {
            returnPointStack.Push(new ReturnPoint(nodeName, blockIndex));
        }

        /// <summary>
        /// 从栈中弹出返回点
        /// </summary>
        /// <returns>返回点，如果栈为空则返回默认值</returns>
        public ReturnPoint PopReturnPoint()
        {
            if (returnPointStack.Count > 0)
            {
                return returnPointStack.Pop();
            }
            return default;
        }

        /// <summary>
        /// 查看栈顶的返回点但不移除
        /// </summary>
        /// <returns>返回点，如果栈为空则返回默认值</returns>
        public ReturnPoint PeekReturnPoint()
        {
            if (returnPointStack.Count > 0)
            {
                return returnPointStack.Peek();
            }
            return default;
        }

        /// <summary>
        /// 获取返回点栈的副本
        /// </summary>
        /// <returns>返回点栈的副本</returns>
        public Stack<ReturnPoint> GetReturnPointStack()
        {
            return new Stack<ReturnPoint>(new Stack<ReturnPoint>(returnPointStack));
        }

        /// <summary>
        /// 检查返回点栈是否为空
        /// </summary>
        /// <returns>如果栈为空则返回true，否则返回false</returns>
        public bool IsReturnPointStackEmpty()
        {
            return returnPointStack.Count == 0;
        }

        /// <summary>
        /// 获取返回点栈中的元素数量
        /// </summary>
        /// <returns>返回点栈中的元素数量</returns>
        public int GetReturnPointStackCount()
        {
            return returnPointStack.Count;
        }

        /// <summary>
        /// 清空返回点栈
        /// </summary>
        public void ClearReturnPointStack()
        {
            returnPointStack.Clear();
        }

        /// <summary>
        /// 设置返回点栈
        /// </summary>
        /// <param name="stack">要设置的返回点栈</param>
        public void SetReturnPointStack(Stack<ReturnPoint> stack)
        {
            returnPointStack.Clear();
            foreach (var point in stack.Reverse())
            {
                returnPointStack.Push(point);
            }
        }

        /// <summary>
        /// 获取完整的对话历史记录
        /// </summary>
        /// <returns>对话历史记录列表</returns>
        public List<DialogueState> GetDialogueHistory()
        {
            return new List<DialogueState>(dialogueHistory);
        }

        /// <summary>
        /// 获取当前小节的所有历史记录（按时间顺序）
        /// </summary>
        /// <returns>当前小节的所有历史记录</returns>
        public List<DialogueState> GetSectionHistory()
        {
            return GetSectionHistory(currentSectionId);
        }

        /// <summary>
        /// 获取指定小节的所有历史记录（按时间顺序）
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <returns>该小节的所有历史记录</returns>
        public List<DialogueState> GetSectionHistory(string sectionId)
        {
            return dialogueHistory
                .Where(state => state.SectionId == sectionId)
                .OrderBy(state => state.Timestamp)
                .ToList();
        }

        /// <summary>
        /// 获取指定小节最后的状态
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <returns>最后的状态</returns>
        public DialogueState? GetLastSectionState(string sectionId)
        {
            return dialogueHistory
                .Where(state => state.SectionId == sectionId)
                .OrderByDescending(state => state.Timestamp)
                .FirstOrDefault();
        }

        /// <summary>
        /// 获取已访问的小节列表
        /// </summary>
        /// <returns>小节ID列表</returns>
        public List<string> GetSections()
        {
            return dialogueHistory
                .Where(state => !string.IsNullOrEmpty(state.SectionId) && state.BlockIndex == -1 && string.IsNullOrEmpty(state.NodeId))
                .Select(state => state.SectionId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 获取当前小节已访问的对话节点列表
        /// </summary>
        /// <returns>对话节点ID列表</returns>
        public List<string> GetNodes()
        {
            return GetNodes(currentSectionId);
        }

        /// <summary>
        /// 获取指定小节已访问的对话节点列表（按访问顺序）
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <returns>对话节点ID列表</returns>
        public List<string> GetNodes(string sectionId)
        {
            return dialogueHistory
                .Where(state => state.SectionId == sectionId && !string.IsNullOrEmpty(state.NodeId) && state.BlockIndex == -1)
                .OrderBy(state => state.Timestamp)
                .Select(state => state.NodeId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 获取指定节点的访问次数
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <param name="nodeId">节点ID</param>
        /// <returns>访问次数</returns>
        public int GetNodeVisitCount(string sectionId, string nodeId)
        {
            return dialogueHistory.Count(state =>
                state.SectionId == sectionId &&
                state.NodeId == nodeId &&
                state.BlockIndex == -1);
        }

        /// <summary>
        /// 获取所有小节中指定节点的访问次数总和
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>所有小节中该节点的访问次数总和</returns>
        public int GetAllSectionsNodeVisitCount(string nodeId)
        {
            return dialogueHistory.Count(state =>
                state.NodeId == nodeId &&
                state.BlockIndex == -1);
        }

        /// <summary>
        /// 获取当前节点已访问的对话块索引列表
        /// </summary>
        /// <returns>对话块索引列表</returns>
        public List<int> GetBlocks()
        {
            return GetBlocks(currentSectionId, currentDialogueNodeId);
        }

        /// <summary>
        /// 获取指定节点已访问的对话块索引列表（按访问顺序）
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <param name="dialogueNodeId">对话节点ID</param>
        /// <returns>对话块索引列表</returns>
        public List<int> GetBlocks(string sectionId, string dialogueNodeId)
        {
            return dialogueHistory
                .Where(state =>
                    state.SectionId == sectionId &&
                    state.NodeId == dialogueNodeId &&
                    state.BlockIndex >= 0)
                .OrderBy(state => state.Timestamp)
                .Select(state => state.BlockIndex)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 获取当前对话块的选择记录（按选择顺序）
        /// </summary>
        /// <returns>选择过的选项索引列表</returns>
        public List<int> GetBlockChoices()
        {
            return GetBlockChoices(currentSectionId, currentDialogueNodeId, currentDialogueBlockIndex);
        }

        /// <summary>
        /// 获取指定对话块的选择记录（按选择顺序）
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="blockIndex">对话块索引</param>
        /// <returns>选择过的选项索引列表</returns>
        public List<int> GetBlockChoices(string sectionId, string nodeId, int blockIndex)
        {
            return dialogueHistory
                .Where(state =>
                    state.SectionId == sectionId &&
                    state.NodeId == nodeId &&
                    state.BlockIndex == blockIndex &&
                    state.ChoiceIndex.HasValue)
                .OrderBy(state => state.Timestamp)
                .Select(state => state.ChoiceIndex!.Value)
                .ToList();
        }

        /// <summary>
        /// 判断节点对话块选项是否已选择
        /// </summary>
        /// <param name="choiceIndex">选项索引</param>
        /// <returns>是否已选择</returns>
        public bool IsChoiceSelected(int choiceIndex)
        {
            return IsChoiceSelected(currentSectionId, currentDialogueNodeId, currentDialogueBlockIndex, choiceIndex);
        }

        /// <summary>
        /// 判断指定节点对话块选项是否已选择
        /// </summary>
        /// <param name="sectionId">对话小节ID</param>
        /// <param name="dialogueNodeId">对话节点ID</param>
        /// <param name="dialogueBlockIndex">对话块索引</param>
        /// <param name="choiceIndex">选项索引</param>
        /// <returns>是否已选择</returns>
        public bool IsChoiceSelected(string sectionId, string dialogueNodeId, int dialogueBlockIndex, int choiceIndex)
        {
            return dialogueHistory.Any(state =>
                state.SectionId == sectionId &&
                state.NodeId == dialogueNodeId &&
                state.BlockIndex == dialogueBlockIndex &&
                state.ChoiceIndex == choiceIndex);
        }

        /// <summary>
        /// 重置对话进度
        /// </summary>
        public void Reset()
        {
            currentSectionId = string.Empty;
            currentDialogueNodeId = string.Empty;
            currentDialogueBlockIndex = 0;
            currentTimestamp = 0;
            dialogueHistory.Clear();
            returnPointStack.Clear();
        }

        /// <summary>
        /// 清除所有历史记录但保留当前状态
        /// </summary>
        public void Clear()
        {
            dialogueHistory.Clear();
            currentTimestamp = 0;
        }
    }
}
