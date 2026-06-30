using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vampire
{

    public class CharacterSelector : MonoBehaviour
    {
        [SerializeField] protected CharacterBlueprint[] characterBlueprints;
        [SerializeField] protected GameObject characterCardPrefab;
        [SerializeField] protected CoinDisplay coinDisplay;

        private CharacterCard[] characterCards;
        
        public void Init()
        {
            characterCards = new CharacterCard[characterBlueprints.Length];
            for (int i = 0; i < characterBlueprints.Length; i++)
            {
                characterCards[i] = Instantiate(characterCardPrefab, this.transform).GetComponent<CharacterCard>();
                characterCards[i].Init(this, characterBlueprints[i], coinDisplay);
            }
            //更新自动布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            //更新卡片布局
            for (int i = 0; i < characterBlueprints.Length; i++)
            {
                characterCards[i].UpdateLayout();
            }
        }
        
        public void StartGame(CharacterBlueprint characterBlueprint)
        {
            CrossSceneData.CharacterBlueprint = characterBlueprint;
            //同步加载场景1
            SceneManager.LoadScene(1);
        }
    }
}
