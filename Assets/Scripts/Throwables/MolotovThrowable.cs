using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    /// <summary>
    /// 燃烧瓶投掷物类，继承自投掷物基类，处理燃烧瓶的爆炸、燃烧逻辑
    /// </summary>
    public class MolotovThrowable : Throwable
    {
        [SerializeField] protected MolotovFire molotovFire; // 燃烧瓶燃烧效果的逻辑组件
        [SerializeField] protected ParticleSystem molotovExplosion; // 燃烧瓶爆炸时的粒子效果
        protected float duration; // 火焰持续时间
        protected float fireRadius; // 火焰影响半径
        protected float fireDamageRate; // 火焰每秒伤害频率（或伤害间隔相关参数）

        /// <summary>
        /// 设置燃烧瓶火焰的属性
        /// </summary>
        /// <param name="duration">火焰持续时间</param>
        /// <param name="fireRadius">火焰影响半径</param>
        /// <param name="fireDamageRate">火焰伤害频率</param>
        public void SetupFire(float duration, float fireRadius, float fireDamageRate)
        {
            this.duration = duration;
            this.fireRadius = fireRadius;
            this.fireDamageRate = fireDamageRate;
        }

        /// <summary>
        /// 重写父类的爆炸方法，触发燃烧逻辑
        /// </summary>
        protected override void Explode()
        {
            // 启动燃烧协程
            StartCoroutine(Burn());
        }

        /// <summary>
        /// 燃烧协程，处理燃烧瓶从爆炸到燃烧结束的整个流程
        /// </summary>
        /// <returns>协程迭代器</returns>
        protected IEnumerator Burn()
        {
            // 隐藏投掷物的精灵和阴影（爆炸后投掷物本身消失）
            throwableSpriteRenderer.enabled = false;
            shadowSpriteRenderer.enabled = false;
            // 激活火焰效果对象
            molotovFire.gameObject.SetActive(true);
            // 播放爆炸粒子效果
            molotovExplosion.Play();
            // 等待火焰燃烧过程完成（执行火焰伤害、范围检测等逻辑）
            // 传入当前燃烧瓶数据：伤害值、击退力、持续时间、半径、伤害频率、目标图层
            yield return StartCoroutine(molotovFire.Burn(this, damage, knockback, duration, fireRadius, fireDamageRate, targetLayer));
            // 燃烧结束后关闭火焰效果
            molotovFire.gameObject.SetActive(false);
            // 恢复投掷物精灵和阴影（可能用于复用，实际销毁前临时恢复）
            throwableSpriteRenderer.enabled = true;
            shadowSpriteRenderer.enabled = true;
            // 销毁投掷物
            DestroyThrowable();
        }

        /// <summary>
        /// 对可受伤害对象造成伤害
        /// </summary>
        /// <param name="damageable">可受伤害的对象接口</param>
        public void Damage(IDamageable damageable)
        {
            // 计算从投掷物到受伤害对象的击退方向（归一化向量）
            Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
            // 调用受伤害对象的受击方法，传入伤害值和带方向的击退力
            damageable.TakeDamage(damage, knockback * knockbackDirection);
            // 触发玩家角色的造成伤害事件（可能用于成就、音效等逻辑）
            playerCharacter.OnDealDamage.Invoke(damage);
        }
    }
}