// ============================================================================
// CutsceneZoneTrigger.cs
// ----------------------------------------------------------------------------
// 특정 구역에 플레이어가 들어오면 연결해둔 CutsceneData를 딱 한 번 재생시키는 범용 트리거입니다.
// LootPickup/NPCTalker와 달리 상호작용 키가 필요 없이, 트리거 콜라이더 안에 플레이어가 들어오는
// 순간 자동으로 발동합니다.
//
// [1회성 - 이번 플레이 세션 동안만]
//   hasTriggered로 딱 한 번만 재생되게 막습니다. 씬을 다시 로드하거나 게임을 재시작하면 다시
//   재생됩니다(=디스크에 저장되는 영구 기록이 아닙니다). 게임을 껐다 켜도 다시 안 나오게 하고
//   싶어지면, 나중에 UIOption의 PlayerPrefs 저장 방식과 같은 패턴으로 확장하면 됩니다.
//
// [범용/재사용 - 이 스크립트는 컷씬 내용을 전혀 모릅니다]
//   이 트리거는 "Cutscene Data 필드에 연결해둔 애셋을 씬의 CutsceneManager로 한 번 재생시킨다"는
//   역할만 하고, 실제 연출 내용(카메라/이동 경로/대사 등)은 CutsceneData 애셋과 CutsceneManager 쪽
//   책임입니다 - 그래서 새 지역에 새 컷씬을 추가할 때마다 이 스크립트를 고칠 필요 없이, 새
//   CutsceneData 애셋을 만들고(TalkScript/QuestData 만들 때와 같은 방식) 트리거 콜라이더 하나에
//   연결하기만 하면 됩니다.
//
// [씬 준비]
//   1) 발동시키고 싶은 구역(예: 마을 입구)에 빈 오브젝트를 만들고 Collider(BoxCollider 등 -
//      Awake()에서 자동으로 Is Trigger로 맞춰줍니다)를 추가한 뒤 이 스크립트를 붙이세요.
//   2) Cutscene Data 필드에 재생할 CutsceneData 애셋을 연결하세요(Project 창에서 Create > Cutscene >
//      Cutscene Data로 미리 만들어두세요 - CutsceneData.cs 참고).
//   3) 씬에 CutsceneManager가 하나 있어야 하고, 그 CutsceneManager에 이 컷씬이 참조하는 카메라/
//      웨이포인트/NPC 키가 등록되어 있어야 합니다(CutsceneManager.cs 참고).
//   4) [중요] OnTriggerEnter가 실제로 호출되려면 이 오브젝트 쪽이나 플레이어 쪽 중 최소 한 곳에는
//      Rigidbody가 있어야 하는 유니티 물리 규칙이 있습니다 - Player 루트에는 AttackHitbox 판정을
//      위해 이미 Kinematic Rigidbody가 있을 테니 그걸로 충분합니다(따로 추가할 필요 없음).
// ============================================================================

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CutsceneZoneTrigger : MonoBehaviour
{
    [Tooltip("플레이어가 이 구역에 처음 들어왔을 때 딱 한 번 재생할 컷씬 애셋입니다.")]
    public CutsceneData cutsceneData;
    [Tooltip("플레이어로 판정할 태그입니다.")]
    public string playerTag = "Player";

    private bool hasTriggered;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;

        if (cutsceneData == null)
        {
            Debug.LogWarning($"[CutsceneZoneTrigger] '{name}': Cutscene Data가 연결되어 있지 않습니다.", this);
            return;
        }

        if (CutsceneManager.Instance == null)
        {
            Debug.LogWarning($"[CutsceneZoneTrigger] '{name}': 씬에 CutsceneManager가 없어 컷씬을 재생할 수 없습니다.", this);
            return;
        }

        hasTriggered = true;
        CutsceneManager.Instance.Play(cutsceneData);
    }
}