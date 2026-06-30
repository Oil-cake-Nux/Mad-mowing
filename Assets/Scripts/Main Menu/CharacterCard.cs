using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;

namespace Vampire
{
    /// <summary>
    /// 角色卡片类
    /// 负责显示角色的基础属性、初始能力，并处理角色购买/选择的交互逻辑
    /// </summary>
    public class CharacterCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText; // 角色名称显示文本（TMPro）
        [SerializeField] private Image characterImage; // 角色外观显示图片
        [SerializeField] private RectTransform characterImageRect; // 角色图片的矩形变换（用于控制尺寸布局）
        [SerializeField] private TextMeshProUGUI hpText; // 角色生命值（HP）显示文本
        [SerializeField] private TextMeshProUGUI armorText; // 角色护甲值显示文本
        [SerializeField] private TextMeshProUGUI mvspdText; // 角色移动速度显示文本
        [SerializeField] private TextMeshProUGUI luckText; // 角色幸运值显示文本
        [SerializeField] private TextMeshProUGUI buttonText; // 卡片按钮文本（购买/选择）
        [SerializeField] private LocalizedString buyLocalization, selectLocalization; // 按钮文本本地化配置（购买/选择）
        [SerializeField] private Image buttonImage; // 卡片按钮背景图片
        [SerializeField] private Color selectColor, buyColor; // 按钮颜色（选择状态/购买状态）
        [SerializeField] private RectTransform startingAbilitiesParent; // 初始能力图标的父物体（用于布局）
        [SerializeField] private GameObject startingAbilityContainerPrefab; // 初始能力容器预制体（承载单个能力图标）
        [SerializeField] private Vector2 startingAbilitiesRectSize = new Vector2(365, 85); // 初始能力区域的总尺寸
        private CharacterSelector characterSelector; // 角色选择器引用（用于启动游戏）
        private CharacterBlueprint characterBlueprint; // 角色蓝图（存储角色的配置数据：属性、成本、初始能力等）
        private CoinDisplay coinDisplay; // 金币显示组件引用（用于更新金币UI）
        private StartingAbilityContainer[] startingAbilityContainers; // 初始能力容器数组（存储实例化后的能力UI）
        private bool initialized; // 初始化标记（避免未初始化时更新UI）
        
        /// <summary>
        /// 组件启用时调用
        /// 监听购买文本的本地化字符串变化事件，用于多语言切换时更新按钮文本
        /// </summary>
        private void OnEnable()
        {
            buyLocalization.StringChanged += UpdateButtonText;
            
        }

        /// <summary>
        /// 组件禁用时调用
        /// 取消监听购买文本的本地化字符串变化事件，防止内存泄漏
        /// </summary>
        private void OnDisable()
        {
            buyLocalization.StringChanged -= UpdateButtonText;
        }

        /// <summary>
        /// 初始化角色卡片
        /// </summary>

        public void Init(CharacterSelector characterSelector, CharacterBlueprint characterBlueprint, CoinDisplay coinDisplay)
        {
            this.characterSelector = characterSelector;
            this.characterBlueprint = characterBlueprint;
            this.coinDisplay = coinDisplay;

            // 设置角色图片（取行走动画序列的第一张精灵）
            characterImage.sprite = characterBlueprint.walkSpriteSequence[0];

            // 赋值角色基础属性文本
            nameText.text = characterBlueprint.name.ToString();
            hpText.text = characterBlueprint.hp.ToString();
            armorText.text = characterBlueprint.armor.ToString();
            // 计算并显示移动速度（按比例转换为百分比格式，四舍五入取整）
            mvspdText.text = Mathf.RoundToInt(characterBlueprint.movespeed / 1.15f * 100f).ToString() + "%";
            luckText.text = characterBlueprint.luck.ToString();

            // 更新按钮文本和颜色
            UpdateButtonText();
            buttonImage.color = characterBlueprint.owned ? selectColor : buyColor;

            // 实例化初始能力容器（根据角色蓝图中的初始能力数量创建），每个容器中只有一个能力
            startingAbilityContainers = new StartingAbilityContainer[characterBlueprint.startingAbilities.Length];
            for (int i = 0; i < characterBlueprint.startingAbilities.Length; i++)
            {
                // 实例化能力容器并挂载到父物体上
                startingAbilityContainers[i] = Instantiate(startingAbilityContainerPrefab, startingAbilitiesParent).GetComponent<StartingAbilityContainer>();
                // 设置能力图标（从能力组件中获取对应图片）
                startingAbilityContainers[i].AbilityImage.sprite = characterBlueprint.startingAbilities[i].GetComponent<Ability>().Image;
            }

            // 标记初始化完成
            initialized = true;
        }

