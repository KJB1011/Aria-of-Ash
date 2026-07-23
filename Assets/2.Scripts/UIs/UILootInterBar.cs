// ============================================================================
// UILootInterBar.cs
// ----------------------------------------------------------------------------
// UIIngameInteraction(플레이어 옆 상호작용 목록)의 Content 안에 하나씩 추가되는 항목 프리팹입니다.
// 범위 안의 상호작용 가능한 대상 하나마다 이 프리팹이 Instantiate되어 이름을 보여주고,
// 지금 마우스 휠로 선택된 대상이면 체크마크가 켜집니다.
//
// [프리팹 준비]
//   TextMeshProUGUI(_txtLootName)와 체크마크 Image(_imgCheckMark)를 인스펙터에서 연결해두면 됩니다.
//   UIIngameInteraction이 Instantiate 직후 SetLootInterBar()로 이름을, 매 프레임 SetCheckMark()로
//   선택 여부를 갱신해줍니다.
// ============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILootInterBar : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtLootName;
    [SerializeField] GameObject _checkMark;


    /// <summary>이름을 설정하는 함수입니다 (IInteractable.InteractionName을 그대로 넘겨주면 됩니다 - 개수가 이미 포함되어 있습니다).</summary>
    public void SetLootInterBar(string name)
    {
        _txtLootName.text = name;
    }

    /// <summary>지금 선택되어 있는지 알려주는 함수입니다.</summary>
    public void SetCheckMark(bool isOn)
    {
        _checkMark.SetActive(isOn);
    }
}