// ============================================================================
// UIMonsterHealthBar.cs
// ----------------------------------------------------------------------------
// 몬스터 머리 위에 떠 있는 체력바 "프리팹" 쪽 스크립트입니다. World Space Canvas 하나에 이름
// 표시용 TextMeshProUGUI + 체력 표시용 Slider(UnityEngine.UI.Slider)로 구성됩니다. 실제로 이
// 프리팹을 Instantiate하고, 몬스터를 따라 위치를 옮기고, 카메라를 향해 계속 회전(빌보드)시키는
// 로직은 몬스터 쪽에 붙는 MonsterHealthBar.cs가 담당합니다 - 이 스크립트는 순수하게 "이름
// 텍스트/체력 비율(0~1)을 받아서 각각 텍스트/Slider 값에 반영"만 합니다 (UIDialogueChoiceButton/
// UIInventoryBar와 같은 "프리팹 컨트롤러" 패턴입니다).
//
// [프리팹 준비]
//   1) 빈 오브젝트를 만들고 Canvas 컴포넌트를 추가하세요. Render Mode를 World Space로 바꾸세요.
//   2) Canvas의 RectTransform Width/Height를 적당히 작게(예: 100 x 26) 잡고, 이 오브젝트(또는
//      Canvas)의 Scale을 0.01 근처로 줄이세요 - World Space Canvas는 기본 픽셀 단위 크기 그대로
//      두면 몬스터보다 훨씬 커 보이므로 반드시 축소해야 합니다.
//   3) Canvas 위쪽에 이름을 표시할 TextMeshProUGUI를 하나 만드세요(가운데 정렬 추천).
//   4) 그 아래에 UI > Slider로 체력바를 만드세요. Interactable은 꺼두세요(플레이어가 직접
//      조작할 UI가 아니라 보여주기만 하는 용도입니다 - Handle Slide Area/Handle도 안 쓸 거면
//      지워도 됩니다). Slider의 Min Value 0 / Max Value 1로 두면 SetHealthRate(0~1)를 그대로
//      value에 대입할 수 있습니다.
//   5) 이 스크립트를 Canvas 오브젝트(또는 그 자식 아무 곳)에 붙이고, 이름 텍스트를 Txt Name
//      필드에, Slider를 Health Slider 필드에 연결하세요.
//   6) 완성된 프리팹을 각 몬스터의 MonsterHealthBar.cs 컴포넌트의 Bar Prefab 필드에 연결하세요.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMonsterHealthBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtName;
    [SerializeField] Slider _healthSlider;

    /// <summary>몬스터 이름을 표시합니다. MonsterHealthBar가 Awake() 시점에 한 번만 호출합니다
    /// (이름은 게임 도중 바뀌지 않으므로 체력처럼 매 프레임 갱신할 필요가 없습니다).</summary>
    public void SetName(string monsterName)
    {
        if (_txtName == null) return;
        _txtName.text = monsterName;
    }

    /// <summary>체력 비율(0~1)을 받아서 Slider의 value에 그대로 반영합니다. 범위 밖 값이 들어와도
    /// 0~1로 안전하게 잘라냅니다. Slider의 Min/Max Value가 0/1로 설정되어 있어야 정확히 맞습니다.</summary>
    public void SetHealthRate(float rate01)
    {
        if (_healthSlider == null) return;
        _healthSlider.value = Mathf.Clamp01(rate01);
    }
}