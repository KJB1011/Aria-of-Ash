// ============================================================================
// UILobby.cs
// ----------------------------------------------------------------------------
// LobbyScene에서 "게임 시작" 버튼을 누르면 화면을 페이드 아웃한 뒤 IngameScene으로 이동시키는
// 스크립트입니다. 실제 페이드/씬 전환은 새로 만들지 않고 GameManager.LoadSceneWithFade()를 그대로
// 재사용합니다(GameManager.cs 상단 [일반적인 씬 전환] 참고) - 화면이 완전히 까매진 뒤에 씬을 불러오고,
// 새 씬이 다 준비되면 자동으로 다시 페이드 인까지 이어집니다. UIGameOver의 재시작 흐름과 같은 페이드
// 인프라를 공유하지만, 그쪽(게임 오버 재시작)과는 완전히 독립적인 경로라 서로 간섭하지 않습니다.
//
// [씬 준비]
//   1) LobbyScene에 GameStart 버튼을 만들고, 이 스크립트를 아무 오브젝트에나 붙인 뒤(버튼 자신에
//      붙여도 됩니다) 버튼의 OnClick에 이 컴포넌트의 ClickGameStartButton()을 연결하세요.
//   2) [중요] LobbyScene에 GameManager가 있어야 합니다 - LobbyScene이 이제 게임이 가장 먼저 시작하는
//      씬이 되었으니, 지금까지 다른 씬에 배치해뒀던 GameManager(Fade Canvas Group/UIExit/UIGameOver
//      자식 구성 포함)를 통째로 LobbyScene으로 옮기세요(GameManager.cs 상단 [씬 준비] 참고). 씬을
//      옮기기만 하면 되고 구성은 그대로 유지하면 됩니다 - GameManager는 DontDestroyOnLoad라 이후
//      IngameScene으로 넘어가도 계속 살아있습니다.
//   3) Ingame Scene Name에 실제 인게임 씬 이름을 정확히 입력하세요 - File > Build Settings의 Scenes
//      In Build 목록에 LobbyScene과 함께 등록되어 있어야 SceneManager.LoadScene()이 찾을 수 있습니다.
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class UILobby : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("게임 시작 버튼을 누르면 불러올 인게임 씬 이름입니다. Build Settings의 Scenes In Build에 " +
              "등록되어 있어야 합니다.")]
    public string ingameSceneName = "IngameScene";
    [Tooltip("게임 시작 버튼을 누른 뒤 화면이 완전히 까매질 때까지 걸리는 시간(초)입니다.")]
    public float fadeOutDuration = 1f;
    [Tooltip("인게임 씬이 다 준비된 뒤, 화면이 다시 보이기까지(페이드 인) 걸리는 시간(초)입니다.")]
    public float fadeInDuration = 1f;

    // 페이드/씬 전환이 시작된 뒤 버튼을 또 눌러도 중복으로 시작되지 않도록 막습니다. 씬이 실제로
    // 바뀌면 이 오브젝트도 함께 파괴되므로 별도로 false로 되돌릴 필요가 없습니다.
    private bool isTransitioning;

    /// <summary>GameStart 버튼의 OnClick에 연결하세요. 화면을 fadeOutDuration초에 걸쳐 까맣게 만든 뒤
    /// ingameSceneName을 불러오고, 새 씬이 준비되면 자동으로 다시 페이드 인됩니다
    /// (GameManager.LoadSceneWithFade() 참고).</summary>
    public void ClickGameStartButton()
    {
        if (isTransitioning) return;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[UILobby] GameManager.Instance가 없어 페이드 연출 없이 씬을 바로 불러옵니다. " +
                              "LobbyScene에 GameManager가 배치되어 있는지 확인하세요.", this);
            SceneManager.LoadScene(ingameSceneName);
            return;
        }

        isTransitioning = true;
        GameManager.Instance.LoadSceneWithFade(ingameSceneName, fadeOutDuration, fadeInDuration);
    }
}