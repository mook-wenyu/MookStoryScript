using DefaultNamespace;
using MookStoryScript;
using UnityEngine;

public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }
    public DialogueManager DialogueMgrs { get; private set; }

    void Awake()
    {
        Instance = this;
        StoryScript.Logger.SetLogger(new UnityLogger());
        Initialize();
    }
    
    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        DialogueMgrs = new DialogueManager(new UnityDialogueLoader());
    }
    
}
