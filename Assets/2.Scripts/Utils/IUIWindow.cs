// ============================================================================
// IUIWindow.cs
// ----------------------------------------------------------------------------
// UICanvas가 "팝업"으로 관리하는 UI 창이 구현하는 인터페이스입니다. 실제로 보이기/숨기기
// (CanvasGroup 페이드 등)는 각 UI가 자기 방식대로 처리하고, UICanvas는 그냥 Open()/Close()를
// 호출해주기만 합니다 - 언제 열고 닫을지, 그리고 그 동안 게임 시간을 멈출지만 UICanvas가 관리합니다.
//
// [경고 - 버튼 OnClick에 Open()/Close()를 직접 연결하지 마세요]
//   Open()/Close()는 public이라 Unity Inspector의 Button OnClick 드롭다운에도 그대로 나타나지만,
//   이 둘은 CanvasGroup 페이드/커서 복원 등 "보이기/숨기기"만 담당할 뿐 UICanvas.currentPopup을
//   비우거나 Time.timeScale을 되돌리는 일은 하지 않습니다(그건 UICanvas.OpenUI()/CloseUI()의
//   책임입니다). 실제로 있었던 버그: 인벤토리 창의 X 버튼을 UIInventory.Close()에 직접 연결했더니,
//   창은 정상적으로 페이드 아웃되어 화면은 돌아온 것처럼 보이는데 UICanvas.currentPopup이 그
//   인벤토리 오브젝트를 계속 가리키고 Time.timeScale도 0에서 돌아오지 않아 게임 전체가 멈춘 채로
//   남는 문제가 있었습니다. 각 창의 여닫기 버튼(X/취소 버튼 포함)은 반드시 그 창 자신의
//   ToggleXxx()/ClickExitButton() 같은 래퍼 함수(내부에서 UICanvas.Instance.OpenUI()/CloseUI()를
//   호출하는 함수)에만 연결하세요 - Open()/Close()를 직접 구현하는 각 클래스도 그 위에 "직접
//   호출하지 말라"는 경고 주석을 달아뒀습니다.
// ============================================================================

public interface IUIWindow
{
    void Open();
    void Close();
}