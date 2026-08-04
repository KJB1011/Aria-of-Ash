// ============================================================================
// SkillTreeNode.cs
// ----------------------------------------------------------------------------
// SkillInfo 화면의 스킬 트리에 배치된 버튼 하나에 붙이는 컴포넌트입니다. 씬에 미리 배치해두신
// 8개의 버튼(패시브/기본공격/스킬/필살기 + 강화 버전 4개) 각각에 이 컴포넌트를 붙이고, data에
// 그 노드에 해당하는 SkillTreeData 애셋을 연결하세요.
//
// [클릭 흐름]
//   버튼의 OnClick에 OnClickNode()를 연결하세요(파라미터 없음). 이 함수가 부모 계층에서
//   UICharacterInfo를 찾아 SelectSkillNode(this)를 호출해줍니다 - UICharacterInfo가 이 노드의
//   data(이름/타입/설명/해제 조건)를 오른쪽 정보 패널에 표시합니다.
//
// [잠금/선택 표시]
//   startUnlocked를 켜두면 씬 시작 시 이미 해제된 상태로 시작합니다(패시브/기본공격/스킬/필살기 같은
//   기본 4개에 추천). lockedOverlay/selectedHighlight는 선택 사항입니다 - 비워두면 그냥 표시를
//   건너뛰고, 연결해두면 잠금 상태/선택 상태에 맞춰 자동으로 켜고 끕니다.
//
// [씬 준비]
//   1) 버튼 오브젝트에 이 스크립트를 붙이세요.
//   2) Data에 이 노드의 SkillTreeData 애셋을 연결하세요.
//   3) (선택) Icon을 비워두면 이 오브젝트의 Image 컴포넌트를 자동으로 씁니다 - Start()에서
//      data.icon으로 자동으로 채워집니다.
//   4) (선택) Locked Overlay/Selected Highlight에 자물쇠 아이콘, 선택 테두리 등의 오브젝트를 연결하세요.
//   5) 버튼의 OnClick에 이 컴포넌트의 OnClickNode()를 연결하세요.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

public class SkillTreeNode : MonoBehaviour
{
    [Tooltip("이 노드가 나타내는 스킬 데이터입니다.")]
    public SkillTreeData data;

    [Tooltip("켜두면 씬 시작 시 이미 해제된 상태로 시작합니다. 기본 4대 스킬(패시브/기본공격/스킬/필살기)은 " +
              "보통 처음부터 켜져있고, 강화 버전 4개는 꺼둔 채로 시작해서 재료로 해제하게 하는 걸 추천합니다.")]
    public bool startUnlocked = true;

    [Tooltip("이 버튼 자체의 아이콘(스킬 아이콘) 이미지입니다. 비워두면 이 오브젝트의 Image 컴포넌트를 자동으로 씁니다.")]
    [SerializeField] private Image icon;
    [Tooltip("잠겨있는 동안 위에 덮어씌울 오브젝트(자물쇠 아이콘, 어둡게 칠한 오버레이 등). 비워두면 잠금 표시를 하지 않습니다.")]
    [SerializeField] private GameObject lockedOverlay;
    [Tooltip("지금 선택된 노드임을 표시하는 오브젝트(테두리 하이라이트 등). 비워두면 선택 표시를 하지 않습니다.")]
    [SerializeField] private Image selectedHighlight;
    [Tooltip("아직 잠겨있지만 재료/골드를 다 모아서 지금 바로 해제할 수 있는 상태일 때 켜지는 느낌표 알림 이미지입니다. " +
              "UICharacterInfo가 재료/골드가 바뀔 때마다 자동으로 켜고 끕니다. 비워두면 알림 표시를 하지 않습니다.")]
    [SerializeField] private GameObject unlockableNotification;

    /// <summary>지금 이 노드가 해제된 상태인지 여부입니다. UICharacterInfo가 UNLOCK 버튼 처리 시 확인/변경합니다.</summary>
    public bool IsUnlocked { get; private set; }

    private void Awake()
    {
        if (icon == null) icon = GetComponent<Image>();
        IsUnlocked = startUnlocked;
    }

    private void Start()
    {
        if (icon != null && data != null && data.icon != null)
        {
            icon.sprite = data.icon;
        }

        RefreshLockVisual();
        SetSelected(false);
    }

    /// <summary>버튼의 OnClick에 파라미터 없이 연결하세요. 부모 계층에서 UICharacterInfo를 찾아 이 노드가
    /// 선택됐다고 알려줍니다(AnimationEventRelay와 같은 자동 찾기 방식).</summary>
    public void OnClickNode()
    {
        UICharacterInfo owner = GetComponentInParent<UICharacterInfo>(true);
        if (owner == null)
        {
            Debug.LogWarning("[SkillTreeNode] 부모 계층에서 UICharacterInfo를 찾을 수 없습니다.", this);
            return;
        }

        owner.SelectSkillNode(this);
    }

    /// <summary>UICharacterInfo.ClickSkillUpgradeButton()이 재료/골드 소모에 성공했을 때 호출합니다.</summary>
    public void MarkUnlocked()
    {
        IsUnlocked = true;
        RefreshLockVisual();
    }

    /// <summary>지금 선택된 노드인지 표시를 켜고 끕니다. UICharacterInfo가 노드를 선택/선택 해제할 때 호출합니다.</summary>
    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.enabled = selected;
    }

    /// <summary>이 노드를 지금 바로 해제할 수 있는 상태(재료/골드 충분)인지 여부에 맞춰 느낌표 알림을 켜고 끕니다.
    /// UICharacterInfo.RefreshSkillNodeNotifications()가 재료/골드가 바뀔 때마다 호출합니다.</summary>
    public void SetUnlockableNotification(bool show)
    {
        if (unlockableNotification != null) unlockableNotification.SetActive(show);
    }

    private void RefreshLockVisual()
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(!IsUnlocked);
    }
}