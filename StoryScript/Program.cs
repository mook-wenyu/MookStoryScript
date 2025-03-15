/*using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MookStoryScript
{
    class Program
    {
        static DialogueManager _manager = new();
        static void Main(string[] args)
        {
            _manager.OnDialogueStarted += () =>
            {
                Console.WriteLine($"对话开始");
            };
            _manager.OnNodeStarted += (sender) =>
            {
                Console.WriteLine($"节点开始: {sender.Name}");
            };
            _manager.OnDialogueUpdated += (sender) =>
            {
                Console.WriteLine($"{sender.Speaker}: {sender.Text}");
                for (int i = 0; i < sender.Options.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {sender.Options[i].Text}");
                }
            };
            _manager.OnOptionSelected += (sender) =>
            {
                Console.WriteLine($"选项选择: {sender + 1}. {_manager.CurrentBlock?.Options[sender].Text}");
            };
            _manager.OnDialogueCompleted += () =>
            {
                Console.WriteLine($"对话结束");
            };

            _ = LoadStoryScript();

            while (true)
            {
                string? r = Console.ReadLine();
                if (r == "q")
                {
                    break;
                }
                else if (string.IsNullOrEmpty(r))
                {
                    _manager.ContinueSay();
                }
                else
                {
                    if (int.TryParse(r, out int choiceIndex))
                    {
                        choiceIndex -= 1;
                        _manager.SelectOption(choiceIndex);
                    }
                    else
                    {
                        Console.WriteLine("输入无效");
                    }
                }
            }

            _manager.VariableManagers.SaveVariables(Directory.GetCurrentDirectory() + "/variables.json");
        }


        private static async Task LoadStoryScript()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _manager.VariableManagers.LoadVariables(Directory.GetCurrentDirectory() + "/variables.json");
            await _manager.StoryLoader();

            stopwatch.Stop();
            Console.WriteLine($"解析完成，耗时: {stopwatch.ElapsedMilliseconds}ms");

            await _manager.CollectAndSaveLocalizationTextsFromLoadedNodes("zh-CN");

            ParsedStoryDialogue(_manager.StoryDialogueNodes);

            _manager.Say("start");

        }

        private static void ParsedStoryDialogue(Dictionary<string, DialogueNode> nodes)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = new List<JsonConverter>
                {
                    new EnumToStringConverter()
                }
            };
            string json = JsonConvert.SerializeObject(nodes, settings);
            Console.WriteLine($"解析后的对话数据: \n{json}");
        }

        private class EnumToStringConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType.IsEnum;
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value is Enum enumValue)
                {
                    writer.WriteValue(Enum.GetName(enumValue.GetType(), enumValue));
                }
                else
                {
                    serializer.Serialize(writer, value);
                }
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException("不支持从字符串转换回枚举");
            }
        }
    }
}
*/