// ============================================================================
// GameManager.cs
// ----------------------------------------------------------------------------
// 씬을 넘나들며 계속 살아있는 전역 매니저입니다. 지금은 UIExit(종료 확인창)를 자식으로 붙잡고
// 있다가 DontDestroyOnLoad로 씬 전환에도 파괴되지 않게 하는 역할만 합니다 - 나중에 로그인 정보 등
// 다른 전역 데이터가 필요해지면 이 스크립트에 계속 이어서 추가하시면 됩니다.
//
// [씬 준비]
//   1) 로그인 씬(또는 게임이 가장 먼저 시작하는 씬)에 빈 오브젝트를 만들고 이 스크립트를 붙이세요.
//   2) 그 오브젝트의 자식으로 UIExit이 붙어있는 팝업 오브젝트(Canvas 포함)를 두세요 - 부모가
//      DontDestroyOnLoad되면 자식도 함께 유지되므로, UIExit도 씬이 바뀌어도 계속 살아있고
//      다른 씬에서도 UIExit.Instance로 그대로 접근할 수 있습니다.
//   3) 씬에 이 스크립트를 가진 오브젝트가 정확히 하나만 있으면 됩니다 - 씬을 다시 불러오는 등
//      두 번째 GameManager가 생겨도 Awake()가 자동으로 기존 것을 유지하고 새로 생긴 걸
//      제거합니다(SoundManager.cs의 중복 방지 패턴과 동일).
//
// [UIExit 접근]
//   다른 스크립트는 UIExit의 static Instance로 바로 접근하면 됩니다: UIExit.Instance.Show().
//   이 스크립트는 그 UIExit이 씬 전환에도 살아있도록 "부모" 역할만 할 뿐, 굳이 이 스크립트를
//   거칠 필요는 없습니다. 다만 씬 연결이 잘 됐는지 바로 확인해볼 수 있도록 Exit 프로퍼티로도
//   꺼내볼 수 있게 해뒀습니다(GameManager.Instance.Exit).
// ============================================================================

using UnityEngine;

public class GameManager : MonoBehaviour
{
    /// <summary>씬을 넘나들며 하나만 유지되는 컴포넌트라, 다른 스크립트에서 여기로 바로 접근합니다.</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>자식으로 붙어있는 UIExit입니다. 없어도(아직 연결 안 해도) null만 담기고 에러는 나지
    /// 않습니다 - UIExit은 보통 UIExit.Instance로 직접 접근하므로, 이 프로퍼티는 씬 연결을 확인하는
    /// 용도 정도로 생각하시면 됩니다.</summary>
    public UIExit Exit { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 씬 전환 등으로 인해 두 번째 GameManager가 생기면 기존 것을 유지하고 새로 생긴 걸 제거합니다.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Exit = GetComponentInChildren<UIExit>(true); // 비활성화된 자식도 찾도록 true를 넘깁니다.
    }
}