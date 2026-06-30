using UnityEngine;

namespace Vampire
{
    //定义「可受伤害实体」的通用接口，任何需要实现「受伤害」和「被击退」逻辑的实体（如角色、敌人等）都可继承该类。
    public abstract class IDamageable : MonoBehaviour
    {
        //用于处理受伤害逻辑
        public abstract void TakeDamage(float damage, Vector2 knockback = default(Vector2));
        //用于处理被击退逻辑
        public abstract void Knockback(Vector2 knockback);
    }
}
