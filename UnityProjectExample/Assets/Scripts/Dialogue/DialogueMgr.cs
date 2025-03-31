using MookStoryScript;
using UnityEngine;
using Logger = MookStoryScript.Logger;

public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }
    public Runner Runners { get; private set; }

    void Awake()
    {
        Instance = this;
        Logger.SetLogger(new UnityLogger());
        Initialize();
    }
    
    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        Runners = new Runner(new UnityDialogueLoader());
    }
    
}
