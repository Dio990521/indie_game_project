using UnityEngine;

namespace IndieGame.Core
{
    [CreateAssetMenu(menuName = "IndieGame/Scene/Location ID")]
    public class LocationID : ScriptableObject
    {
        [SerializeField] private string displayName;
        public string DisplayName => displayName;
    }
}
