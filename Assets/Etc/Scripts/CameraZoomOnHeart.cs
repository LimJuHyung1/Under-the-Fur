using UnityEngine;

public class CameraZoomOnHeart : MonoBehaviour
{
    [SerializeField] private SessionManager session;
    private Camera targetCamera;

    [Header("호감 1회당 줄어드는 Size (작을수록 더 확대)")]
    [SerializeField] private float step = 0.5f;

    [Header("최소 Size (너무 작아지지 않게 제한)")]
    [SerializeField] private float minSize = 1.0f;

    [Header("다음 동물 시작 시 기본 Size")]
    [SerializeField] private float defaultSize = 3.0f;

    [Header("줌 보간 속도 (클수록 빨리 따라감)")]
    [SerializeField] private float lerpSpeed = 8f;

    private float targetSize;

    private void Awake()
    {
        if (session == null)
            session = FindFirstObjectByType<SessionManager>();

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        targetSize = defaultSize;
    }

    private void OnEnable()
    {
        if (session == null)
            session = FindFirstObjectByType<SessionManager>();

        if (session != null)
        {
            session.OnHeartOpenGained += OnHeart;
            session.OnSessionProgressChanged += OnProgressChanged;
        }
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.OnHeartOpenGained -= OnHeart;
            session.OnSessionProgressChanged -= OnProgressChanged;
        }
    }

    private void Update()
    {
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        float current = targetCamera.orthographicSize;
        float next = Mathf.Lerp(current, targetSize, Time.deltaTime * lerpSpeed);

        // 너무 미세하게 계속 움직이는 것 방지
        if (Mathf.Abs(next - targetSize) < 0.001f)
            next = targetSize;

        targetCamera.orthographicSize = next;
    }

    public void ResetToDefault()
    {
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        targetSize = defaultSize;
        targetCamera.orthographicSize = defaultSize;

        // 추가: 카메라 리셋 후 배경을 다시 화면에 맞춤
        var fitter = FindFirstObjectByType<BackgroundFitToCamera>();
        if (fitter != null)
            fitter.FitNow();
    }


    private void OnProgressChanged(int currentTurn, int maxTurns, int heartOpenCount)
    {
        // 새 동물 세션 시작 직후 (0, max, 0) 이벤트가 오면 기본 줌으로 복구
        if (currentTurn == 0)
            ResetToDefault();
    }

    private void OnHeart(int _)
    {
        if (targetCamera == null) return;
        if (!targetCamera.orthographic) return;

        // 누적 줌인: 현재 목표값에서 step만큼 더 줌인
        targetSize = Mathf.Max(minSize, targetSize - step);
    }
}