        /// <summary>
        /// 更新卡片布局（角色图片 + 初始能力图标）
        /// 保证图片按比例缩放，不超出指定区域
        /// </summary>
        public void UpdateLayout()
        {
            // 角色图片布局计算
            float yHeight = Mathf.Abs(characterImageRect.sizeDelta.y); // 获取角色图片矩形的原始高度
            // 根据角色精灵的宽高比，计算对应的宽度（保持比例不变）
            float xWidth = characterBlueprint.walkSpriteSequence[0].textureRect.width / (float)characterBlueprint.walkSpriteSequence[0].textureRect.height * yHeight;

            // 如果计算出的宽度超过矩形最大宽度，重新按宽度限制计算高度
            if (xWidth > Mathf.Abs(characterImageRect.sizeDelta.x))
            {
                xWidth = Mathf.Abs(characterImageRect.sizeDelta.x);
                yHeight = characterBlueprint.walkSpriteSequence[0].textureRect.height / (float)characterBlueprint.walkSpriteSequence[0].textureRect.width * xWidth;
            }
            // 应用计算后的尺寸到角色图片
            ((RectTransform)characterImage.transform).sizeDelta = new Vector2(xWidth, yHeight);

            // 初始能力图标布局计算
            // 计算每个能力图标的最大可用宽度（总宽度 ÷ 能力数量）
            float maxImageWidth = startingAbilitiesRectSize.x / startingAbilityContainers.Length;
            for (int i = 0; i < startingAbilityContainers.Length; i++)
            {
                StartingAbilityContainer startingAbilityContainer = startingAbilityContainers[i];
                float imageHeight = startingAbilitiesRectSize.y; // 能力图标的原始高度
                // 根据能力精灵的宽高比，计算对应的宽度（保持比例不变）
                float imageWidth = startingAbilityContainer.AbilityImage.sprite.textureRect.width / (float)startingAbilityContainer.AbilityImage.sprite.textureRect.height * imageHeight;

                // 如果计算出的宽度超过最大可用宽度，重新按宽度限制计算高度
                if (imageWidth > maxImageWidth)
                {
                    imageWidth = maxImageWidth;
                    imageHeight = startingAbilityContainer.AbilityImage.sprite.textureRect.height / (float)startingAbilityContainer.AbilityImage.sprite.textureRect.width * imageWidth;
                }
                // 应用计算后的尺寸到能力图标
                startingAbilityContainer.ImageRect.sizeDelta = new Vector2(imageWidth, imageHeight);
            }
        }

        /// <summary>
        /// 卡片按钮点击事件（购买/选择角色的核心逻辑）
        /// </summary>
        public void Selected()
        {
            // 角色未被拥有时，执行购买逻辑
            if (!characterBlueprint.owned)
            {
                // 获取玩家当前金币数量（从PlayerPrefs持久化存储中读取）
                int coinCount = PlayerPrefs.GetInt("Coins");
                // 检查金币是否足够购买该角色
                if (coinCount >= characterBlueprint.cost)
                {
                    // 扣除购买成本，更新持久化金币数量
                    PlayerPrefs.SetInt("Coins", coinCount - characterBlueprint.cost);
                    // 标记角色为已拥有
                    characterBlueprint.owned = true;
                    // 更新按钮文本和颜色
                    UpdateButtonText();
                    buttonImage.color = selectColor;
                    // 更新金币显示UI
                    coinDisplay.UpdateDisplay();
                }
            }
            else
            {
                // 角色已被拥有时，通知角色选择器启动游戏
                characterSelector.StartGame(characterBlueprint);
            }
        }

        /// <summary>
        /// 本地化字符串变化时的回调方法（带参数重载）
        /// 内部调用无参的按钮文本更新方法
        /// </summary>
        /// <param name="text">变化后的本地化文本（未实际使用，仅作为回调参数）</param>
        private void UpdateButtonText(string text)
        {
            UpdateButtonText();
        }

        /// <summary>
        /// 更新按钮文本内容
        /// 根据角色是否已拥有，切换“购买”或“选择”文本
        /// </summary>
        private void UpdateButtonText()
        {
            // 未初始化时直接返回，避免空引用错误
            if (!initialized) return;

            // 角色已拥有：显示选择文本（本地化）
            if (characterBlueprint.owned)
            {
                buttonText.text = selectLocalization.GetLocalizedString();
            }
            // 角色未拥有：显示购买文本 + 价格（格式化字符串）
            else
            {
                buttonText.text = String.Format("{0} (${1})", buyLocalization.GetLocalizedString(), characterBlueprint.cost);
            }
        }
    }
}