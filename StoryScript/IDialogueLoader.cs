using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryScript;

namespace MookStoryScript
{
    public interface IDialogueLoader
    {
        public Dictionary<string, DialogueNode> DialogueNodes { get; }
        public void LoadDialogueScriptContent(string scriptContent, string sourceName = "Unknown source");
        public void RegisterDialogueNode(DialogueNode dialogueNode);
        public DialogueNode? GetDialogueNode(string nodeName);
    }

    public class DefaultDialogueLoader : IDialogueLoader
    {
        public Dictionary<string, DialogueNode> DialogueNodes { get; private set; }

        public StoryScriptParser StoryScriptParsers { get; private set; }

        public DefaultDialogueLoader() : this(string.Empty)
        {
        }

        public DefaultDialogueLoader(string rootDir)
        {
            Logger.Log("Initializing DefaultDialogueLoader...");
            StoryScriptParsers = new StoryScriptParser();
            DialogueNodes = new Dictionary<string, DialogueNode>();

            if (string.IsNullOrEmpty(rootDir))
            {
                rootDir = "Story";
            }

            // 加载所有对话脚本
            var files = Utils.GetFiles(rootDir, new[] {".txt", ".mds"});
            foreach (string file in files)
            {
                if (string.IsNullOrEmpty(file)) continue;
                string ds = Utils.ReadFile(file);

                if (string.IsNullOrEmpty(ds)) continue;
                string fileName = Path.GetFileName(file);
                LoadDialogueScriptContent(ds, fileName);
            }
        }

        /// <summary>
        /// 加载故事脚本内容
        /// </summary>
        public void LoadDialogueScriptContent(string scriptContent, string sourceName = "Unknown source")
        {
            try
            {
                var nodes = StoryScriptParsers.ParseScript(scriptContent, sourceName);
                foreach (var node in nodes)
                {
                    DialogueNodes[node.Key] = node.Value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{sourceName}] Failed to parse story script\n{ex.Message}");
            }
        }

        /// <summary>
        /// 注册对话数据
        /// </summary>
        public void RegisterDialogueNode(DialogueNode dialogueNode)
        {
            // 注册对话
            DialogueNodes[dialogueNode.Name] = dialogueNode;
        }

        /// <summary>
        /// 获取对话数据
        /// </summary>
        public DialogueNode? GetDialogueNode(string nodeName)
        {
            return DialogueNodes!.GetValueOrDefault(nodeName, null);
        }

    }
}
