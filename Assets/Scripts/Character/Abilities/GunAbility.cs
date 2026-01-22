using System.Collections;
using UnityEngine;

namespace Vampire
{
    //枪械类技能的的基类，重要功能是在 ProjectileAbility 基础上做“连发弹匣”
    public class GunAbility : ProjectileAbility
    {
        [Header("Gun Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected UpgradeableDamageRate firerate;  //射速

        protected override void Attack()
        {
            StartCoroutine(FireClip());
        }

        protected IEnumerator FireClip()
        {
            int clipSize = projectileCount.Value;
            //
            timeSinceLastAttack -= clipSize/firerate.Value;
            for (int i = 0; i < clipSize; i++)
            {
                LaunchProjectile();
                //停顿射速分之一秒
                yield return new WaitForSeconds(1/firerate.Value);
            }
        }
    }
}
