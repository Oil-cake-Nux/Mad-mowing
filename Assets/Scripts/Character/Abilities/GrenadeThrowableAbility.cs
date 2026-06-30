using System.Collections;
using UnityEngine;

namespace Vampire
{
    //扔手雷（落地爆炸碎片）技能类
    public class GrenadeThrowableAbility : ThrowableAbility
    {
        [Header("Grenade Stats")]
        [SerializeField] protected UpgradeableProjectileCount fragmentCount;

        protected override void LaunchThrowable()
        {
            GrenadeThrowable throwable = (GrenadeThrowable) entityManager.SpawnThrowable(throwableIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, 0, monsterLayer);
            throwable.SetupGrenade(fragmentCount.Value);
            throwable.Throw((Vector2)playerCharacter.transform.position + Random.insideUnitCircle * throwRadius);
            throwable.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
        }
    }
}
