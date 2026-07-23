// ============================================================================
// UIDialogueChoiceButton.cs
// ----------------------------------------------------------------------------
// UIDialogue가 선택지 개수(0~3개)만큼 그때그때 Instantiate/Destroy하는 선택지 버튼 프리팹입니다.
// UIInventoryBar와 같은 패턴입니다 - 이 버튼이 클릭되면 OnClicked 이벤트로 "내가 몇 번째
// 선택지인지"만 알려주고, 실제 진행(TalkManager.SelectChoice() 호출)은 UIDialogue가 담당합니다.
//
// [프리팹 준비]
//   1) TextMeshProUGUI를 Txt Choice 필드에 연결하세요.
//   2) 프리팹 루트(또는 클릭 영역)에 Button 컴포넌트를 추가하고, OnClick에 이 스크립트의
//      OnClickButton()을 연결하세요 - 다른 UI들과 동일하게 Button → OnClick → 함수 방식입니다.
// ============================================================================

using System;
using TMPro;
using UnityEngine;

public class UIDialogueChoiceButton : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _txtChoice;

    /// <summary>이 버튼이 클릭되면(Button OnClick → OnClickButton()) 발생하는 이벤트입니다. 인자는
    /// 이 버튼의 선택지 index입니다 - UIDialogue가 구독해서 TalkManager.SelectChoice()에 그대로
    /// 넘깁니다.</summary>
    public event Action<int> OnClicked;

    private int choiceIndex;

    /// <summary>이 버튼이 나타내는 선택지의 index와 표시할 텍스트를 설정합니다.</summary>
    public void Setup(int index, string text)
    {
        choiceIndex = index;
        _txtChoice.text = text;
    }

    /// <summary>이 버튼의 Button OnClick에 연결하세요.</summary>
    public void OnClickButton()
    {
        OnClicked?.Invoke(choiceIndex);
    }
}