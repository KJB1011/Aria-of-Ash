// ============================================================================
// UINPCNameplate.cs
// ----------------------------------------------------------------------------
// NPC 머리 위에 떠 있는 이름표 "프리팹" 쪽 스크립트입니다. World Space Canvas 하나에 이름 표시용
// TextMeshProUGUI로만 구성됩니다 - 체력바가 없다는 점만 빼면 UIMonsterHealthBar.cs와 완전히 같은
// "프리팹 컨트롤러" 패턴입니다(위치를 NPC 위로 옮기고 카메라를 향해 빌보드시키는 실제 로직은 이
// 스크립트가 아니라 NPCNameplate.cs가 담당하고, 이 스크립트는 순수하게 "이름 텍스트를 받아서 UI에
// 반영"만 합니다).
//
// [프리팹 준비]
//   1) 빈 오브젝트를 만들고 Canvas 컴포넌트를 추가하세요. Render Mode를 World Space로 바꾸세요.
//   2) Canvas의 RectTransform Width/Height를 적당히 작게(예: 100 x 26) 잡고, 이 오브젝트(또는
//      Canvas)의 Scale을 0.01 근처로 줄이세요 - World Space Canvas는 기본 픽셀 단위 크기 그대로
//      두면 NPC보다 훨씬 커 보이므로 반드시 축소해야 합니다.
//   3) Canvas 안에 이름을 표시할 TextMeshProUGUI를 하나 만드세요(가운데 정렬 추천, 배경이 있으면
//      가독성에 도움이 됩니다 - 선택사항).
//   4) 이 스크립트를 Canvas 오브젝트(또는 그 자식 아무 곳)에 붙이고, 이름 텍스트를 Txt Name
//      필드에 연결하세요.
//   5) 완성된 프리팹을 각 NPC의 NPCNameplate.cs 컴포넌트의 Nameplate Prefab 필드에 연결하세요.
// ============================================================================

using TMPro;
using UnityEngine;


public class UINPCNameplate : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtName;

    /// <summary>NPC 이름을 표시합니다. NPCNameplate가 Awake() 시점에 한 번만 호출합니다(이름은 게임
    /// 도중 바뀌지 않으므로 체력바처럼 매 프레임 갱신할 필요가 없습니다).</summary>
    public void SetName(string npcName)
    {
        if (_txtName == null) return;
        _txtName.text = npcName;
    }
}