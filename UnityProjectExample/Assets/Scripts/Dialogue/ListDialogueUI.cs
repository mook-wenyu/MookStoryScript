using System.Text;
using MookStoryScript;
using UnityEngine;
using UnityEngine.UI;
using Logger = MookStoryScript.Logger;

public class ListDialogueUI : MonoBehaviour
{
    public GameObject dialogueUI;
    
    public ScrollRect dialogueContainer;
    public GameObject dialoguePrefab;
    
    public Transform optionContainer;
    public Button optionPrefab;
    
    public GameObject btnRoot;
    public InputField inputField;
    public Button lDialogueBtn;

    private Button _clickHandler;
    
    // Start is called before the first frame update
    void Start()
    {
        // 初始化点击处理
        /*
        _clickHandler = dialogueUI.GetComponent<Button>();
        if (_clickHandler == null)
        {
            _clickHandler = dialogueUI.AddComponent<Button>();
        }
        _clickHandler.onClick.AddListener(DialogueMgr.Instance.DialogueMgrs.ContinueSay);
        */

        DialogueMgr.Instance.Runners.OnDialogueStarted += OnDialogueStarted;
        DialogueMgr.Instance.Runners.OnDialogueUpdated += OnDialogueUpdated;
        DialogueMgr.Instance.Runners.OnOptionSelected += OnOptionSelected;
        DialogueMgr.Instance.Runners.OnDialogueCompleted += OnDialogueCompleted;
        
        lDialogueBtn.onClick.AddListener(OnListClickDialogue);
        
        dialogueUI.SetActive(false);
    }

    private void OnDialogueStarted()
    {
        Debug.Log("对话开始");
        
    }

    private void OnDialogueUpdated(DialogueBlock block)
    {
        if (!string.IsNullOrEmpty(block.Text))
        {
            var go = Instantiate(dialoguePrefab, dialogueContainer.content);
            Text contentText = go.GetComponent<Text>();
                
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(block.Speaker))
            {
                sb.Append(block.Speaker);
                sb.Append(" - ");
            }
            sb.Append(block.Text);
            contentText.text = sb.ToString();
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentText.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(dialogueContainer.content.GetComponent<RectTransform>());
            
            Canvas.ForceUpdateCanvases();
            dialogueContainer.verticalNormalizedPosition = 0f;
        }

        for (int i = 0; i < optionContainer.childCount; i++)
        {
            Destroy(optionContainer.GetChild(i).gameObject);
        }

        if (block.Options.Count <= 0)
        {
            var go = Instantiate(optionPrefab, optionContainer);
            go.GetComponentInChildren<Text>().text = "继续";
            go.onClick.AddListener(() =>
            {
                DialogueMgr.Instance.Runners.ContinueSay();
            });
            return;
        }
        foreach (var option in block.Options)
        {
            var go = Instantiate(optionPrefab, optionContainer);
            go.GetComponentInChildren<Text>().text = option.Text;
            go.onClick.AddListener(() =>
            {
                DialogueMgr.Instance.Runners.SelectOption(block.Options.IndexOf(option));
            });
        }
    }

    private void OnOptionSelected(int index)
    {
        Debug.Log("选择：" + index);
    }

    private void OnDialogueCompleted()
    {
        Debug.Log("对话结束");
        dialogueUI.SetActive(false);
        btnRoot.SetActive(true);
    }

    public void OnListClickDialogue()
    {
        btnRoot.SetActive(false);
        dialogueUI.SetActive(true);
        
        for (int i = 0; i < dialogueContainer.content.childCount; i++)
        {
            Destroy(dialogueContainer.content.GetChild(i).gameObject);
        }
        
        DialogueMgr.Instance.Runners.Say(inputField.text.Trim());
    }

    private void OnDestroy()
    {
        DialogueMgr.Instance.Runners.OnDialogueStarted -= OnDialogueStarted;
        DialogueMgr.Instance.Runners.OnDialogueUpdated -= OnDialogueUpdated;
        DialogueMgr.Instance.Runners.OnOptionSelected -= OnOptionSelected;
        DialogueMgr.Instance.Runners.OnDialogueCompleted -= OnDialogueCompleted;
    }
}
