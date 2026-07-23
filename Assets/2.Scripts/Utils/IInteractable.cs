// ============================================================================
// IInteractable.cs
// ----------------------------------------------------------------------------
// 플레이어가 상호작용 키로 상호작용할 수 있는 모든 대상이 구현하는 인터페이스입니다.
// 우선은 전리품(LootPickup)이 이 인터페이스를 구현하지만, 나중에 NPC 대화, 오브젝트 조사,
// 문/스위치 등 다른 종류의 상호작용 대상이 추가되더라도 이 인터페이스만 구현하면
// InteractionDetector가 똑같은 방식으로 감지/목록 표시/선택/실행을 처리해줍니다.
// ============================================================================

using UnityEngine;

public interface IInteractable
{
    /// <summary>상호작용 목록 UI에 표시할 이름입니다. (예: "슬라임 젤리 x3")</summary>
    string InteractionName { get; }

    /// <summary>거리순 정렬 등에 사용할 월드 좌표입니다. 보통 transform.position을 그대로 돌려주면 됩니다.</summary>
    Vector3 InteractionPosition { get; }

    /// <summary>실제 상호작용을 실행합니다. interactor는 상호작용을 시도한 오브젝트(보통 Player)입니다.
    /// 지금 당장은 쓰지 않더라도, 나중에(예: 인벤토리 시스템) 필요할 수 있어 인자로 받아둡니다.</summary>
    void Interact(GameObject interactor);
}