using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;


namespace IndieGame.Gameplay.Inventory
{
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Item")]
    public class ItemSO : ScriptableObject
    {
        [FormerlySerializedAs("ItemName")]
        public LocalizedString ItemName;
        [TextArea]
        [FormerlySerializedAs("Description")]
        public LocalizedString Description;

        public virtual void Use()
        {
            string name = ItemName != null ? ItemName.GetLocalizedString() : "Item";
            Debug.Log($"使用了道具: {name}");
        }
    }
}
