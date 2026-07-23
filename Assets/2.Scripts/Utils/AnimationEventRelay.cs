// ============================================================================
// AnimationEventRelay.cs
// ----------------------------------------------------------------------------
// "OnAttackComboWindowOpen has no receiver!" 경고를 해결하기 위한 중계용 스크립트.
//
// [왜 필요한가]
//   Animation Event는 그 애니메이션을 재생 중인 Animator 컴포넌트가 붙어있는
//   "그 오브젝트" 위의 컴포넌트에서만 메서드를 찾습니다. 그런데 지금 프로젝트 구조는
//   PlayerController가 Player(루트) 오브젝트에 있고, Animator/모델(예: "Paladin WProp
//   J Nordstrom")은 그 아래 하위 오브젝트로 들어가 있죠. AttackAreaController도
//   Player 아래 "AttackArea"라는 별도 자식 오브젝트에 있어서 모델 쪽에서는 바로 안
//   보입니다. 그래서 Animation Event가 호출하려는 메서드들을 모델 오브젝트에서 못 찾아
//   "no receiver" 경고가 뜨는 겁니다.
//
// [해결 방법]
//   Animator가 붙어있는 그 모델 오브젝트(예: "Paladin WProp J Nordstrom")에 이
//   스크립트를 추가하세요. Animation Event는 이제 이 스크립트의 메서드를 정상적으로
//   찾아서 호출하고, 이 스크립트는 그 호출을 PlayerController / AttackAreaController로
//   그대로 전달(relay)해줍니다.
//
// [설정 방법]
//   1) Animator 컴포넌트가 붙어있는 모델 오브젝트를 선택합니다.
//   2) 이 스크립트(AnimationEventRelay)를 그 오브젝트에 Add Component로 추가합니다.
//   3) playerController / attackArea 필드는 비워두면 Awake()에서 자동으로 찾아
//      연결합니다 (씬 전체의 루트, 즉 Player 오브젝트를 기준으로 그 하위에서 찾습니다).
//      구조가 특이하다면 인스펙터에서 직접 드래그해 넣어도 됩니다.
//   4) 각 클립에 걸어둔 Animation Event(Function: OnAttackComboWindowOpen /
//      OnAttackMotionEnd / OnHitboxOpen(String) / OnHitboxClose(String) / OnAttackSwingVfx(String) /
//      OnAttackSwingVfx2(String) / OnUltSkillVfx1(String) / OnUltSkillVfx2(String) / OnUltSlamImpact /
//      OnUltCameraPullBack / OnUltCameraSwitchToBack)는 그대로 두시면 됩니다. OnHitboxOpen/OnHitboxClose의
//      String 파라미터에는 AttackArea 아래 만들어둔 자식 오브젝트 이름(예: "Attack1")을, 나머지 VFX 관련
//      이벤트들의 String 파라미터에는 Resources/VFX 아래의 이펙트 이름(예: "FX_Player_Slash")을 넣으세요.
//      OnAttackSwingVfx2는 한 타격에 VFX를 2개 넣고 싶을 때만(예: 3타) 그 클립에 추가로 걸어두면 됩니다.
//      OnUltSlamImpact(파라미터 없음)는 UltSkill 클립에서 실제로 내려찍는(타격) 프레임에 걸어두세요 -
//      SkillInfo의 '필살기강화'가 해제되어 있을 때만 그 프레임을 기준으로 0.5초 뒤 2차 폭발이 터집니다
//      (PlayerController.ultSecondExplosionDelay 참고). OnUltCameraPullBack(파라미터 없음)은 정면샷이
//      잠깐 보여진 직후 프레임에 걸어두세요 - FaceCam에서 뒤로 멀어지는 PullBackCam으로 블렌드를
//      시작시킵니다. OnUltCameraSwitchToBack(파라미터 없음)은 그보다 뒤, OnUltSlamImpact보다 살짝
//      앞쪽(캐릭터가 돌아서며 감아 들어가기 시작하는 프레임)에 걸어두세요 - PullBackCam에서 BackCam으로
//      블렌드를 시작시킵니다.
// ============================================================================

