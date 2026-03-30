using UnityEngine;
using UnityEngine.UI;

public class AnimalNameLabel : MonoBehaviour
{
    [Header("대상 Text (비우면 자식에서 자동 탐색)")]
    [SerializeField] private Text targetText;

    [Header("표시 우선순위")]
    [SerializeField] private bool preferSpeciesDisplayName = true;

    [Header("이름을 못 찾았을 때")]
    [SerializeField] private string fallbackText = "";

    [Header("비활성 자식 포함 탐색")]
    [SerializeField] private bool includeInactive = true;

    private AnimalAgent agent;

    private void Awake()
    {
        agent = GetComponent<AnimalAgent>();
        if (agent == null)
            agent = GetComponentInChildren<AnimalAgent>(true);

        if (targetText == null)
            targetText = GetComponentInChildren<Text>(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetText == null)
            targetText = FindTextInChildren(true);
    }
#endif

    // 다른 스크립트에서 호출하는 용도
    public void RefreshName()
    {
        if (targetText == null)
            targetText = FindTextInChildren(includeInactive);

        if (targetText == null)
            return;

        string nameToShow = ResolveName();
        targetText.text = string.IsNullOrWhiteSpace(nameToShow) ? fallbackText : nameToShow;
    }

    // 필요하면 외부에서 Text를 직접 지정 가능
    public void SetTargetText(Text text, bool refreshNow = true)
    {
        targetText = text;
        if (refreshNow) RefreshName();
    }

    private string ResolveName()
    {
        // 1) AnimalAgent의 Species.displayName 우선
        if (preferSpeciesDisplayName && agent != null && agent.Species != null)
        {
            string displayName = agent.Species.displayName;
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;
        }

        // 2) 없으면 동물 오브젝트 이름
        return gameObject.name;
    }

    private Text FindTextInChildren(bool includeInactiveChildren)
    {
        // 자기 자신(동물 오브젝트)에 Text가 붙어있을 일은 거의 없지만,
        // 혹시 모르니 자식 포함으로 찾는다
        return GetComponentInChildren<Text>(includeInactiveChildren);
    }
}
