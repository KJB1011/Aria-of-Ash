// ============================================================================
// UIDialogueChoiceButton.cs
// ----------------------------------------------------------------------------
// UIDialogue가 선택지 개수(0~3개)만큼 그때그때 Instantiate/Destroy하는 선택지 버튼 프리팹입니다.
// UIInventoryBar와 같은 패턴입니다 - 이 버튼이 클릭되면 OnClicked 이벤트로 "내가 몇 번째
// 선택지인지"만 알려주고, 실제 진행(TalkManager.SelectChoice() 호출)은 UIDialogue가 담당합니다.
//
// [클릭 효과음]
// 이 프리팹 하나가 일반 대화 선택지와 퀘스트 관련 선택지(수락/거절/보고 - TalkScript.Choice.questToTurnIn
// 등)를 전부 표시하는 데 재사용되므로, choiceSfxName을 여기 한 곳에만 설정해두면 어떤 종류의 선택지를
// 클릭하든 공통으로 재생됩니다. 일반 메뉴 버튼들은 각자의 ClickXButton() 안에서
// SoundManager.Instance.PlayUIClickSfx()를 직접 호출하는 방식(SoundManager.cs의 uiClickSfxName 참고)을
// 쓰므로, 이 선택지 전용 choiceSfxName과는 완전히 별개로 관리됩니다 - 서로 다른 소리를 원하면 이 값과
// SoundManager.uiClickSfxName을 다르게 설정하면 됩니다.
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

    [Tooltip("이 선택지를 클릭했을 때 재생할 효과음입니다(Resources/SFX/ 아래 클립 이름과 일치해야 함). " +
              "일반 대화 선택지와 퀘스트 수락/거절/보고 선택지 모두 이 프리팹을 공유하므로 여기 한 값이 " +
              "공통으로 적용됩니다. 비워두면 재생하지 않습니다.")]
    public string choiceSfxName = "UI_Click";

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
        if (!string.IsNullOrEmpty(choiceSfxName))
        {
            SoundManager.Instance.PlaySFX(choiceSfxName);
        }

        OnClicked?.Invoke(choiceIndex);
    }
}