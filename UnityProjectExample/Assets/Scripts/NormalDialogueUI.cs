using MookStoryScript;
using UnityEngine;
using UnityEngine.UI;
using Logger = MookStoryScript.Logger;

public class NormalDialogueUI : MonoBehaviour
{
    public GameObject dialogueUI;
    public Text speakerText;
    public Text contentText;

    public Transform optionContainer;
    public Button optionPrefab;

    public GameObject btnRoot;
    public InputField inputField;
    public Button nDialogueBtn;

    private Button _clickHandler;

    // Start is called before the first frame update
    void Start()
    {
        // 初始化点击处理
        _clickHandler = dialogueUI.GetComponent<Button>();
        if (_clickHandler == null)
        {
            _clickHandler = dialogueUI.AddComponent<Button>();
        }
        _clickHandler.onClick.AddListener(DialogueMgr.Instance.Runners.ContinueSay);

        DialogueMgr.Instance.Runners.OnDialogueStarted += OnDialogueStarted;
        DialogueMgr.Instance.Runners.OnDialogueUpdated += OnDialogueUpdated;
        DialogueMgr.Instance.Runners.OnOptionSelected += OnOptionSelected;
        DialogueMgr.Instance.Runners.OnDialogueCompleted += OnDialogueCompleted;
        
        nDialogueBtn.onClick.AddListener(OnNormalClickDialogue);
        
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
            speakerText.text = !string.IsNullOrEmpty(block.Speaker) ? block.Speaker : "";
            contentText.text = block.Text;
        }

        for (int i = 0; i < optionContainer.childCount; i++)
        {
            Destroy(optionContainer.GetChild(i).gameObject);
        }

        if (block.Options.Count <= 0) return;
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

    public void OnNormalClickDialogue()
    {
        btnRoot.SetActive(false);
        dialogueUI.SetActive(true);
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
