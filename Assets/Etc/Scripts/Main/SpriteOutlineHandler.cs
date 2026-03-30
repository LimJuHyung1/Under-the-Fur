using UnityEngine;

public class SpriteOutlineHandler : MonoBehaviour
{
    [Header("머티리얼 설정")]
    [SerializeField] private Material outlineMaterial; // 위에서 만든 M_AnimalOutline

    private Material originalMaterial;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // AnimalAgent의 구조를 고려하여 자식에서 SpriteRenderer를 찾습니다.
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
        }
    }

    private void OnMouseEnter()
    {
        if (spriteRenderer != null && outlineMaterial != null)
        {
            spriteRenderer.material = outlineMaterial;
        }
    }

    private void OnMouseExit()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.material = originalMaterial;
        }
    }
}