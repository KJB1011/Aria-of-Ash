// ============================================================================
// BGMZoneTrigger.cs
// ----------------------------------------------------------------------------
// 플레이어가 이 트리거 볼륨에 들어오면 SoundManager에게 "이 구역의 평상시(비전투) 음악은 이거야"라고
// 알려주는 컴포넌트입니다. VillageZone.cs와 같은 뼈대(트리거 콜라이더 + isPlayerInside 중복 방지 +
// 반복 발동)를 씁니다 - CutsceneZoneTrigger.cs처럼 "딱 한 번만" 발동하는 게 아니라, 플레이어가 구역을
// 나갔다가 다시 들어와도 매번 다시 발동해야 하기 때문입니다(마을 ↔ 필드를 왔다갔다 할 때마다 음악이
// 바뀌어야 하니까요).
//
// [실제 재생은 SoundManager가 담당합니다]
//   이 스크립트는 SoundManager.PlayBGM()을 직접 부르지 않고, 항상 SoundManager.SetFieldBGM()을 거칩니다.
//   그래야 "지금 전투 중이면 당장 바꾸지 않고, 전투가 끝난 뒤에 최신 구역 음악으로 되돌아간다"는 규칙이
//   자동으로 지켜집니다(예: 필드에서 몬스터와 싸우다가 몸싸움 도중 구역 경계를 살짝 넘어가도, 전투
//   음악이 뚝 끊기고 필드 음악으로 바뀌는 일이 없습니다). 자세한 설계 이유는 SoundManager.cs 상단
//   [전투 음악 자동 전환] 주석을 참고하세요.
//
// [진입할 때만 적용하고, 나갈 때는 보통 아무것도 하지 않습니다]
//   구역 A 안에 작은 하위 구역 B(예: 던전 입구, 보스방)를 겹쳐서 배치하는 경우가 흔합니다. B에 들어가면
//   B의 음악으로 바뀌어야 하지만, B에서 "나가는" 순간 자동으로 A의 음악으로 돌아가게 하려면 이 트리거
//   하나만으로는 알 수 없습니다(A의 콜라이더 안인지 밖인지 이 스크립트는 모릅니다). 그래서 기본값은
//   "나갈 때 아무것도 안 함"이고, 필요한 경우에만 revertOnExit을 켜고 exitBgmName을 지정하세요(주로
//   B 콜라이더가 A 콜라이더 안에 완전히 포함되는 형태로 배치했을 때 유용합니다).
//
// [예시 - 기본 필드 음악 + 마을 음악]
//   SoundManager.startBgmName = "Field_Theme"로 설정해두면 게임을 시작하자마자 이 곡이 자동으로
//   재생됩니다(SoundManager.cs 상단 [게임 시작 시 기본 배경음악] 참고). 마을 경계에 이 스크립트를 붙이고
//   zoneBgmName = "Village_Theme", revertOnExit = true, exitBgmName = "Field_Theme"으로 설정하면,
//   마을에 들어가는 순간 Village_Theme으로 바뀌고 마을을 나가는 순간 다시 Field_Theme으로 돌아갑니다 -
//   startBgmName과 exitBgmName에 같은 문자열("Field_Theme")을 넣어주면 됩니다.
//
// [씬 준비]
//   1) 구역 경계에 맞춰 빈 오브젝트에 Collider를 추가한 뒤(Awake()에서 자동으로 Is Trigger로 맞춰줍니다)
//      이 스크립트를 붙이고, zoneBgmName에 Resources/BGM/ 아래의 파일명을 정확히 입력하세요.
//   2) OnTriggerEnter/Exit가 실제로 호출되려면 이 오브젝트나 플레이어 쪽 중 최소 한 곳에는 Rigidbody가
//      있어야 하는 유니티 물리 규칙이 있습니다 - Player 루트에는 이미 Kinematic Rigidbody가 있으니 따로
//      추가할 필요 없습니다(VillageZone.cs / CutsceneZoneTrigger.cs와 동일).
//   3) 씬이 시작될 때 플레이어가 이미 구역 안에 서 있으면(예: 스폰 지점이 트리거 볼륨 내부) OnTriggerEnter가
//      호출되지 않을 수 있습니다 - 각 씬의 스폰 지점을 해당 구역의 BGMZoneTrigger 콜라이더 안쪽에 두거나,
//      스폰 지점 바로 앞에 트리거를 배치해서 플레이어가 스폰 직후 반드시 한 번은 통과하도록 구성하세요.
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BGMZoneTrigger : MonoBehaviour
{
    [Tooltip("플레이어로 판정할 태그입니다.")]
    public string playerTag = "Player";

    [Header("이 구역의 평상시(비전투) 음악")]
    [Tooltip("Resources/BGM/ 아래 파일명과 정확히 일치해야 합니다. 전투 중이 아니라면 즉시 이 곡으로 " +
              "바뀌고, 전투 중이라면 전투가 끝난 뒤 이 곡으로 돌아갑니다.")]
    public string zoneBgmName;

    [Tooltip("이 구역 음악으로 전환될 때의 크로스페이드 시간(초). 음수를 넣으면 SoundManager의 기본값을 씁니다.")]
    public float fadeDuration = -1f;

    [Header("퇴장 시 동작 (선택)")]
    [Tooltip("켜두면 플레이어가 이 트리거를 벗어날 때 exitBgmName으로 다시 전환합니다. 주로 더 큰 구역 " +
              "안에 겹쳐 배치한 하위 구역(던전 입구, 보스방 등)에서, 그 하위 구역을 벗어나면 바깥쪽 " +
              "구역의 음악으로 돌아가게 할 때 사용하세요. 꺼두면(기본값) 나갈 때는 아무 것도 하지 않고, " +
              "바깥쪽에 배치된 다른 BGMZoneTrigger가 있다면 그쪽 진입 시점에 자연스럽게 넘어갑니다.")]
    public bool revertOnExit = false;
    [Tooltip("revertOnExit이 켜져 있을 때, 벗어나는 순간 전환할 곡 이름입니다(Resources/BGM/ 기준).")]
    public string exitBgmName;

    // 같은 플레이어가 중복으로 Enter/Exit 판정을 받아도(콜라이더가 여러 개인 경우 등) 음악 전환이 두 번
    // 일어나지 않도록 상태를 추적합니다.
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

        if (string.IsNullOrEmpty(zoneBgmName))
        {
            Debug.LogWarning($"[BGMZoneTrigger] '{name}': zoneBgmName이 비어있어 구역 음악을 전환하지 않습니다.", this);
            return;
        }

        SoundManager.Instance.SetFieldBGM(zoneBgmName, fadeDuration);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isPlayerInside) return;
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = false;

        if (!revertOnExit) return;
        if (string.IsNullOrEmpty(exitBgmName))
        {
            Debug.LogWarning($"[BGMZoneTrigger] '{name}': revertOnExit이 켜져 있지만 exitBgmName이 비어있어 " +
                              "퇴장 시 음악을 전환하지 않습니다.", this);
            return;
        }

        SoundManager.Instance.SetFieldBGM(exitBgmName, fadeDuration);
    }
}