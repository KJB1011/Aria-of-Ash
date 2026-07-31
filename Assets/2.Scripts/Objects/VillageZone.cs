// ============================================================================
// VillageZone.cs
// ----------------------------------------------------------------------------
// 플레이어가 마을 구역에 들어오고 나갈 때 화면에 알림 텍스트(FloatingTextManager)를 띄우고, 체력 자연
// 회복량(PlayerStats.hpRegenPerSecond)을 마을 안에 있는 동안만 임시로 올려주는 트리거입니다.
// CutsceneZoneTrigger.cs와 같은 뼈대(트리거 콜라이더 + 태그 검사)를 쓰지만, 그쪽은 "딱 한 번만" 발동하는
// 반면 이 스크립트는 들어올 때/나갈 때마다 매번 반복해서 발동합니다 - 이 코드베이스에서 OnTriggerExit을
// 쓰는 첫 스크립트입니다.
//
// [입장/퇴장 알림 - FloatingTextManager]
//   화면 고정 위치에 잠깐 떴다 사라지는 알림 텍스트는 새로 만들지 않고 기존 FloatingTextManager를
//   그대로 재사용합니다(FloatingTextManager.cs 참고 - 월드 좌표가 아니라 화면 좌표 기준이라 게임을
//   멈추지 않고도 자연스럽게 떴다 사라집니다). enterMessage/exitMessage는 TextMeshPro의 리치 텍스트
//   태그(예: <color=yellow>...</color>)를 그대로 문자열에 포함시켜서, 마을 이름 부분만 다른 색으로
//   강조할 수 있습니다. 기본값은 "리브라이트" 부분만 노란색, 나머지는 (태그가 없으니) 기본 흰색으로
//   나옵니다. FloatingTextManager.Instance는 씬에 미리 배치해두지 않아도 처음 호출되는 순간 자동으로
//   생성됩니다 - Resources/HUD/FloatingText 프리팹만 준비되어 있으면 됩니다(FloatingTextManager.cs
//   상단 [프리팹 준비] 참고).
//
// [체력 자연 회복량 보너스 - 절대값을 지정하지 않고 더하고/빼는 방식]
//   들어오는 순간 PlayerStats.hpRegenPerSecond에 villageRegenBonus(기본 50)를 더하고, 나가는 순간
//   똑같은 값을 다시 뺍니다. hpRegenPerSecond를 특정 값으로 "설정"하는 게 아니라 "더하고 빼는" 방식으로
//   해둔 이유는, 나중에 다른 시스템(장비/버프/스킬 강화 등)이 같은 필드를 건드리게 되어도 이 스크립트가
//   그 값을 덮어써버리지 않고 서로 누적되도록 하기 위해서입니다. isPlayerInside로 상태를 추적해서, 같은
//   플레이어가 중복으로 Enter/Exit 판정을 받아도(예: 플레이어에게 콜라이더가 여러 개 있는 경우) 두 번
//   더해지거나 두 번 빠지지 않게 막습니다.
//
// [씬 준비]
//   1) 마을 경계에 맞춰 빈 오브젝트에 Collider(BoxCollider 등 - Awake()에서 자동으로 Is Trigger로
//      맞춰줍니다)를 추가한 뒤 이 스크립트를 붙이세요. 마을 전체를 감싸는 하나의 콜라이더로 만들면
//      충분합니다(입구/출구가 여러 곳이어도 큰 트리거 볼륨 하나로 다 처리됩니다).
//   2) Resources/HUD/FloatingText 프리팹이 준비되어 있는지 확인하세요(FloatingTextManager.cs 참고) -
//      없으면 알림 텍스트 대신 콘솔에 경고만 남고 조용히 넘어갑니다.
//   3) [중요] OnTriggerEnter/Exit가 실제로 호출되려면 이 오브젝트나 플레이어 쪽 중 최소 한 곳에는
//      Rigidbody가 있어야 하는 유니티 물리 규칙이 있습니다 - Player 루트에는 AttackHitbox 판정을 위해
//      이미 Kinematic Rigidbody가 있을 테니 따로 추가할 필요 없습니다(CutsceneZoneTrigger.cs와 동일).
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class VillageZone : MonoBehaviour
{
    [Tooltip("플레이어로 판정할 태그입니다.")]
    public string playerTag = "Player";

    [Header("체력 자연 회복 보너스")]
    [Tooltip("마을 안에 있는 동안 PlayerStats.hpRegenPerSecond에 추가로 더해줄 값(초당). 마을을 나가는 " +
              "순간 똑같은 값만큼 다시 빼서 원래대로 되돌립니다.")]
    public float villageRegenBonus = 50f;

    [Header("입장/퇴장 알림 (FloatingTextManager)")]
    [Tooltip("마을에 들어올 때 화면에 띄울 알림 문구입니다. TextMeshPro 리치 텍스트 태그(<color=...>)를 " +
              "그대로 넣으면 그 부분만 다른 색으로 강조됩니다.")]
    [TextArea]
    public string enterMessage = "<color=yellow>리브라이트</color>마을에 진입했습니다.";
    [Tooltip("마을에서 나갈 때 화면에 띄울 알림 문구입니다.")]
    [TextArea]
    public string exitMessage = "<color=yellow>리브라이트</color> 마을에서 나갔습니다";

    // 같은 플레이어가 중복으로 Enter/Exit 판정을 받아도(콜라이더가 여러 개인 경우 등) 회복 보너스와
    // 알림 텍스트가 두 번 뜨거나 두 번 빠지지 않도록 상태를 추적합니다.
    private bool isPlayerInside;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPlayerInside) return;
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = true;

        ApplyRegenBonus(true);
        if (!string.IsNullOrEmpty(enterMessage)) FloatingTextManager.Instance.Show(enterMessage);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isPlayerInside) return;
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = false;

        ApplyRegenBonus(false);
        if (!string.IsNullOrEmpty(exitMessage)) FloatingTextManager.Instance.Show(exitMessage);
    }

    /// <summary>PlayerStats.hpRegenPerSecond에 villageRegenBonus를 더하거나(entering=true) 뺍니다
    /// (entering=false). PlayerStats.Instance가 없는 테스트 씬 등에서도 안전합니다.</summary>
    private void ApplyRegenBonus(bool entering)
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[VillageZone] '{name}': PlayerStats.Instance가 없어 체력 자연 회복 보너스를 " +
                              "적용/해제할 수 없습니다.", this);
            return;
        }

        PlayerStats.Instance.hpRegenPerSecond += entering ? villageRegenBonus : -villageRegenBonus;
    }
}