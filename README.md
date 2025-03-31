# MookStoryScript

MookStoryScript 是一个轻量级、易用的对话脚本系统，专为游戏、互动小说和视觉小说设计。它提供了简单直观的语法，让创作者能够轻松编写分支对话和互动故事。

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

## 特性

- **简单直观的语法**：易于学习和使用，无需编程经验
- **丰富的对话功能**：支持角色对话、表情、条件分支和选项
- **变量系统**：可以定义和操作变量，实现游戏状态追踪
- **条件逻辑**：支持 if/elif/else 条件结构，实现复杂的故事分支
- **函数调用**：可以调用外部函数，与游戏系统交互
- **本地化支持**：内置多语言支持，便于国际化
- **事件系统**：提供丰富的事件回调，方便与游戏引擎集成
- **属性注册**：支持通过特性(Attribute)自动注册变量和函数
- **内置对象**：支持注册复杂对象，提供更丰富的交互能力
- **自定义加载器**：支持自定义脚本加载方式，灵活适应不同项目需求

## 安装

### 手动安装

1. 克隆仓库
```bash
git clone https://github.com/mook-wenyu/MookStoryScript.git
```

2. 使用方式：
   - **项目引用**：直接添加 MookStoryScript 项目引用到你的解决方案中
   - **源码复制**：将 StoryScript 文件夹复制到你的项目中
   - **DLL引用**：将编译后的 DLL 添加到你的项目引用中

## Unity项目集成

### 基本设置

1. 引入 MookStoryScript
   - 方法一：将 StoryScript 文件夹复制到你的 Unity 项目的 Scripts 文件夹中
   - 方法二：将编译后的 DLL 添加到 Unity 项目的 Plugins 文件夹
2. 创建一个 DialogueManager 的包装类

请查看代码文件：[`DialogueMgr.cs`](UnityProjectExample/Assets/Scripts/Dialogue/DialogueMgr.cs)

### 保存和加载进度

```csharp
// 保存游戏进度
public void SaveGame(string saveName)
{
    var saveData = new SaveData
    {
        DialogueProgress = _dialogueManager.DialogueProgresses,
        Variables = _dialogueManager.VariableManagers.GetVariables()
    };
    
    string json = JsonUtility.ToJson(saveData);
    PlayerPrefs.SetString("Save_" + saveName, json);
    PlayerPrefs.Save();
}

// 加载游戏进度
public void LoadGame(string saveName)
{
    if (PlayerPrefs.HasKey("Save_" + saveName))
    {
        string json = PlayerPrefs.GetString("Save_" + saveName);
        var saveData = JsonUtility.FromJson<SaveData>(json);
        
        _dialogueManager.LoadProgress(saveData.DialogueProgress);
        _dialogueManager.VariableManagers.LoadVariables(saveData.Variables);
    }
}

// 保存数据结构
[System.Serializable]
public class SaveData
{
    public DialogueProgress DialogueProgress;
    public Dictionary<string, object> Variables;
}
```

## 脚本加载

MookStoryScript 使用加载器自动加载脚本文件。默认情况下，DialogueManager 会使用内置的加载器，但您也可以实现自定义加载器。

### 自定义加载器

您可以通过实现 `IDialogueLoader` 接口创建自定义加载器，以支持从数据库、网络或其他来源加载脚本。

请查看代码文件：[`UnityDialogueLoader.cs`](UnityProjectExample/Assets/Scripts/Dialogue/UnityDialogueLoader.cs)

## 脚本语法

请查看对话脚本文件：[`basic_example.txt`](UnityProjectExample/Assets/Resources/Story/basic_example.txt)

### 节点定义

节点是对话脚本的基本单位，使用 `::` 前缀定义：

```
:: node_name
这里是节点内容
```

节点名称可以包含字母、数字和下划线。推荐使用下划线命名法（如 `node_name`）。

### 基本对话

```
// 无说话者的旁白
这是一段旁白文本，没有说话者。

// 有说话者的对话
角色名: 这是一段有说话者的对话。

// 带表情的对话
角色名[高兴]: 这是一段带表情的对话。
```

### 变量系统

```
// 定义和设置变量
var player_score 0           // 定义整数变量
var player_name "玩家"       // 定义字符串变量
var is_hero true      // 定义布尔变量

// 设置变量值
set player_score 100         // 设置值
add player_score 10          // 增加值
sub player_score 5           // 减少值

// 在对话中使用变量
角色: 你好，{player_name}！你的得分是{player_score}分！
```

### 条件语句

```
// 基本条件
if player_score > 80
    角色: 太棒了！你的得分很高！
endif

// 多分支条件
if player_score > 80
    角色: 太棒了！你的得分很高！
elif player_score > 60
    角色: 不错，继续努力！
else
    角色: 加油，你可以做得更好！
endif

// 条件运算符
// 支持: ==, !=, >, <, >=, <=, &&(和), ||(或), !(非)
if (player_score > 50) && (is_hero == true)
    角色: 你是一位强大的勇者！
endif
```

### 选项系统

```
// 基本选项
角色: 你想做什么？
-> 去村庄
    角色: 你决定去村庄。
    => village_area
-> 去森林 [if has_map]
    角色: 你决定去森林。
    => forest_area

// 嵌套选项
-> 与商人交谈
    商人: 你好，旅行者！需要买些什么吗？
    -> 购买武器
        sub player_gold 50
        商人: 这把剑很适合你！
    -> 购买药水
        sub player_gold 20
        商人: 药水已经装好了，祝你好运！
    -> 离开
        商人: 欢迎下次光临！
```

### 跳转命令

```
// 基本跳转
=> town_center

// 条件跳转
if player_score > 80
    => high_score_node
else
    => low_score_node
endif
```

### 函数调用

```
// 基本函数调用
call play_sound("explosion")

// 带参数的函数调用
call add_item("剑", 1)

// 使用返回值
set damage calculate_damage(player_strength, enemy_defense)
角色: 你造成了{damage}点伤害！
```

### 等待命令

等待命令用于控制对话节奏：

```
// 等待指定秒数
wait 2.5

// 等待动画完成
call play_animation("角色", "跳跃")
wait 1.5  // 等待动画播放完成
```

### 注释

注释用于添加说明，不会影响脚本执行：

```
// 这是一行注释
角色: 你好！
```

### 访问对象属性

```
// 在对话中使用对象属性
角色: 你当前有{game.player_gold}金币，生命值是{game.player_health}。

// 在条件中使用对象属性
if game.player_health < 30
    角色: 你的生命值很低，需要休息！
endif

// 修改对象属性
add game.player_gold 50
```

## 许可证

本项目采用 Apache License 2.0 许可证 - 详情请查看 [LICENSE](LICENSE.txt) 文件。

## 联系方式

- GitHub Issues: [提交问题](https://github.com/mook-wenyu/MookStoryScript/issues)
- Email: 1317578863@qq.com