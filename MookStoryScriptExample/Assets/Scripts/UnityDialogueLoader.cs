using System;
using System.Collections.Generic;
using MookStoryScript;
using UnityEngine;

public class UnityDialogueLoader : IDialogueLoader
{
    public Dictionary<string, DialogueNode> DialogueNodes { get; private set; }

    public StoryScriptParser StoryScriptParsers { get; private set; }

    public UnityDialogueLoader() : this(string.Empty)
    {
    }

    public UnityDialogueLoader(string rootDir)
    {
        Console.WriteLine("Initializing DefaultDialogueLoader...");
        StoryScriptParsers = new StoryScriptParser();
        DialogueNodes = new Dictionary<string, DialogueNode>();

        if (string.IsNullOrEmpty(rootDir))
        {
            rootDir = "Story";
        }

        // 加载所有对话脚本
        var assets = Resources.LoadAll<TextAsset>(rootDir);
        foreach (var asset in assets)
        {
            LoadDialogueScriptContent(asset.text, asset.name);
        }
    }

    /// <summary>
    /// 加载对话脚本内容
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
            Console.Error.WriteLine($"[{sourceName}] Failed to parse story script\n{ex.Message}");
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
    public DialogueNode GetDialogueNode(string nodeName)
    {
        return DialogueNodes.GetValueOrDefault(nodeName, null);
    }
}
