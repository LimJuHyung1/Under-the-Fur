using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFitToCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool fitWidth = true;
    [SerializeField] private bool fitHeight = true;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        FitNow();
    }

    [ContextMenu("Fit Now")]
    public void FitNow()
    {
        if (targetCamera == null) return;

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        // 카메라가 보여주는 월드 가로/세로 크기 (Orthographic 기준)
        float worldHeight = targetCamera.orthographicSize * 2f;
        float worldWidth = worldHeight * targetCamera.aspect;

        // 현재 스프라이트의 월드 크기(스케일 1 기준)
        Vector2 spriteSize = sr.sprite.bounds.size; // 월드 유닛

        Vector3 scale = transform.localScale;

        if (fitWidth && spriteSize.x > 0f)
            scale.x = worldWidth / spriteSize.x;

        if (fitHeight && spriteSize.y > 0f)
            scale.y = worldHeight / spriteSize.y;

        transform.localScale = scale;

        // 카메라 정중앙에 배경을 두고 싶으면 (선택)
        Vector3 p = transform.position;
        p.x = targetCamera.transform.position.x;
        p.y = targetCamera.transform.position.y;
        transform.position = p;
    }
}
