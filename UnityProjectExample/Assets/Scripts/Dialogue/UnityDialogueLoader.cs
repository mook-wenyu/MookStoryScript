using System;
using MookStoryScript;
using UnityEngine;
using Logger = MookStoryScript.Logger;

public class UnityDialogueLoader : IDialogueLoader
{
    private readonly string _rootDir;
    private readonly string[] _fileExtensions = {".txt", ".mds"};

    public UnityDialogueLoader() : this("Story")
    {
    }

    public UnityDialogueLoader(string rootDir)
    {
        Console.WriteLine("Initializing UnityDialogueLoader...");

        _rootDir = rootDir;
    }

    /// <summary>
    /// 加载脚本
    /// </summary>
    public void LoadScripts(Runner runner)
    {
        // 加载所有对话脚本
        var assets = Resources.LoadAll<TextAsset>(_rootDir);
        foreach (var asset in assets)
        {
            try
            {
                LoadDialogueScriptContent(asset.text, runner, asset.name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载脚本文件 {asset.name} 时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载故事脚本内容
    /// </summary>
    public void LoadDialogueScriptContent(string scriptContent, Runner runner, string sourceName = "Unknown source")
    {
        try
        {
            var nodes = runner.StoryScriptParsers.ParseScript(scriptContent, sourceName);
            foreach (var node in nodes)
            {
                runner.DialogueNodes[node.Key] = node.Value;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[{sourceName}] Failed to parse story script\n{ex.Message}");
        }
    }

}
