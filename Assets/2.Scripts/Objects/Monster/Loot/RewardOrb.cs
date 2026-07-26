// ============================================================================
// RewardOrb.cs
// ----------------------------------------------------------------------------
// 몬스터를 처치하면 나오는 경험치/골드 오브젝트입니다. LootPickup(전리품)과 달리 상호작용
// 키로 줍는 게 아니라, 잠깐 튀어나왔다가(팝) 착지한 뒤 스스로 플레이어를 향해 날아가서(시킹)
// 자동으로 흡수됩니다. 흡수되는 순간 실제 보상(경험치/골드)이 적용되고, 전리품을 주울 때처럼
// 화면 왼쪽 로그(UIIngameLoot)에도 표시됩니다.
//
// [프리팹 준비 - 경험치용/골드용 각각 하나씩]
//   1) Type을 Experience 또는 Gold로 지정하세요.
//   2) Icon과 Display Name Prefix를 채우세요 (예: Experience → 이름 "경험치", Gold → 이름 "골드").
//      화면 왼쪽 로그에는 "경험치 x12"처럼 표시됩니다.
//   3) Collider가 자동으로 추가됩니다(RequireComponent) - Is Trigger로 자동 설정되긴 하지만,
//      지금은 실제로 트리거 판정을 쓰지 않고 거리 계산(absorbDistance)만으로 흡수 여부를
//      판단합니다 (나중에 다른 용도로 재사용할 수 있도록 그냥 켜둔 것뿐입니다).
//   4) 이 두 프리팹을 LootDropper의 Exp Orb Prefab / Gold Orb Prefab 필드에 연결하세요.
//   5) Pickup Sfx Name에 흡수될 때 재생할 효과음 이름을 넣으세요(Resources/SFX/ 아래 클립 이름).
//      Exp Orb 프리팹과 Gold Orb 프리팹이 서로 다른 컴포넌트 인스턴스이므로, 경험치와 골드의
//      흡수음을 서로 다르게 설정할 수 있습니다. 비워두면 소리 없이 조용히 흡수됩니다.
//
// [동작 흐름]
//   1) Launch()로 팝(포물선) 애니메이션 시작 - LootPickup.Launch()와 같은 방식입니다.
//   2) 착지 후 seekDelay초 동안 가만히 있습니다.
//   3) 그 뒤로는 매 프레임 플레이어 쪽으로 날아갑니다. 시간이 지날수록(seekAcceleration)
//      점점 빨라져서, 플레이어가 멀리 있어도 결국 따라잡습니다.
//   4) 플레이어와의 거리가 absorbDistance 이하가 되면 흡수됩니다 - 보상 적용 + 로그 표시 + 파괴.
// ============================================================================

using UnityEngine;

public enum RewardOrbType
{
    Experience,
    Gold,
}

[RequireComponent(typeof(Collider))]
public class RewardOrb : MonoBehaviour
{
    [Header("보상 종류")]
    public RewardOrbType type = RewardOrbType.Experience;
    public Sprite icon;
    [Tooltip("전리품 로그에 표시될 이름입니다. 예: \"경험치\" → \"경험치 x12\"로 표시됩니다.")]
    public string displayNamePrefix = "경험치";

    [Header("팝 애니메이션 (드롭되는 순간)")]
    public float popDuration = 0.4f;
    public float popArcHeight = 1f;

    [Header("흡수 (플레이어 추적)")]
    [Tooltip("착지 후 이 시간(초) 동안은 가만히 있다가 플레이어 쪽으로 날아가기 시작합니다.")]
    public float seekDelay = 0.3f;
    [Tooltip("플레이어를 향해 날아가는 시작 속도(미터/초).")]
    public float seekSpeed = 6f;
    [Tooltip("시간이 지날수록 속도가 이만큼(미터/초²) 점점 빨라집니다 - 플레이어가 멀리 있어도 결국 따라잡습니다.")]
    public float seekAcceleration = 12f;
    [Tooltip("플레이어와 이 거리 안으로 들어오면 흡수됩니다.")]
    public float absorbDistance = 0.6f;

    [Header("사운드")]
    [Tooltip("흡수(Absorb)되는 순간 재생할 효과음 이름 (Resources/SFX/ 아래 클립 이름과 일치해야 함). " +
              "비워두면 소리 없이 조용히 흡수됩니다. Exp Orb/Gold Orb 프리팹마다 다르게 설정하세요.")]
    public string pickupSfxName;

    private int amount;
    private Transform playerTransform;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float popTimer;
    private bool isPopping = true;
    private float seekTimer;
    private float currentSeekSpeed;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>이 오브젝트가 지급할 보상 수치를 설정합니다. LootDropper가 Instantiate 직후 호출합니다.</summary>
    public void Setup(int amount)
    {
        this.amount = amount;
        playerTransform = PlayerStats.Instance.transform;
    }

    /// <summary>현재 위치(시작 위치)에서 targetPosition(착지 위치)까지 포물선을 그리며 튀어나가는
    /// 연출을 시작합니다. LootPickup.Launch()와 같은 방식입니다.</summary>
    public void Launch(Vector3 targetPosition)
    {
        startPosition = transform.position;
        this.targetPosition = targetPosition;
        popTimer = 0f;
        isPopping = true;
    }

    private void Update()
    {
        if (isPopping)
        {
            UpdatePop();
        }
        else
        {
            UpdateSeek();
        }
    }

    private void UpdatePop()
    {
        popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);

        Vector3 horizontal = Vector3.Lerp(startPosition, targetPosition, t);
        float arc = Mathf.Sin(t * Mathf.PI) * popArcHeight;
        transform.position = horizontal + Vector3.up * arc;

        if (t >= 1f)
        {
            isPopping = false;
            seekTimer = seekDelay;
            currentSeekSpeed = seekSpeed;
        }
    }

    private void UpdateSeek()
    {
        if (seekTimer > 0f)
        {
            seekTimer -= Time.deltaTime;
            return;
        }

        currentSeekSpeed += seekAcceleration * Time.deltaTime;

        Vector3 toPlayer = playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= absorbDistance)
        {
            Absorb();
            return;
        }

        transform.position += toPlayer.normalized * currentSeekSpeed * Time.deltaTime;
    }

    private void Absorb()
    {
        if (type == RewardOrbType.Experience)
        {
            PlayerStats.Instance.AddExperience(amount);
        }
        else
        {
            PlayerCurrency.Instance.AddGold(amount);
        }

        UIIngameLoot.Instance.AddLoot(icon, $"{displayNamePrefix} x{amount}");
        PlayPickupSfx();
        Destroy(gameObject);
    }

    private void PlayPickupSfx()
    {
        if (string.IsNullOrEmpty(pickupSfxName)) return;
        SoundManager.Instance.PlaySFX(pickupSfxName, transform.position);
    }
}