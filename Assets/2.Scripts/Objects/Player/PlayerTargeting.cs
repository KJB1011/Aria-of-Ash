// ============================================================================
// PlayerTargeting.cs
// ----------------------------------------------------------------------------
// 플레이어 주변의 작은 탐지 범위(detectRange) 안에서 가장 가까운 적을 자동으로 찾아
// CurrentTarget으로 들고 있는 컴포넌트입니다. PlayerController가 기본 공격/스킬을
// 시작할 때 이 컴포넌트에게 "지금 가장 가까운 적이 어느 방향에 있는지" 물어봐서
// 그 방향으로 캐릭터를 스냅 회전시키는 데 사용됩니다 (원신 등에서 흔한, 근처에 적이
// 있으면 공격이 자동으로 그쪽을 향하는 소프트 락온 방식).
//
// [씬 준비]
//   1) Player 오브젝트(혹은 PlayerController가 붙은 오브젝트)에 이 스크립트를 추가하세요.
//   2) Enemy Mask에 몬스터들이 속한 레이어를 지정하세요.
//   3) PlayerController의 Targeting 필드가 비어있으면 자동으로 같은 오브젝트에서 찾습니다.
// ============================================================================

using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    [Header("탐지")]
    [Tooltip("이 범위 안에 있는 적 중 가장 가까운 대상을 자동으로 타겟팅합니다. 락온용이라 보통 작게 잡습니다.")]
    public float detectRange = 5f;
    [Tooltip("적으로 판정할 레이어. 몬스터들이 속한 레이어를 지정하세요.")]
    public LayerMask enemyMask;
    [Tooltip("몇 초 간격으로 주변을 다시 스캔할지. 너무 짧으면 매 프레임 스캔하는 것과 비슷해지고, " +
              "너무 길면 적이 움직였을 때 타겟이 늦게 갱신됩니다.")]
    public float scanInterval = 0.1f;
    [Tooltip("한 번에 감지할 수 있는 최대 콜라이더 수 (버퍼 크기). 좁은 탐지 범위에서 왕왕 몰려도 " +
              "충분하도록 여유 있게 잡아뒀습니다.")]
    public int maxDetections = 16;

    /// <summary>지금 가장 가까운 적. 범위 안에 아무도 없으면 null입니다.</summary>
    public Transform CurrentTarget { get; private set; }

    private float scanTimer;
    private Collider[] scanBuffer;

    private void Awake()
    {
        scanBuffer = new Collider[Mathf.Max(1, maxDetections)];
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;

        scanTimer = scanInterval;
        Rescan();
    }

    private void Rescan()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, scanBuffer, enemyMask);

        Transform nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = scanBuffer[i];
            if (col == null) continue;

            float sqrDist = (col.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = col.transform;
            }
        }

        CurrentTarget = nearest;
    }

    /// <summary>CurrentTarget 방향(수평 성분만, 정규화됨)을 돌려줍니다. 타겟이 없으면 Vector3.zero.</summary>
    public Vector3 GetDirectionToTarget(Vector3 fromPosition)
    {
        if (CurrentTarget == null) return Vector3.zero;

        Vector3 direction = CurrentTarget.position - fromPosition;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        if (CurrentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, CurrentTarget.position);
        }
    }
}