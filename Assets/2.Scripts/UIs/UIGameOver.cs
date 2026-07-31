// ============================================================================
// UIGameOver.cs
// ----------------------------------------------------------------------------
// 플레이어가 사망했을 때 뜨는 게임 오버 화면입니다. UIExit과 완전히 같은 구조로 GameManager의
// 자식에 두고 DontDestroyOnLoad로 씬을 넘나들며 계속 살아있게 합니다. 기능은 단순합니다 - 페이드
// 인으로 나타나고, 버튼 하나로 씬을 재시작합니다.
//
// [뜨는 순서 - 2단계 연출, GameManager.TriggerGameOver() 참고]
//   PlayerController.Die()가 호출되면 그 안에서 GameManager.Instance.TriggerGameOver()를 부릅니다.
//   그러면 순서대로:
//     1) GameManager가 먼저 화면 전체를 FadeOut(화면이 서서히 까맣게)합니다.
//     2) 완전히 까매지고 나서야 이 스크립트의 Show()를 호출해서, 이 게임 오버 화면 자신을
//        FadeIn(알파 0 → 1)으로 서서히 보여줍니다.
//   "화면이 어두워지고, 그 다음에 게임 오버 화면이 나타난다"는 이 2단계를 이 스크립트와 GameManager가
//   나눠서 담당합니다 - 1단계(화면 전체 페이드 아웃)는 GameManager.FadeOut()이, 2단계(이 화면 자체를
//   페이드 인)는 이 스크립트의 Show()가 맡습니다. 직접 Show()를 호출할 일은 거의 없고, 보통
//   GameManager.TriggerGameOver()를 통해서만 호출됩니다.
//
// [재시작]
//   재시작 버튼의 OnClick에 ClickRestartButton()을 연결하세요 - Ingame Scene Name에 지정한 씬을
//   다시 불러와서 처음부터 다시 시작합니다.
//
// [화면을 다시 가리지 않도록 - Hide()]
//   이 오브젝트는 GameManager처럼 DontDestroyOnLoad로 유지되므로, ClickRestartButton()으로 씬을
//   다시 불러와도 Awake()가 재실행되지 않아 Show()가 켜뒀던 알파/interactable/blocksRaycasts가
//   그대로 남아있습니다 - 손대지 않으면 재시작된 게임 화면을 계속 가리고 클릭도 막습니다. 그래서
//   GameManager가 새 씬이 다 로드된 직후 자동으로 Hide()를 호출해서 완전히 숨긴 상태로 되돌려줍니다
//   (GameManager.cs 상단 [게임 오버 화면 자동 리셋] 참고) - 따로 연결하거나 호출할 필요 없습니다.
//
// [씬 준비]
//   1) 게임 오버 화면 오브젝트(항상 활성화 상태로 두세요)에 이 스크립트와 CanvasGroup을 붙이세요
//      (CanvasGroup은 RequireComponent로 자동 추가됩니다).
//   2) GameManager 오브젝트의 자식으로 두세요(UIExit과 같은 자리) - GameManager가 DontDestroyOnLoad로
//      유지되면서 이 오브젝트도 함께 씬 전환에도 살아남습니다.
//   3) Ingame Scene Name에 재시작할 씬 이름(기본값 "IngameScene")을 확인/수정하세요.
//   4) 재시작 버튼의 OnClick에 이 컴포넌트의 ClickRestartButton()을 연결하세요.
// ============================================================================

using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class UIGameOver : MonoBehaviour
{
    /// <summary>씬(또는 DontDestroyOnLoad로 살아남은 오브젝트)에 하나만 있는 컴포넌트라, 다른
    /// 스크립트에서 여기로 바로 접근합니다.</summary>
    public static UIGameOver Instance { get; private set; }

    [Header("재시작")]
    [Tooltip("재시작 버튼을 누르면 다시 불러올 씬 이름입니다.")]
    public string ingameSceneName = "IngameScene";

    [Header("표시")]
    [Tooltip("이 화면 자신이 페이드 인(알파 0 → 1)하는 데 걸리는 시간(초)입니다. 화면 전체가 까맣게 " +
              "덮이는 시간(GameManager.gameOverFadeOutDuration)과는 별개입니다.")]
    public float fadeDuration = 1f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private bool isOpen;

    private void Awake()
    {
        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>게임 오버 화면을 페이드 인으로 보여줍니다. 보통 GameManager.TriggerGameOver()가 화면을
    /// 다 어둡게 만든 뒤에 대신 호출해줍니다 - 직접 호출할 일은 거의 없습니다. 재시작 버튼을 누를 수
    /// 있도록 커서를 풀어줍니다.</summary>
    public void Show()
    {
        if (isOpen) return;
        isOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true); // Time.timeScale이 0이어도 얼어붙지 않습니다.
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>재시작 버튼 OnClick에 연결하세요. Ingame Scene Name에 지정한 씬을 다시 불러와서
    /// 처음부터 다시 시작합니다.</summary>
    public void ClickRestartButton()
    {
        Time.timeScale = 1f; // 사망/다른 팝업 등으로 timeScale이 0에 멈춰있었을 수 있으니, 새 씬을 시작하기 전에 원래대로 되돌립니다.
        SceneManager.LoadScene(ingameSceneName);
    }

    /// <summary>이 화면을 다시 완전히 숨긴 상태로 되돌립니다 - GameManager가 새 씬이 다 로드된 뒤에
    /// 자동으로 호출해줍니다(GameManager.HandleSceneLoaded() 참고). 이 오브젝트는 DontDestroyOnLoad로
    /// 유지되어 Awake()가 재시작 때 다시 실행되지 않으므로, Show()로 켜졌던 알파/interactable/
    /// blocksRaycasts/isOpen을 여기서 직접 초기 상태로 되돌리지 않으면 새로 시작된 게임 화면을 계속
    /// 가리고 클릭도 막게 됩니다. Show()가 풀어줬던 커서도 게임플레이 기본값(잠금+숨김)으로 되돌립니다 -
    /// 새 씬에서 다른 값을 쓰고 싶다면 그 씬의 시작 스크립트에서 다시 조정하세요.</summary>
    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        fadeTween?.Kill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}