using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [Tooltip("비워두면 Awake()에서 자동으로 찾습니다.")]
    public PlayerController playerController;
    [Tooltip("비워두면 Awake()에서 자동으로 찾습니다. Player 아래 \"AttackArea\" 오브젝트에 붙어있는 컨트롤러입니다.")]
    public AttackAreaController attackArea;
    [Tooltip("비워두면 Awake()에서 자동으로 찾습니다. 파이어볼 발사를 담당하는 컴포넌트입니다.")]
    public PlayerSkillProjectile skillProjectile;

    private void Awake()
    {
        // AttackArea/PlayerSkillProjectile이 모델의 형제뻘 오브젝트라 GetComponentInParent로는
        // 못 찾기 때문에, 씬 계층의 최상위(Player 루트)를 기준으로 그 하위 전체에서 찾습니다.
        Transform root = transform.root;

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
            if (playerController == null) playerController = root.GetComponentInChildren<PlayerController>(true);
        }
        if (attackArea == null)
        {
            attackArea = root.GetComponentInChildren<AttackAreaController>(true);
        }
        if (skillProjectile == null)
        {
            skillProjectile = root.GetComponentInChildren<PlayerSkillProjectile>(true);
        }

        if (playerController == null)
        {
            Debug.LogWarning($"[AnimationEventRelay] {name}: PlayerController를 찾지 못했습니다. " +
                              "인스펙터에서 playerController 필드를 직접 연결해주세요.", this);
        }
        if (attackArea == null)
        {
            Debug.LogWarning($"[AnimationEventRelay] {name}: AttackAreaController를 찾지 못했습니다. " +
                              "공격 판정 이벤트를 쓰신다면 인스펙터에서 attackArea 필드를 직접 연결해주세요.", this);
        }
        if (skillProjectile == null)
        {
            Debug.LogWarning($"[AnimationEventRelay] {name}: PlayerSkillProjectile을 찾지 못했습니다. " +
                              "파이어볼 발사 이벤트를 쓰신다면 인스펙터에서 skillProjectile 필드를 직접 연결해주세요.", this);
        }
    }

    // Animation Event가 호출하는 진입점들. 이름은 클립에 걸어둔 Function 이름과 반드시 일치해야 합니다.

    public void OnAttackComboWindowOpen()
    {
        if (playerController != null) playerController.OnAttackComboWindowOpen();
    }

    public void OnAttackMotionEnd()
    {
        if (playerController != null) playerController.OnAttackMotionEnd();
    }

    public void OnAttackSwingVfx(string vfxName)
    {
        if (playerController != null) playerController.OnAttackSwingVfx(vfxName);
    }

    public void OnAttackSwingVfx2(string vfxName)
    {
        if (playerController != null) playerController.OnAttackSwingVfx2(vfxName);
    }

    public void OnUltSkillVfx1(string vfxName)
    {
        if (playerController != null) playerController.OnUltSkillVfx1(vfxName);
    }

    public void OnUltSkillVfx2(string vfxName)
    {
        if (playerController != null) playerController.OnUltSkillVfx2(vfxName);
    }

    public void OnUltSlamImpact()
    {
        if (playerController != null) playerController.OnUltSlamImpact();
    }

    public void OnUltCameraPullBack()
    {
        if (playerController != null) playerController.OnUltCameraPullBack();
    }

    public void OnUltCameraSwitchToBack()
    {
        if (playerController != null) playerController.OnUltCameraSwitchToBack();
    }

    public void OnHitboxOpen(string motionName)
    {
        if (attackArea != null) attackArea.OpenHitbox(motionName);
    }

    public void OnHitboxClose(string motionName)
    {
        if (attackArea != null) attackArea.CloseHitbox(motionName);
    }

    public void OnFireballRelease()
    {
        if (skillProjectile != null) skillProjectile.OnFireballRelease();
    }
}