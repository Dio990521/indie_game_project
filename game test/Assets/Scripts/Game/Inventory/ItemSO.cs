using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Item")]
    public class ItemSO : ScriptableObject
    {
        public string ItemName;
        [TextArea] public string Description;

        public virtual void Use()
        {
            Debug.Log($"使用了道具: {ItemName}");
        }
    }
}
