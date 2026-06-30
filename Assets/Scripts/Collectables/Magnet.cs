namespace Vampire
{
    //磁铁
    public class Magnet : Collectable
    {   
        protected override void OnCollected()
        {
            entityManager.CollectAllCoinsAndGems();
            Destroy(gameObject);
        }
    }
}
