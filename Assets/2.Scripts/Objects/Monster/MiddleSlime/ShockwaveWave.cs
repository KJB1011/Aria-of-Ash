// ============================================================================
// ShockwaveWave.cs
// ----------------------------------------------------------------------------
// 물수제비처럼 origin에서 forward 방향으로 나아가며, burstCount번(기본 3번) 연속으로
// "펑펑펑" 터지는 파도 하나입니다. MiddleSlimeBoss.FireWaveFan()이 부채꼴을 waveCount개
// 방향으로 나눠서 이 오브젝트를 방향별로 하나씩 동시에 만듭니다.
//
// [비주얼은 전부 VFX 프리팹(travelVfxName)에 맡깁니다]
// 예전 버전은 이 스크립트가 직접 부채꼴 메쉬를 그렸지만, 지금은 "앞으로 나아가면서 3번 연속으로
// 터지는" 연출 자체를 이미 완성된 VFX 프리팹(travelVfxName)이 전부 담당합니다. 이 스크립트는
// Launch() 시점에 VFXManager.Instance.Play()로 그 VFX를 한 번 재생만 시키고, 정작 이 스크립트가
// 하는 일은 "데미지 판정"뿐입니다 - 화면에 보이는 burst 타이밍에 맞춰서, 실제로 데미지가
// 들어가야 하는 지점(burst 지점)에서 Physics.OverlapSphere로 맞은 대상을 찾아 데미지를 적용합니다.
//
// [burst 지점/타이밍 계산 - 폭 전체를 커버]
// burstCount(기본 3)개의 burst가 travelDuration 동안 균등한 시간 간격으로 발생하고, 각 burst는
// origin에서 forward 방향으로 maxDistance까지 균등한 거리 간격으로 떨어진 지점에서 일어납니다.
// [중요] 이 조각(halfAngleDeg만큼의 부채꼴 폭)은 거리가 멀어질수록 양옆으로 넓게 벌어지는데,
// burst 판정을 정중앙 한 지점에서만 하면(반지름 burstRadius가 고정이라) 거리가 멀수록 옆으로 살짝만
// 벗어나도 안 맞는 문제가 생깁니다 - 그래서 각 burst마다 폭 전체(-halfAngleDeg ~ +halfAngleDeg)를
// burstArcSamples개의 지점으로 나눠서 나란히 판정합니다(부채꼴 정중앙만이 아니라 양옆까지 고르게
// 커버). burstArcSamples를 1로 두면 예전처럼 정중앙 한 지점만 판정합니다.
// 예) burstCount=3, maxDistance=8, travelDuration=1이면:
//   1번째 burst: t=0.33초, 거리 2.67에서 폭 전체를 burstArcSamples개 지점으로 판정
//   2번째 burst: t=0.67초, 거리 5.33에서 폭 전체를 burstArcSamples개 지점으로 판정
//   3번째 burst: t=1.00초, 거리 8.0에서 폭 전체를 burstArcSamples개 지점으로 판정
// VFX 프리팹 자체의 burst 타이밍(속도감)과 여기 travelDuration/burstCount가 맞아떨어지도록
// 인스펙터에서 눈으로 맞춰보며 조절하세요 - 프리팹의 Particle System들을 재생해보면서 실제
// burst가 몇 초 간격으로 몇 미터씩 나아가는지 확인 후 travelDuration/maxDistance를 맞추면 됩니다.
//
// [대상이 여러 burst에 걸쳐 맞을 수 있음 - 의도된 동작]
// burst끼리는 "이미 맞은 대상" 기록을 공유하지 않습니다 - 즉 플레이어가 자리를 옮기지 않고 여러 burst의
// 판정 범위 안에 계속 있다면 여러 번 맞아서 데미지가 여러 번 들어갈 수 있습니다(멀티히트 스킬처럼
// 의도된 동작입니다). 다만 같은 burst 안에서는(폭을 여러 지점으로 나눠 판정하다 보니 한 대상이 두
// 지점 모두에 걸릴 수 있음) 중복으로 맞지 않도록 burst 하나마다 "이미 맞은 대상" 기록을 따로 둡니다.
//
// [데미지 계산]
// 고정 데미지 대신 sourceStats(발사한 보스의 MonsterStats).CalculateDamage(damagePercent, 맞은
// 플레이어의 방어력)로 계산합니다 - MiddleSlimeBoss.FireWaveFan()이 Launch() 호출 전에
// damagePercent/sourceStats를 자동으로 채워줍니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("burst 지점에서 실제로 대상을 맞혔을 때(대상별로) 재생할 VFX 이름 (Resources/VFX/ 아래 " +
              "프리팹 이름과 일치해야 함). travelVfxName(파도 자체의 이동+burst 연출)과 달리, 이건 맞은 " +
              "대상이 있을 때만 그 위치에서 재생되는 타격 스파크입니다. 비워두면 VFX 없이 데미지만 적용됩니다.")]
    public string hitVfxName;
    [Tooltip("이 파도가 origin에서 forward 방향으로 나아가며 burstCount번 연속으로 터지는 연출 전체를 " +
              "담당하는 VFX 이름입니다(Resources/VFX/ 아래 프리팹 이름과 일치해야 함). Launch() 시점에 " +
              "origin 위치에서 forward를 바라보는 방향으로 딱 한 번 재생됩니다 - 이 VFX 프리팹 자체가 " +
              "이미 \"앞으로 나아가며 여러 번 터지는\" 연출을 전부 갖고 있다는 전제입니다. 이 스크립트는 " +
              "그 연출의 burst 타이밍에 맞춰 데미지 판정만 담당합니다(burstCount/travelDuration/maxDistance " +
              "참고). 비워두면 판정만 조용히 이뤄지고 화면에는 아무것도 보이지 않습니다.")]
    public string travelVfxName;

    [Header("burst (연속 타격)")]
    [Tooltip("몇 번 연속으로 터지는지입니다. travelVfxName 프리팹이 실제로 몇 번 burst하는지와 맞춰주세요.")]
    public int burstCount = 3;
    [Tooltip("각 burst 지점에서 판정할 반지름입니다.")]
    public float burstRadius = 1.5f;
    [Tooltip("travelVfxName VFX를 재생할 위치를, origin(보스 몸통 위치)에서 forward 방향으로 이만큼(미터) " +
              "앞으로 띄웁니다. 데미지 판정(burst 지점 계산)에는 전혀 영향을 주지 않고 오직 VFX가 재생되는 " +
              "'시각적' 위치만 조절합니다 - VFX 프리팹이 스스로 이동하지 않고 재생 지점에서 정해진 " +
              "길이만큼만 뻗어나가는 형태라면(예: Shape Module의 Length), origin이 보스 모델 몸통 " +
              "한가운데에 있을 때 그 몸 안/뒤에 가려서 안 보일 수 있습니다 - 이 값을 늘려서 몸 바깥에서 " +
              "재생되도록 조절하세요.")]
    public float vfxForwardOffset = 0f;
    [Tooltip("burst마다 부채꼴 폭(halfAngleDeg) 전체를 몇 개의 지점으로 나눠서 판정할지입니다. 1이면 " +
              "정중앙 한 지점만 판정합니다 - 부채꼴 폭이 좁거나 burstRadius가 넉넉하면 1로도 충분하지만, " +
              "거리가 멀어질수록 폭이 넓어지는데 반지름은 고정이라, 넓은 부채꼴에서는 늘려야(3~5 권장) " +
              "정중앙에서 벗어난 대상도 놓치지 않습니다.")]
    public int burstArcSamples = 5;

    private Vector3 origin;
    private Vector3 forward;
    private float halfAngleRad;
    private float maxDistance;
    private float travelDuration;
    private float elapsed;

    private float[] burstTimes;
    private float[] burstDistances;
    private int nextBurstIndex;

    private readonly HashSet<Collider> hitThisBurst = new HashSet<Collider>();

    /// <summary>origin에서 forward 방향을 중심으로 좌우 halfAngleDeg만큼 벌어진 부채꼴을 따라,
    /// travelDuration초에 걸쳐 maxDistance까지 나아가는 파도 하나를 시작합니다. burstCount(기본 3)개의
    /// burst가 시간/거리 모두 균등한 간격으로 자동 계산되고, 각 burst는 폭 전체(burstArcSamples개
    /// 지점)를 나눠서 판정합니다.</summary>
    public void Launch(Vector3 origin, Vector3 forward, float halfAngleDeg, float maxDistance, float travelDuration)
    {
        this.origin = origin;
        this.forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        halfAngleRad = halfAngleDeg * Mathf.Deg2Rad;
        this.maxDistance = maxDistance;
        this.travelDuration = Mathf.Max(0.01f, travelDuration);
        burstCount = Mathf.Max(1, burstCount);
        burstArcSamples = Mathf.Max(1, burstArcSamples);

        elapsed = 0f;
        nextBurstIndex = 0;
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(this.forward);

        burstTimes = new float[burstCount];
        burstDistances = new float[burstCount];
        for (int i = 0; i < burstCount; i++)
        {
            float t = (i + 1) / (float)burstCount; // 예: burstCount=3 → 1/3, 2/3, 3/3
            burstTimes[i] = this.travelDuration * t;
            burstDistances[i] = maxDistance * t;
        }

        // 이 VFX 프리팹 자체가 "앞으로 나아가며 burstCount번 터지는" 연출을 전부 갖고 있다는 전제로,
        // origin에서 forward를 바라보는 방향으로 딱 한 번만 재생합니다. duration을 travelDuration으로
        // 직접 지정해서, 이 파도가 사라지는 시점과 VFX 반납 시점을 맞춥니다.
        // vfxForwardOffset만큼 앞으로 띄운 위치에서 재생합니다 - origin(보스 몸통)이 아니라 그 앞쪽에서
        // 재생해야, 몸 안에 파묻혀 가려지지 않고 제대로 보입니다. 판정(burstDistances)은 origin 기준
        // 그대로라 이 오프셋의 영향을 받지 않습니다.
        if (!string.IsNullOrEmpty(travelVfxName))
        {
            Vector3 vfxSpawnPosition = origin + this.forward * vfxForwardOffset;
            VFXManager.Instance.Play(travelVfxName, vfxSpawnPosition, transform.rotation, this.travelDuration, null);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // 이번 프레임 사이에 여러 burst 타이밍을 동시에 지나쳤을 가능성까지 대비해 while로 처리합니다
        // (프레임 드랍 등으로 deltaTime이 커지는 경우).
        while (nextBurstIndex < burstCount && elapsed >= burstTimes[nextBurstIndex])
        {
            PerformBurst(burstDistances[nextBurstIndex]);
            nextBurstIndex++;
        }

        if (elapsed >= travelDuration)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>이 burst 지점의 거리(distance)에서, 부채꼴 폭(-halfAngleRad ~ +halfAngleRad) 전체를
    /// burstArcSamples개의 지점으로 나눠 각각 OverlapSphere로 판정합니다 - 정중앙만 판정하면 거리가
    /// 멀어질수록 넓어지는 폭을 다 커버하지 못하는 문제를 막기 위함입니다.</summary>
    private void PerformBurst(float distance)
    {
        hitThisBurst.Clear();

        for (int i = 0; i < burstArcSamples; i++)
        {
            float t = burstArcSamples == 1 ? 0.5f : i / (float)(burstArcSamples - 1);
            float angle = Mathf.Lerp(-halfAngleRad, halfAngleRad, t) * Mathf.Rad2Deg;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 position = origin + dir * distance;

            CheckHitsAt(position);
        }
    }

    private void CheckHitsAt(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, burstRadius, hitMask);
        foreach (Collider col in hits)
        {
            if (!hitThisBurst.Add(col)) continue; // 이번 burst 안에서 다른 샘플 지점에 이미 맞은 대상이면 건너뜀.

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            PlayerStats playerStats = col.GetComponentInParent<PlayerStats>();
            float targetDefense = playerStats != null ? playerStats.TotalDefense : 0f;

            DamageResult result = sourceStats != null
                ? sourceStats.CalculateDamage(damagePercent, targetDefense)
                : default;
            damageable.TakeDamage(result.damage);

            Vector3 hitPoint = col.ClosestPoint(position);

            if (!string.IsNullOrEmpty(hitVfxName))
            {
                VFXManager.Instance.Play(hitVfxName, hitPoint, transform.rotation);
            }

            Vector3 numberPosition = hitPoint + Vector3.up * damageNumberHeightOffset;
            DamageNumberManager.Instance.Show(result.damage, numberPosition, false, DamageNumberTeam.Player);
        }
    }
}