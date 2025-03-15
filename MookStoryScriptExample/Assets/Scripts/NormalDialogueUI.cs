using System;
using System.Collections;
using System.Collections.Generic;
using MookStoryScript;
using UnityEngine;
using UnityEngine.UI;

public class NormalDialogueUI : MonoBehaviour
{
    public GameObject normalDialogue;
    public Text speakerText;
    public Text contentText;

    public Transform optionContainer;
    public Button optionPrefab;

    public GameObject btnRoot;
    public InputField inputField;

    private Button _clickHandler;

    // Start is called before the first frame update
    void Start()
    {
        // 初始化点击处理
        _clickHandler = normalDialogue.GetComponent<Button>();
        if (_clickHandler == null)
        {
            _clickHandler = normalDialogue.AddComponent<Button>();
        }
        _clickHandler.onClick.AddListener(DialogueMgr.Instance.DialogueMgrs.ContinueSay);

        DialogueMgr.Instance.DialogueMgrs.OnDialogueStarted += OnDialogueStarted;
        DialogueMgr.Instance.DialogueMgrs.OnDialogueUpdated += OnDialogueUpdated;
        DialogueMgr.Instance.DialogueMgrs.OnOptionSelected += OnOptionSelected;
        DialogueMgr.Instance.DialogueMgrs.OnDialogueCompleted += OnDialogueCompleted;
    }

    private void OnDialogueStarted()
    {
        Debug.Log("对话开始");
        normalDialogue.SetActive(true);
    }

    private void OnDialogueUpdated(DialogueBlock block)
    {
        speakerText.text = !string.IsNullOrEmpty(block.Speaker) ? block.Speaker : "";
        contentText.text = block.Text;
        
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
                DialogueMgr.Instance.DialogueMgrs.SelectOption(block.Options.IndexOf(option));
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
        normalDialogue.SetActive(false);
        btnRoot.SetActive(true);
    }

    public void OnNormalClickDialogue()
    {
        btnRoot.SetActive(false);
        DialogueMgr.Instance.DialogueMgrs.Say(inputField.text.Trim());
    }

    private void OnDestroy()
    {
        DialogueMgr.Instance.DialogueMgrs.OnDialogueStarted -= OnDialogueStarted;
        DialogueMgr.Instance.DialogueMgrs.OnDialogueUpdated -= OnDialogueUpdated;
        DialogueMgr.Instance.DialogueMgrs.OnOptionSelected -= OnOptionSelected;
        DialogueMgr.Instance.DialogueMgrs.OnDialogueCompleted -= OnDialogueCompleted;
    }
}
