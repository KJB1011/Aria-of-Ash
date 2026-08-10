// ============================================================================
// AttackAreaController.cs
// ----------------------------------------------------------------------------
// Player 아래 만들어둔 "AttackArea"(태그: AttackArea) 오브젝트에 붙이는 컨트롤러입니다.
// 그 아래 Attack1, Attack2, Attack3, Skill, UltSkill 등 모션 이름으로 만든 자식
// 오브젝트(각각 BoxCollider + AttackHitbox)를 이름으로 찾아뒀다가, Animation Event가
// 이름을 넘겨주면 그 이름에 해당하는 히트박스만 열고 닫아줍니다.
//
// [씬 준비]
//   1) Player 아래 "AttackArea"라는 빈 오브젝트를 만들고 Tag를 "AttackArea"로 지정한 뒤
//      이 스크립트를 붙이세요.
//   2) 그 아래에 Attack1, Attack2, Attack3, Skill, UltSkill 등 각 모션 이름 그대로
//      자식 오브젝트를 만들고, 각각에 BoxCollider + AttackHitbox 컴포넌트를 추가해서
//      원하는 판정 범위(위치/크기/회전)로 맞춰두세요.
//   3) 각 Attack/Skill 애니메이션 클립에서, 무기가 실제로 적을 스치기 시작하는 프레임에
//      OnHitboxOpen(String) Animation Event를 추가하고 문자열 파라미터에 그 자식
//      오브젝트 이름(예: "Attack1")을 그대로 넣으세요. 판정이 끝나야 하는 프레임에는
//      OnHitboxClose(String) 이벤트를 추가하고 같은 이름을 넣으세요.
//   4) Animator가 Player와 다른 모델 오브젝트에 있다면, AnimationEventRelay가 이
//      컨트롤러를 자동으로 찾아 이벤트를 전달해줍니다 (transform.root 기준으로 찾습니다).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AttackAreaController : MonoBehaviour
{
    public const string RequiredTag = "AttackArea";

    private readonly Dictionary<string, AttackHitbox> hitboxesByName = new Dictionary<string, AttackHitbox>();
    private string currentOpenMotionName;
    private PlayerController playerController;

    // 주의: 여기서 자식 AttackHitbox들의 Close()를 호출하려면 그 히트박스들의 Awake()가 먼저
    // 끝나서 boxCollider가 세팅되어 있어야 합니다. 그런데 유니티는 서로 다른 오브젝트의
    // Awake() 호출 순서를 보장하지 않습니다(부모가 자식보다 먼저 Awake될 수도 있습니다) -
    // 그래서 이 초기화를 Awake()가 아니라 Start()에서 합니다. Start()는 씬의 모든 오브젝트의
    // Awake()가 전부 끝난 뒤에 호출되는 게 보장되므로, 이 시점엔 모든 AttackHitbox의
    // boxCollider가 확실히 준비되어 있습니다.
    private void Start()
    {
        if (!CompareTag(RequiredTag))
        {
            Debug.LogWarning($"[AttackAreaController] {name}: 이 오브젝트의 Tag가 '{RequiredTag}'가 아닙니다. " +
                              "설정 혼동을 막기 위해 Tag를 맞춰두는 걸 권장합니다.", this);
        }

        // 캔슬된 모션의 뒤늦은 OpenHitbox 이벤트를 걸러내기 위해 필요합니다(OpenHitbox() 주석 참고).
        // 못 찾아도 경고만 남기고 예전처럼(필터링 없이) 동작합니다 - Player 하위 구조가 아닌 다른
        // 용도로 이 컨트롤러를 재사용하는 경우를 막지 않기 위해서입니다.
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning($"[AttackAreaController] {name}: 상위에서 PlayerController를 찾지 못했습니다. " +
                              "캔슬된 공격의 뒤늦은 Animation Event를 걸러내는 기능이 동작하지 않습니다.", this);
        }

        // 비활성 상태인 자식도 포함해서 이름으로 등록해둡니다 (Attack1, Attack2, Skill...).
        AttackHitbox[] hitboxes = GetComponentsInChildren<AttackHitbox>(true);
        foreach (AttackHitbox hitbox in hitboxes)
        {
            if (hitboxesByName.ContainsKey(hitbox.name))
            {
                Debug.LogWarning($"[AttackAreaController] '{hitbox.name}' 이름의 히트박스가 여러 개 있습니다. " +
                                  "자식 오브젝트 이름은 서로 겹치지 않게 해주세요.", hitbox);
                continue;
            }

            hitboxesByName[hitbox.name] = hitbox;
            hitbox.Close(); // 시작할 때는 전부 꺼둡니다.
        }
    }

    /// <summary>Animation Event 전용. motionName은 그 모션에 대응하는 자식 오브젝트 이름과 정확히 일치해야
    /// 합니다 (예: "Attack1", "Attack2", "Skill"). 대소문자와 철자를 꼭 맞춰주세요.</summary>
    public void OpenHitbox(string motionName)
    {
        if (!hitboxesByName.TryGetValue(motionName, out AttackHitbox hitbox))
        {
            Debug.LogWarning($"[AttackAreaController] '{motionName}' 이름의 히트박스를 찾을 수 없습니다. " +
                              "AttackArea 하위 오브젝트 이름과 Animation Event에 넣은 문자열이 일치하는지 확인하세요.");
            return;
        }

        // [캔슬 이후 유령 판정 방지] 대시/스킬 입력으로 이 공격이 이미 캔슬(CancelAttack→
        // EndAttackMotion→CloseAllHitboxes)된 뒤에도, Animator 트랜지션 블렌드 등으로 인해 예전
        // 모션의 OpenHitbox Animation Event가 한두 프레임 늦게 들어올 수 있습니다. 이때 아무 확인 없이
        // 그냥 열어버리면, 캐릭터는 이미 구르기/스킬 모션으로 넘어간 것처럼 보이는데 판정만 몰래 열려서
        // (심지어 이 뒤에 CloseHitbox가 영영 안 불릴 수도 있어 계속 켜진 채로 남을 수도 있습니다)
        // "모션은 안 나갔는데 공격만 맞는" 현상이 생깁니다. PlayerController에게 지금 정말 이 모션이
        // 진행 중인지 먼저 확인해서, 이미 캔슬된 뒤늦은 이벤트는 조용히 무시합니다.
        if (playerController != null && !playerController.IsAttackMotionCurrent(motionName))
        {
            return;
        }

        // 콤보 캔슬 등으로 이전 모션의 히트박스가 안 닫힌 채 남아있었다면 먼저 닫아줍니다.
        if (currentOpenMotionName != null && currentOpenMotionName != motionName)
        {
            CloseHitbox(currentOpenMotionName);
        }

        currentOpenMotionName = motionName;
        hitbox.Open();
    }

    /// <summary>Animation Event 전용. 트랜지션 블렌드 등으로 이전 모션의 Close 이벤트가 늦게 들어와도,
    /// 이미 다른 모션의 히트박스가 열려있다면 그걸 잘못 닫지 않도록 이름이 일치할 때만 닫습니다.</summary>
    public void CloseHitbox(string motionName)
    {
        if (currentOpenMotionName != motionName) return;

        if (hitboxesByName.TryGetValue(motionName, out AttackHitbox hitbox))
        {
            hitbox.Close();
        }

        currentOpenMotionName = null;
    }

    /// <summary>대시/스킬로 콤보가 캔슬되거나 안전장치로 강제 종료될 때 코드에서 직접 호출하세요.
    /// 열려있던 히트박스를 무조건 닫아서, Animation Event가 못 불렸을 경우에도 판정이 계속
    /// 켜진 채로 남아있는 일을 막습니다.</summary>
    public void CloseAllHitboxes()
    {
        foreach (AttackHitbox hitbox in hitboxesByName.Values)
        {
            hitbox.Close();
        }
        currentOpenMotionName = null;
    }
}