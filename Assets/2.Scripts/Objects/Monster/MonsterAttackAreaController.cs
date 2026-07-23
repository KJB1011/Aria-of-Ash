// ============================================================================
// MonsterAttackAreaController.cs
// ----------------------------------------------------------------------------
// 몬스터 아래 만들어둔 "MonsterAttackArea"(태그: MonsterAttackArea) 오브젝트에 붙이는
// 컨트롤러입니다. Player의 AttackAreaController와 완전히 동일한 구조입니다 - 그 아래
// BodyAttack 등 모션 이름으로 만든 자식 오브젝트(각각 BoxCollider + MonsterAttackHitbox)를
// 이름으로 찾아뒀다가, Animation Event가 이름을 넘겨주면 그 이름에 해당하는 히트박스만
// 열고 닫아줍니다.
//
// [씬 준비]
//   1) 몬스터 오브젝트 아래 "MonsterAttackArea"라는 빈 오브젝트를 만들고 Tag를
//      "MonsterAttackArea"로 지정한 뒤 이 스크립트를 붙이세요 (프로젝트에 아직 이 Tag가 없다면
//      Tags & Layers 설정에서 새로 추가해야 합니다).
//   2) 그 아래에 BodyAttack 등 각 근접 모션 이름 그대로 자식 오브젝트를 만들고, 각각에
//      BoxCollider + MonsterAttackHitbox 컴포넌트를 추가해서 원하는 판정 범위(위치/크기/회전)로
//      맞춰두세요.
//   3) 각 근접 공격 애니메이션 클립에서, 실제로 플레이어를 스치기 시작하는 프레임에
//      OnHitboxOpen(String) Animation Event를 추가하고 문자열 파라미터에 그 자식 오브젝트
//      이름(예: "BodyAttack")을 그대로 넣으세요. 판정이 끝나야 하는 프레임에는
//      OnHitboxClose(String) 이벤트를 추가하고 같은 이름을 넣으세요.
//   4) Animator가 몬스터 루트와 다른 모델 오브젝트에 있다면, MonsterAnimationEventRelay가 이
//      컨트롤러를 자동으로 찾아 이벤트를 전달해줍니다 (transform.root 기준으로 찾습니다).
//      Animator를 가진 그 오브젝트에 MonsterAnimationEventRelay를 붙이세요.
//   5) MonsterFSM(SlimeFSM/WoodGolemFSM 등) 쪽의 attackArea 필드는 비워두면 Awake()에서 자동으로
//      찾아 연결됩니다 - 피격 스턴/사망 등으로 BodyAttack이 중간에 끊기면 자동으로
//      CloseAllHitboxes()를 호출해 판정이 열린 채로 남아있지 않게 하는 안전장치로 쓰입니다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAttackAreaController : MonoBehaviour
{
    public const string RequiredTag = "MonsterAttackArea";

    private readonly Dictionary<string, MonsterAttackHitbox> hitboxesByName = new Dictionary<string, MonsterAttackHitbox>();
    private string currentOpenMotionName;

    // Player의 AttackAreaController와 같은 이유로 Start()에서 등록합니다 - 부모(이 컨트롤러)가
    // 자식(MonsterAttackHitbox)들보다 먼저 Awake될 수도 있어서, 모든 오브젝트의 Awake()가 끝난
    // 뒤인 Start()에서 등록해야 각 히트박스의 boxCollider가 확실히 준비된 상태입니다.
    private void Start()
    {
        if (!CompareTag(RequiredTag))
        {
            Debug.LogWarning($"[MonsterAttackAreaController] {name}: 이 오브젝트의 Tag가 '{RequiredTag}'가 아닙니다. " +
                              "설정 혼동을 막기 위해 Tag를 맞춰두는 걸 권장합니다.", this);
        }

        // 비활성 상태인 자식도 포함해서 이름으로 등록해둡니다 (BodyAttack 등).
        MonsterAttackHitbox[] hitboxes = GetComponentsInChildren<MonsterAttackHitbox>(true);
        foreach (MonsterAttackHitbox hitbox in hitboxes)
        {
            if (hitboxesByName.ContainsKey(hitbox.name))
            {
                Debug.LogWarning($"[MonsterAttackAreaController] '{hitbox.name}' 이름의 히트박스가 여러 개 있습니다. " +
                                  "자식 오브젝트 이름은 서로 겹치지 않게 해주세요.", hitbox);
                continue;
            }

            hitboxesByName[hitbox.name] = hitbox;
            hitbox.Close(); // 시작할 때는 전부 꺼둡니다.
        }
    }

    /// <summary>Animation Event 전용. motionName은 그 모션에 대응하는 자식 오브젝트 이름과 정확히 일치해야
    /// 합니다 (예: "BodyAttack"). 대소문자와 철자를 꼭 맞춰주세요.</summary>
    public void OpenHitbox(string motionName)
    {
        if (!hitboxesByName.TryGetValue(motionName, out MonsterAttackHitbox hitbox))
        {
            Debug.LogWarning($"[MonsterAttackAreaController] '{motionName}' 이름의 히트박스를 찾을 수 없습니다. " +
                              "MonsterAttackArea 하위 오브젝트 이름과 Animation Event에 넣은 문자열이 일치하는지 확인하세요.");
            return;
        }

        // 다른 모션의 히트박스가 안 닫힌 채 남아있었다면 먼저 닫아줍니다.
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

        if (hitboxesByName.TryGetValue(motionName, out MonsterAttackHitbox hitbox))
        {
            hitbox.Close();
        }

        currentOpenMotionName = null;
    }

    /// <summary>피격 스턴/사망 등으로 공격이 강제로 끊기거나 안전장치로 강제 종료될 때 코드에서 직접
    /// 호출하세요(MonsterFSM.ChangeState()가 이미 이렇게 호출해줍니다). 열려있던 히트박스를 무조건
    /// 닫아서, Animation Event가 못 불렸을 경우에도 판정이 계속 켜진 채로 남아있는 일을 막습니다.</summary>
    public void CloseAllHitboxes()
    {
        foreach (MonsterAttackHitbox hitbox in hitboxesByName.Values)
        {
            hitbox.Close();
        }
        currentOpenMotionName = null;
    }
}