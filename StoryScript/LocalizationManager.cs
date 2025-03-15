using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MookStoryScript
{
    /// <summary>
    /// 本地化管理器，负责加载和管理本地化文本
    /// </summary>
    public class LocalizationManager
    {
        private readonly Dictionary<string, Dictionary<string, string>> _localizedTexts;

        /// <summary>
        /// 默认语言
        /// </summary>
        public string DefaultLanguage { get; set; }

        /// <summary>
        /// 当前语言
        /// </summary>
        public string CurrentLanguage { get; private set; }

        public LocalizationManager() : this("zh-CN", "zh-CN")
        {
        }

        public LocalizationManager(string currentLanguage) : this(currentLanguage, "zh-CN")
        {
        }

        public LocalizationManager(string currentLanguage, string defaultLanguage)
        {
            Console.WriteLine("Initializing LocalizationManager...");
            _localizedTexts = new Dictionary<string, Dictionary<string, string>>();
            CurrentLanguage = currentLanguage;
            DefaultLanguage = defaultLanguage;
        }

        /// <summary>
        /// 加载本地化文件
        /// </summary>
        /// <param name="language">语言代码</param>
        /// <param name="localizationTexts"></param>
        public void LoadLocalization(string language, Dictionary<string, string> localizationTexts)
        {
            if (localizationTexts.Count == 0)
            {
                Console.WriteLine($"Warning: localization texts for {language} are null");
                return;
            }

            _localizedTexts[language] = localizationTexts;
            Console.WriteLine($"Loaded {language} language, {localizationTexts.Count} localization texts");
        }

        /// <summary>
        /// 切换当前语言
        /// </summary>
        /// <param name="language">语言代码</param>
        public void SwitchLanguage(string language)
        {
            if (!_localizedTexts.ContainsKey(language))
            {
                Console.WriteLine($"Warning: language {language} not loaded, using empty dictionary");
                _localizedTexts[language] = new Dictionary<string, string>();
            }
            CurrentLanguage = language;
            Console.WriteLine($"Switched to {language} language");
        }

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        /// <param name="key">本地化键</param>
        /// <returns>本地化文本，如果找不到则返回键名</returns>
        public string GetText(string key)
        {
            if (_localizedTexts.TryGetValue(CurrentLanguage, out var texts) &&
                texts.TryGetValue(key, out string? text))
            {
                return text;
            }

            // 如果在当前语言中找不到，尝试在默认语言中查找
            if (CurrentLanguage != DefaultLanguage &&
                _localizedTexts.TryGetValue(DefaultLanguage, out var defaultTexts) &&
                defaultTexts.TryGetValue(key, out string? defaultText))
            {
                return defaultText;
            }

            return key; // 如果找不到，返回键名
        }

        /// <summary>
        /// 添加或更新本地化文本
        /// </summary>
        /// <param name="language">语言代码</param>
        /// <param name="key">本地化键</param>
        /// <param name="text">本地化文本</param>
        public void SetText(string language, string key, string text)
        {
            if (!_localizedTexts.ContainsKey(language))
            {
                _localizedTexts[language] = new Dictionary<string, string>();
            }
            _localizedTexts[language][key] = text;
        }

        /// <summary>
        /// 获取指定语言的本地化文本
        /// </summary>
        /// <param name="language">语言代码</param>
        /// <returns>本地化文本字典</returns>
        public Dictionary<string, string> GetLocalizationTexts(string language)
        {
            return _localizedTexts[language];
        }

        /// <summary>
        /// 获取所有语言的本地化文本
        /// </summary>
        /// <returns>所有语言的本地化文本字典</returns>
        public Dictionary<string, Dictionary<string, string>> GetAllLocalizationTexts()
        {
            return _localizedTexts;
        }

        /// <summary>
        /// 自动收集已加载对话节点中的本地化文本并添加到指定语言
        /// </summary>
        /// <param name="nodes">已加载的对话节点</param>
        /// <param name="language">目标语言</param>
        public void CollectLocalizationTextsFromNodes(Dictionary<string, DialogueNode> nodes, string language)
        {
            var texts = ExtractLocalizationTextsFromNodes(nodes);

            // 添加到指定语言
            foreach (var pair in texts)
            {
                // 只添加不存在的键
                if (!_localizedTexts.ContainsKey(language) ||
                    !_localizedTexts[language].ContainsKey(pair.Key))
                {
                    SetText(language, pair.Key, pair.Value);
                }
            }
        }

        /// <summary>
        /// 从已加载的对话节点中提取需要本地化的文本
        /// </summary>
        /// <param name="nodes">已加载的对话节点</param>
        /// <returns>提取的本地化文本字典（键值对）</returns>
        public Dictionary<string, string> ExtractLocalizationTextsFromNodes(Dictionary<string, DialogueNode> nodes)
        {
            var result = new Dictionary<string, string>();

            foreach (var node in nodes.Values)
            {
                // 处理节点中的所有对话块
                foreach (var block in node.Blocks)
                {
                    // 处理对话文本
                    if (!string.IsNullOrEmpty(block.Text) && !block.Text.StartsWith("#"))
                    {
                        string key = GenerateLocalizationKey(block.Text, node.Name);
                        result[key] = block.Text;

                        // 提取文本中的插值表达式中的本地化键
                        ExtractKeysFromInterpolation(block.Text, result);
                    }

                    // 处理选项文本
                    foreach (var option in block.Options)
                    {
                        if (!string.IsNullOrEmpty(option.Text) && !option.Text.StartsWith("#"))
                        {
                            string key = $"choice_{GenerateLocalizationKey(option.Text, node.Name)}";
                            result[key] = option.Text;

                            // 提取文本中的插值表达式中的本地化键
                            ExtractKeysFromInterpolation(option.Text, result);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从文本中提取插值表达式中的本地化键
        /// </summary>
        /// <param name="text">要处理的文本</param>
        /// <param name="result">结果字典</param>
        private void ExtractKeysFromInterpolation(string text, Dictionary<string, string> result)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 提取插值表达式中的本地化键
            // 支持三种格式：l('key')、l("key")和l(key)
            var matches = ScriptPatterns.LocalizationKeyPattern.Matches(text);

            foreach (Match match in matches)
            {
                // 获取匹配的键（三种格式中的一种）
                string key = match.Groups["key1"].Success ? match.Groups["key1"].Value :
                    match.Groups["key2"].Success ? match.Groups["key2"].Value :
                    match.Groups["key3"].Value.Trim();

                if (!string.IsNullOrEmpty(key) &&
                    !result.ContainsKey(key) &&
                    (!_localizedTexts.ContainsKey(CurrentLanguage) ||
                     !_localizedTexts[CurrentLanguage].ContainsKey(key)))
                {
                    // 如果键不存在于结果集和当前语言的本地化文本中，添加一个空值
                    result[key] = $"[Not translated: {key}]";
                }
            }
        }

        /// <summary>
        /// 生成本地化键
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <param name="context">上下文（如节点名称）</param>
        /// <returns>生成的本地化键</returns>
        private string GenerateLocalizationKey(string text, string context)
        {
            if (string.IsNullOrEmpty(text))
                return "empty_text";

            // 生成一个更有意义的键
            // 1. 取文本的前几个字符作为前缀
            string prefix;
            if (text.Length > 10)
            {
                // 使用正则表达式一次性替换所有标点符号和空白字符
                prefix = ScriptPatterns.PunctuationPattern.Replace(text.Substring(0, 10).ToLower(), "");

                // 如果替换后为空，使用一个默认值
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = "text";
                }
            }
            else
            {
                // 使用正则表达式一次性替换所有标点符号和空白字符
                prefix = ScriptPatterns.PunctuationPattern.Replace(text.ToLower(), "");

                // 如果替换后为空，使用一个默认值
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = "text";
                }
            }

            // 2. 添加哈希值以确保唯一性
            int hash = text.GetHashCode();
            string hashStr = Math.Abs(hash).ToString("X8");

            // 组合键名
            return $"text_{context}_{prefix}_{hashStr}";
        }

    }
}
