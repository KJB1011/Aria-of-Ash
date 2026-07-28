// ============================================================================
// UIBossHPBar.cs
// ----------------------------------------------------------------------------
// 화면에 고정된 보스 체력바 UI입니다. World Space가 아니라 일반 Screen Space Canvas에 이름+레벨
// 표시용 TextMeshProUGUI + 체력 표시용 Slider(UnityEngine.UI.Slider)로 구성됩니다. 몬스터 머리
// 위를 따라다니는 UIMonsterHealthBar.cs와 달리 위치를 옮기거나 빌보드할 필요가 없어서, 이 스크립트는
// 순수하게 "이름/레벨 텍스트, 체력 비율(0~1)을 받아서 각각 텍스트/Slider 값에 반영"만 합니다
// (UIMonsterHealthBar.cs와 같은 "프리팹 컨트롤러" 패턴 - 실제로 언제 어떤 값을 넣을지는 보스 쪽
// 스크립트, 예: MiddleSlimeBoss.cs가 결정합니다).
//
// [프리팹/씬 준비]
//   1) Canvas(Screen Space - Overlay 또는 Camera)를 화면 위쪽 등 원하는 위치에 배치하세요.
//   2) 이름+레벨을 표시할 TextMeshProUGUI를 하나 만드세요.
//   3) 그 아래(또는 옆)에 UI > Slider로 체력바를 만드세요. Interactable은 꺼두세요(보여주기만
//      하는 용도입니다). Slider의 Min Value 0 / Max Value 1로 두면 SetHealthRate(0~1)를 그대로
//      value에 대입할 수 있습니다.
//   4) 이 스크립트를 Canvas 오브젝트(또는 그 자식 아무 곳)에 붙이고, 이름+레벨 텍스트를
//      Txt Name Level 필드에, Slider를 Health Slider 필드에 연결하세요.
//   5) 씬에 배치해둔 이 오브젝트를 보스 쪽 스크립트(예: MiddleSlimeBoss.Boss Hp Bar 필드)에
//      연결하세요.
//
// [표시 형식]
//   SetInfo(bossName, level)를 호출하면 "[보스 이름]   Lv. [레벨]" 형식으로 표시됩니다
//   (예: "미들 슬라임   Lv. 25"). 이름/레벨은 보스가 등장할 때(또는 씬 시작 시) 한 번만
//   반영하면 되고, 전투 중 계속 바뀌는 체력만 SetHealthRate()로 매 프레임 갱신하면 됩니다.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIBossHPBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtNameLevel;
    [SerializeField] Slider _healthSlider;

    /// <summary>보스 이름과 레벨을 "[이름]   Lv. [레벨]" 형식으로 표시합니다. 보스 쪽 스크립트가
    /// 등장 시점에 한 번만 호출하면 됩니다(이름/레벨은 전투 중 바뀌지 않으므로 체력처럼 매 프레임
    /// 갱신할 필요가 없습니다).</summary>
    public void SetInfo(string bossName, int level)
    {
        if (_txtNameLevel == null) return;
        _txtNameLevel.text = $"{bossName}   Lv. {level}";
    }

    /// <summary>체력 비율(0~1)을 받아서 Slider의 value에 그대로 반영합니다. 범위 밖 값이 들어와도
    /// 0~1로 안전하게 잘라냅니다. Slider의 Min/Max Value가 0/1로 설정되어 있어야 정확히 맞습니다.</summary>
    public void SetHealthRate(float rate01)
    {
        if (_healthSlider == null) return;
        _healthSlider.value = Mathf.Clamp01(rate01);
    }
}