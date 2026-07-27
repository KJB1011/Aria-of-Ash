// ============================================================================
// MonsterActivation.cs
// ----------------------------------------------------------------------------
// 몬스터를 플레이어와의 거리 기반으로 자동 활성화/비활성화 시켜주는 최적화용 컴포넌트입니다.
// 실제 거리 검사와 켜고 끄는 판단은 전부 MonsterActivationManager가 담당하고, 이 컴포넌트는
// 그 매니저에게 "나 여기 있어요"라고 등록/해제만 해주는 아주 얇은 연결 다리입니다.
//
// [왜 몬스터 스스로 검사하지 않는가]
//   오브젝트가 비활성화(SetActive(false))되면 그 순간 이 몬스터 위의 모든 컴포넌트의 Update()가
//   같이 멈춥니다. 그래서 몬스터 자신은 "내가 다시 범위 안에 들어왔는지"를 스스로 확인할 방법이
//   없습니다 - 항상 켜져있는 중앙 매니저(MonsterActivationManager)가 대신 거리를 검사하고 켜고
//   끄는 역할을 합니다. 자세한 동작 방식은 MonsterActivationManager.cs를 참고하세요.
//
// [씬 준비]
//   MonsterFSM을 상속한 몬스터(Slime/WoodGolem 등)와 MiddleSlimeBoss에는 이미 [RequireComponent]로
//   자동으로 붙어있습니다 - 별도 설정 없이 그대로 두면 됩니다. 활성화 반경/검사 주기는 이 컴포넌트가
//   아니라 MonsterActivationManager 쪽 값을 조절하세요.
//
// [주의 - 죽는 도중(Die) 비활성화되는 경우]
//   플레이어가 몬스터를 처치한 직후(dieDelay 동안 시체가 남아있는 사이) 아주 멀리 이동하면 시체가
//   범위 밖으로 벗어나 잠깐 비활성화될 수 있습니다. 이 경우에도 Destroy(gameObject, dieDelay) 예약은
//   활성/비활성 여부와 무관하게 정확한 시점에 그대로 실행되니(Unity 엔진이 직접 관리) 신경 쓸 필요
//   없습니다 - 사실상 무해한 경계 케이스라 별도로 예외 처리하지 않았습니다.
// ============================================================================

using UnityEngine;

public class MonsterActivation : MonoBehaviour
{
    private void Awake()
    {
        MonsterActivationManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        // InstanceIfExists를 씁니다 - Instance(자동 생성 프로퍼티)를 쓰면 앱/씬 종료 시점에
        // 매니저가 먼저 파괴된 뒤에도 해제하려다 새 매니저를 또 만들어버리는 낭비가 생길 수 있습니다.
        MonsterActivationManager existingManager = MonsterActivationManager.InstanceIfExists;
        if (existingManager != null)
        {
            existingManager.Unregister(this);
        }
    }
}