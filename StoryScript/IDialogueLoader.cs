using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MookStoryScript
{
    public interface IDialogueLoader
    {
        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="runner">运行器</param>
        /// <returns>异步任务</returns>
        public void LoadScripts(Runner runner);

        /// <summary>
        /// 加载故事脚本内容
        /// </summary>
        public void LoadDialogueScriptContent(string scriptContent, Runner runner, string sourceName = "Unknown source");
    }

    public class DefaultDialogueLoader : IDialogueLoader
    {
        private readonly string _rootDir;
        private readonly string[] _fileExtensions = { ".txt", ".mds" };

        public DefaultDialogueLoader() : this("DialogueScripts")
        {
        }

        public DefaultDialogueLoader(string rootDir)
        {
            Logger.Log("Initializing DefaultDialogueLoader...");

            _rootDir = rootDir;

            if (!Directory.Exists(_rootDir))
            {
                Directory.CreateDirectory(_rootDir);
            }
        }

        /// <summary>
        /// 加载脚本
        /// </summary>
        /// <param name="runner">运行器</param>
        public void LoadScripts(Runner runner)
        {
            // 获取所有符合条件的文件
            var files = Directory.GetFiles(_rootDir, "*.*", SearchOption.AllDirectories)
                .Where(f => _fileExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            // 加载所有文件
            foreach (var file in files)
            {
                try
                {
                    if (string.IsNullOrEmpty(file)) continue;
                    string ds = Utils.ReadFile(file);

                    if (string.IsNullOrEmpty(ds)) continue;
                    string fileName = Path.GetFileName(file);
                    LoadDialogueScriptContent(ds, runner, fileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载脚本文件 {file} 时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 加载故事脚本内容
        /// </summary>
        /// <param name="scriptContent">故事脚本内容</param>
        /// <param name="runner">运行器</param>
        /// <param name="sourceName">源名称</param>
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
}
