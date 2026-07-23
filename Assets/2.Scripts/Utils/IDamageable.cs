// ============================================================================
// IDamageable.cs
// ----------------------------------------------------------------------------
// 데미지를 받을 수 있는 대상(플레이어, 파괴 가능한 오브젝트 등)이 구현하는 인터페이스입니다.
// 투사체(ProjectileBase)는 맞은 대상이 이 인터페이스를 구현하고 있으면 TakeDamage를 호출합니다.
// 예) public class PlayerHealth : MonoBehaviour, IDamageable { public void TakeDamage(float amount) { ... } }
// ============================================================================

public interface IDamageable
{
    void TakeDamage(float amount);
}