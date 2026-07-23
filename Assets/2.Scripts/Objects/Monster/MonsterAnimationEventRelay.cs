// ============================================================================
// MonsterAnimationEventRelay.cs
// ----------------------------------------------------------------------------
// Player의 AnimationEventRelay와 같은 이유로 필요한 중계용 스크립트입니다.
//
// [왜 필요한가]
//   Animation Event는 그 애니메이션을 재생 중인 Animator 컴포넌트가 붙어있는 "그 오브젝트"
//   위의 컴포넌트에서만 메서드를 찾습니다. 몬스터의 Animator가 몬스터 루트가 아니라 하위의
//   모델 오브젝트에 있고, MonsterAttackAreaController는 별도의 "MonsterAttackArea" 자식
//   오브젝트에 있다면 모델 쪽에서는 바로 안 보여서 "no receiver" 경고가 뜹니다.
//
// [해결 방법]
//   Animator가 붙어있는 그 모델 오브젝트에 이 스크립트를 추가하세요. Animation Event는 이제
//   이 스크립트의 메서드를 정상적으로 찾아서 호출하고, 이 스크립트는 그 호출을
//   MonsterAttackAreaController로 그대로 전달(relay)해줍니다.
//
// [설정 방법]
//   1) Animator 컴포넌트가 붙어있는 모델 오브젝트를 선택합니다.
//   2) 이 스크립트(MonsterAnimationEventRelay)를 그 오브젝트에 Add Component로 추가합니다.
//   3) attackArea 필드는 비워두면 Awake()에서 자동으로 찾아 연결합니다 (씬 계층의 최상위, 즉
//      몬스터 루트를 기준으로 그 하위에서 찾습니다). 구조가 특이하다면 인스펙터에서 직접
//      드래그해 넣어도 됩니다.
//   4) 근접 공격 애니메이션 클립에 걸어둔 Animation Event(Function: OnHitboxOpen(String) /
//      OnHitboxClose(String))는 그대로 두시면 됩니다. String 파라미터에는 MonsterAttackArea
//      아래 만들어둔 자식 오브젝트 이름(예: "BodyAttack")을 넣으세요.
//
// [Animator가 몬스터 루트에 바로 있다면]
//   이 스크립트 없이도 몬스터 루트에 직접 이 스크립트를 추가해도 동일하게 동작합니다 -
//   중요한 건 "Animator가 붙어있는 그 오브젝트에 이 스크립트가 있어야 한다"는 것뿐입니다.
// ============================================================================

using UnityEngine;

public class MonsterAnimationEventRelay : MonoBehaviour
{
    [Tooltip("비워두면 Awake()에서 자동으로 찾습니다. 몬스터 아래 \"MonsterAttackArea\" 오브젝트에 붙어있는 컨트롤러입니다.")]
    public MonsterAttackAreaController attackArea;

    private void Awake()
    {
        // MonsterAttackArea가 모델의 형제뻘 오브젝트라 GetComponentInParent로는 못 찾기 때문에,
        // 씬 계층의 최상위(몬스터 루트)를 기준으로 그 하위 전체에서 찾습니다.
        if (attackArea == null)
        {
            Transform root = transform.root;
            attackArea = root.GetComponentInChildren<MonsterAttackAreaController>(true);
        }

        if (attackArea == null)
        {
            Debug.LogWarning($"[MonsterAnimationEventRelay] {name}: MonsterAttackAreaController를 찾지 못했습니다. " +
                              "인스펙터에서 attackArea 필드를 직접 연결해주세요.", this);
        }
    }

    public void OnHitboxOpen(string motionName)
    {
        if (attackArea != null) attackArea.OpenHitbox(motionName);
    }

    public void OnHitboxClose(string motionName)
    {
        if (attackArea != null) attackArea.CloseHitbox(motionName);
    }
}