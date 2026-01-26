using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Core
{
    [CreateAssetMenu(menuName = "IndieGame/Scene/Location ID")]
    public class LocationID : ScriptableObject
    {
        [SerializeField]
        [FormerlySerializedAs("displayName")]
        private LocalizedString displayName;
        public LocalizedString DisplayName => displayName;
    }
}
