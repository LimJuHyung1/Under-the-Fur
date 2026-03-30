using UnityEngine;
using UnityEngine.UI;

public class IssueUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject issuePanel;
    public Image screen;
    public Image portraitImage;
    public Text nameText;
    public Text titleText;
    public Text descriptionText;


    public void ShowReport(string animalID)
    {
        // 1. 데이터 가져오기 및 패널 활성화
        var data = IssueDataManager.Instance.GetIssue(animalID);
        if (issuePanel != null) issuePanel.SetActive(true);
        if (screen != null) screen.gameObject.SetActive(true);

        // 2. 데이터 텍스트 연결 (애니메이션 이전에 배치하여 정보 누락 방지)
        if (!string.IsNullOrEmpty(data.id))
        {
            if (nameText != null) nameText.text = data.displayName;
            if (titleText != null) titleText.text = data.issueTitle;
            if (descriptionText != null) descriptionText.text = data.description;

            Sprite portrait = Resources.Load<Sprite>($"Portraits/{animalID}");
            if (portrait != null && portraitImage != null) portraitImage.sprite = portrait;
        }
        else
        {
            Debug.LogWarning($"ID '{animalID}'에 해당하는 데이터를 찾을 수 없습니다.");
        }
    }

    public void CloseReport()
    {
               if (issuePanel != null) issuePanel.SetActive(false);
        if (screen != null) screen.gameObject.SetActive(false);
    }
}