using UnityEngine;

public class AnimalInteraction : MonoBehaviour
{
    [SerializeField] private string animalID;
    [SerializeField] private IssueUI issueUI;

    void Awake()
    {
        if (string.IsNullOrEmpty(animalID))
        {
            animalID = gameObject.name;
        }

        if (issueUI == null)
        {
            issueUI = Object.FindFirstObjectByType<IssueUI>();
        }
    }

    void OnMouseDown()
    {
        // 씬 내의 EncyclopediaManager를 찾습니다.
        var manager = Object.FindAnyObjectByType<EncyclopediaManager>();

        // 잠금 해제 여부 확인
        // 만약 도감 씬이 아니라서 매니저가 없다면 기본적으로 보여주거나, 
        // 도감 씬에서만 작동하게 하려면 manager != null 조건을 추가합니다.
        bool canShow = (manager == null) || manager.IsUnlocked(animalID);

        if (canShow)
        {
            if (issueUI != null)
            {
                Debug.Log($"Animal '{animalID}' clicked. Showing report.");
                issueUI.ShowReport(animalID);
            }
        }
        else
        {
            Debug.Log($"Animal '{animalID}' is still locked. Report hidden.");
        }
    }
}