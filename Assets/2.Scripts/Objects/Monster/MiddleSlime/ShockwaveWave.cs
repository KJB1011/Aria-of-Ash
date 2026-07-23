// ============================================================================
// ShockwaveWave.cs
// ----------------------------------------------------------------------------
// 부채꼴 범위를 따라 origin에서 바깥으로 퍼져나가는 "파도" 하나.
// 이동(반지름 확장)과는 별개로, 높이(Y)는 heightCurve를 따라 땅에서 올라왔다가
// 다시 내려가도록 되어 있습니다 (기본값이 정규분포/Normal Graph처럼 봉긋 솟았다 내려가는 모양).
//
// 판정은 Collider/Trigger 없이, 현재 반지름의 호(arc)를 여러 지점으로 나눠서
// Physics.OverlapSphere로 직접 검사합니다. (부채꼴처럼 계속 반지름이 바뀌는 모양을
// 매 프레임 Collider로 다시 만드는 것보다 훨씬 가볍고 다루기 쉽습니다)
//
// [데미지 계산]
// 고정 데미지 대신 sourceStats(발사한 보스의 MonsterStats).CalculateDamage(damagePercent, 맞은
// 플레이어의 방어력)로 계산합니다 - MiddleSlimeBoss.FireWave()가 Launch() 호출 전에 damagePercent/
// sourceStats를 자동으로 채워줍니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShockwaveWave : MonoBehaviour
{
    [Header("피해")]
    [Tooltip("데미지 배율(%). 최종 데미지는 sourceStats.CalculateDamage(damagePercent, 맞은 플레이어의 방어력)로 " +
              "계산됩니다. MiddleSlimeBoss가 발사할 때 자동으로 채워줍니다.")]
    public float damagePercent = 100f;
    [Tooltip("데미지 계산에 쓸 발사자(보스)의 스탯. MiddleSlimeBoss가 발사할 때 자동으로 채워줍니다.")]
    public MonsterStats sourceStats;
    public LayerMask hitMask;
    [Tooltip("데미지가 들어갈 때, 데미지 숫자를 맞은 대상 위로 얼마나 띄울지(미터).")]
    public float damageNumberHeightOffset = 1.2f;
    [Tooltip("데미지가 들어가는 순간 재생할 VFX 이름 (Resources/VFX/ 아래 프리팹 이름과 일치해야 함). " +
              "비워두면 VFX 없이 데미지만 적용됩니다. MiddleSlimeBoss가 발사할 때 자동으로 채워줍니다.")]
    public string hitVfxName;

    [Header("모양")]
    [Tooltip("파도가 지나가는 자리의 폭(반지름 기준 두께)")]
    public float bandThickness = 1f;
    [Tooltip("판정/메쉬를 위해 부채꼴을 몇 개의 각도 지점으로 나눌지")]
    public int segments = 8;
    [Tooltip("각 지점에서 판정할 구체 반지름")]
    public float sampleRadius = 0.6f;

    [Header("높이 (Normal Graph 형태)")]
    [Tooltip("진행도(0~1)에 따른 높이 배율. 기본값은 중간에 봉긋 솟았다가 내려오는 곡선입니다.")]
    public AnimationCurve heightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3f, 0f));
    public float maxHeight = 1.2f;

    private Vector3 origin;
    private Vector3 forward;
    private float halfAngleRad;
    private float maxRadius;
    private float travelDuration;
    private float elapsed;

    private Mesh mesh;
    private readonly HashSet<Collider> alreadyHit = new HashSet<Collider>();

    private void Awake()
    {
        mesh = new Mesh { name = "ShockwaveWave" };
        GetComponent<MeshFilter>().mesh = mesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer.sharedMaterial == null)
        {
            meshRenderer.sharedMaterial = CircleAreaIndicator.CreateDefaultMaterial(new Color(0.3f, 0.6f, 1f, 0.65f));
        }
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    /// <summary>
    /// origin에서 시작해 forwardDirection을 중심으로 좌우 halfAngleDeg만큼 벌어진 부채꼴을 따라,
    /// travelDuration초 동안 반지름 0 → maxRadius까지 퍼져나갑니다.
    /// </summary>
    public void Launch(Vector3 origin, Vector3 forwardDirection, float halfAngleDeg, float maxRadius, float travelDuration)
    {
        this.origin = origin;
        forward = forwardDirection.sqrMagnitude > 0.0001f ? forwardDirection.normalized : Vector3.forward;
        halfAngleRad = halfAngleDeg * Mathf.Deg2Rad;
        this.maxRadius = maxRadius;
        this.travelDuration = Mathf.Max(0.01f, travelDuration);

        elapsed = 0f;
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / travelDuration);

        float currentRadius = Mathf.Lerp(0f, maxRadius, t);
        float height = heightCurve.Evaluate(t) * maxHeight;
        transform.position = origin + Vector3.up * height;

        RebuildMesh(currentRadius);
        CheckHits(currentRadius);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void RebuildMesh(float centerRadius)
    {
        float inner = Mathf.Max(0f, centerRadius - bandThickness * 0.5f);
        float outer = centerRadius + bandThickness * 0.5f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)); // 로컬 +Z가 정면

            vertices[i * 2] = dir * inner;
            vertices[i * 2 + 1] = dir * outer;
        }

        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = i * 2 + 1;
            int c = (i + 1) * 2;
            int d = (i + 1) * 2 + 1;

            triangles[triIndex++] = a;
            triangles[triIndex++] = c;
            triangles[triIndex++] = b;

            triangles[triIndex++] = b;
            triangles[triIndex++] = c;
            triangles[triIndex++] = d;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void CheckHits(float currentRadius)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angleDeg = Mathf.Lerp(-halfAngleRad, halfAngleRad, t) * Mathf.Rad2Deg;
            Vector3 dir = Quaternion.AngleAxis(angleDeg, Vector3.up) * forward;
            Vector3 samplePoint = origin + dir * currentRadius + Vector3.up * 0.5f;

            Collider[] hits = Physics.OverlapSphere(samplePoint, sampleRadius, hitMask);
            foreach (Collider col in hits)
            {
                if (!alreadyHit.Add(col)) continue; // 이미 이 파도에 맞은 대상이면 건너뜀

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    PlayerStats playerStats = col.GetComponentInParent<PlayerStats>();
                    float targetDefense = playerStats != null ? playerStats.TotalDefense : 0f;

                    DamageResult result = sourceStats != null
                        ? sourceStats.CalculateDamage(damagePercent, targetDefense)
                        : default;
                    damageable.TakeDamage(result.damage);

                    Vector3 hitPoint = col.ClosestPoint(samplePoint);

                    if (!string.IsNullOrEmpty(hitVfxName))
                    {
                        VFXManager.Instance.Play(hitVfxName, hitPoint, transform.rotation);
                    }

                    Vector3 numberPosition = hitPoint + Vector3.up * damageNumberHeightOffset;
                    DamageNumberManager.Instance.Show(result.damage, numberPosition, false, DamageNumberTeam.Player);
                }
            }
        }
    }
}