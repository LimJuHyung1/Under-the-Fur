using System.Collections.Generic;
using UnityEngine;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class MainSceneAnimalWander : MonoBehaviour
    {
        public enum Direction8
        {
            North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest
        }

        [Header("기본 설정")]
        [SerializeField] private bool disableInputControllersOnStart = true;
        [SerializeField] private Direction8 startFacing = Direction8.North;

        [Header("이동")]
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private float arriveDistance = 0.08f;

        [Header("상태 시간")]
        [SerializeField] private float idleMin = 0.4f;
        [SerializeField] private float idleMax = 1.2f;
        [SerializeField] private float moveMin = 0.8f;
        [SerializeField] private float moveMax = 2.2f;

        [Header("입력 감지 시 정지")]
        [SerializeField] private bool pauseOnAnyInput = true;
        [SerializeField] private float pauseSecondsAfterInput = 0.7f;

        [Header("이동 범위(옵션)")]
        [SerializeField] private Collider2D roamAreaCollider;
        [SerializeField] private float cameraPadding = 0.8f;

        [Header("이동 포인트(옵션)")]
        [SerializeField] private Transform roamPointsRoot;

        [Header("물리 옵션(메인 씬용)")]
        [SerializeField] private bool useKinematicMove = true; // 메인 메뉴면 Kinematic + MovePosition이 더 가벼운 편

        private Rigidbody2D rb;
        private Animator animator;
        private Camera cachedMainCam;

        private float stateTimer;
        private float pauseUntil;

        private Vector2 targetPos;
        private bool isMoving;

        private float arriveDistSqr;

        // 입력 체크를 모든 개체가 각자 하지 않도록 전역 캐싱
        private static int s_lastInputFrame = -1;
        private static float s_globalPauseUntil = 0f;

        // Animator 최적화 캐시
        private HashSet<int> boolParamHashes;

        private int[] dirBoolHash = new int[8];
        private int[] moveBoolHash = new int[8];
        private int isWalkingHash;

        private int currentDirIndex = -1;
        private int currentMoveIndex = -1;

        private static readonly string[] DirSuffix =
        {
            "North","South","East","West","NorthEast","NorthWest","SouthEast","SouthWest"
        };

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponentInChildren<Animator>(true);
            cachedMainCam = Camera.main;

            arriveDistSqr = arriveDistance * arriveDistance;

            if (disableInputControllersOnStart)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;

                var ac = GetComponentInChildren<AnimationController>(true);
                if (ac != null) ac.enabled = false;
            }

            if (useKinematicMove)
                rb.bodyType = RigidbodyType2D.Dynamic;

            BuildAnimatorCache();

            SetFacingOptimized(startFacing);
            SetIdleAnimOptimized();

            EnterIdle(Random.Range(idleMin, idleMax));
        }

        private void Update()
        {
            // Update는 상태 전환 타이머만 처리 (가볍게 유지)

            if (pauseOnAnyInput)
            {
                // 전역 1회만 입력 체크
                if (s_lastInputFrame != Time.frameCount)
                {
                    s_lastInputFrame = Time.frameCount;

                    // anyKey는 매 프레임 true가 될 수 있어서, down 계열 중심으로
                    bool hasInput =
                        Input.anyKeyDown ||
                        Input.GetMouseButtonDown(0) ||
                        Input.GetMouseButtonDown(1);

                    if (hasInput)
                        s_globalPauseUntil = Time.time + pauseSecondsAfterInput;
                }

                pauseUntil = s_globalPauseUntil;
            }

            if (Time.time < pauseUntil)
            {
                if (isMoving)
                {
                    isMoving = false;
                    StopMove();
                    SetIdleAnimOptimized();
                }
                return;
            }

            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            if (isMoving)
            {
                EnterIdle(Random.Range(idleMin, idleMax));
            }
            else
            {
                PickNewTarget();
                EnterMove(Random.Range(moveMin, moveMax));
            }
        }

        private void FixedUpdate()
        {
            if (!isMoving) return;
            if (Time.time < pauseUntil) return;

            Vector2 pos = rb.position;
            Vector2 to = targetPos - pos;

            float distSqr = to.sqrMagnitude;
            if (distSqr <= arriveDistSqr)
            {
                isMoving = false;
                StopMove();
                SetIdleAnimOptimized();
                EnterIdle(Random.Range(idleMin, idleMax));
                return;
            }

            float invDist = 1f / Mathf.Sqrt(distSqr);
            Vector2 dir = to * invDist;

            // 이동
            DoMove(dir);

            // 애니메이션은 "바뀔 때만" 토글됨
            Direction8 d8 = VectorToDir8(dir);
            SetFacingOptimized(d8);
            SetMoveAnimOptimized(d8);
        }

        private void EnterIdle(float seconds)
        {
            isMoving = false;
            StopMove();
            SetIdleAnimOptimized();
            stateTimer = seconds;
        }

        private void EnterMove(float seconds)
        {
            isMoving = true;
            stateTimer = seconds;
        }

        private void PickNewTarget()
        {
            if (roamPointsRoot != null && roamPointsRoot.childCount > 0)
            {
                int idx = Random.Range(0, roamPointsRoot.childCount);
                targetPos = roamPointsRoot.GetChild(idx).position;
                return;
            }

            if (roamAreaCollider != null)
            {
                Bounds b = roamAreaCollider.bounds;
                for (int i = 0; i < 20; i++)
                {
                    float x = Random.Range(b.min.x, b.max.x);
                    float y = Random.Range(b.min.y, b.max.y);
                    Vector2 p = new Vector2(x, y);

                    if (roamAreaCollider.OverlapPoint(p))
                    {
                        targetPos = p;
                        return;
                    }
                }

                targetPos = b.center;
                return;
            }

            Camera cam = cachedMainCam != null ? cachedMainCam : Camera.main;
            if (cam != null)
            {
                Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane));
                Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, cam.nearClipPlane));

                float minX = bl.x + cameraPadding;
                float maxX = tr.x - cameraPadding;
                float minY = bl.y + cameraPadding;
                float maxY = tr.y - cameraPadding;

                targetPos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            }
            else
            {
                Vector2 p = rb.position;
                targetPos = p + Random.insideUnitCircle * 1.5f;
            }
        }

        private void DoMove(Vector2 dir)
        {
            if (useKinematicMove)
            {
                Vector2 next = rb.position + dir * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(next);
            }
            else
            {
                SetRBVelocity(dir * moveSpeed);
            }
        }

        private void StopMove()
        {
            if (useKinematicMove)
            {
                // Kinematic은 velocity 개념이 약해서 별도 처리는 필요 없음
            }
            else
            {
                SetRBVelocity(Vector2.zero);
            }
        }

        private Direction8 VectorToDir8(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= 330f || angle < 15f) return Direction8.East;
            if (angle >= 15f && angle < 60f) return Direction8.NorthEast;
            if (angle >= 60f && angle < 120f) return Direction8.North;
            if (angle >= 120f && angle < 165f) return Direction8.NorthWest;
            if (angle >= 165f && angle < 195f) return Direction8.West;
            if (angle >= 195f && angle < 240f) return Direction8.SouthWest;
            if (angle >= 240f && angle < 300f) return Direction8.South;
            return Direction8.SouthEast;
        }

        private int DirToIndex(Direction8 dir)
        {
            switch (dir)
            {
                case Direction8.North: return 0;
                case Direction8.South: return 1;
                case Direction8.East: return 2;
                case Direction8.West: return 3;
                case Direction8.NorthEast: return 4;
                case Direction8.NorthWest: return 5;
                case Direction8.SouthEast: return 6;
                case Direction8.SouthWest: return 7;
            }
            return 2;
        }

        private void BuildAnimatorCache()
        {
            if (animator == null) return;

            boolParamHashes = new HashSet<int>();
            var ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Bool)
                    boolParamHashes.Add(ps[i].nameHash);
            }

            for (int i = 0; i < 8; i++)
            {
                dirBoolHash[i] = Animator.StringToHash("is" + DirSuffix[i]);
                moveBoolHash[i] = Animator.StringToHash("Move" + DirSuffix[i]);
            }

            isWalkingHash = Animator.StringToHash("isWalking");
        }

        private bool HasBool(int hash)
        {
            return boolParamHashes != null && boolParamHashes.Contains(hash);
        }

        private void SetBool(int hash, bool value)
        {
            if (animator == null) return;
            if (!HasBool(hash)) return;
            animator.SetBool(hash, value);
        }

        private void SetFacingOptimized(Direction8 dir)
        {
            int idx = DirToIndex(dir);
            if (idx == currentDirIndex) return;

            if (currentDirIndex >= 0)
                SetBool(dirBoolHash[currentDirIndex], false);

            currentDirIndex = idx;
            SetBool(dirBoolHash[currentDirIndex], true);
        }

        private void SetIdleAnimOptimized()
        {
            if (currentMoveIndex >= 0)
            {
                SetBool(moveBoolHash[currentMoveIndex], false);
                currentMoveIndex = -1;
            }

            SetBool(isWalkingHash, false);

            if (currentDirIndex >= 0)
                SetBool(dirBoolHash[currentDirIndex], true);
        }

        private void SetMoveAnimOptimized(Direction8 dir)
        {
            int idx = DirToIndex(dir);

            if (currentMoveIndex != idx)
            {
                if (currentMoveIndex >= 0)
                    SetBool(moveBoolHash[currentMoveIndex], false);

                currentMoveIndex = idx;
                SetBool(moveBoolHash[currentMoveIndex], true);
            }

            SetBool(isWalkingHash, true);
        }

        private void SetRBVelocity(Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }
    }
}
