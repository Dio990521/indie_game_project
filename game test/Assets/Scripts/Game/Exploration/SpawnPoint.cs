using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Exploration
{
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private LocationID locationId;

        private static readonly Dictionary<LocationID, SpawnPoint> Registry = new Dictionary<LocationID, SpawnPoint>();

        private void OnEnable()
        {
            if (locationId == null)
            {
                Debug.LogWarning("[SpawnPoint] Missing LocationID.");
                return;
            }
            Registry[locationId] = this;
        }

        private void OnDisable()
        {
            if (locationId == null) return;
            if (Registry.TryGetValue(locationId, out SpawnPoint existing) && existing == this)
            {
                Registry.Remove(locationId);
            }
        }

        public static bool TryGet(LocationID id, out SpawnPoint spawnPoint)
        {
            if (id == null)
            {
                spawnPoint = null;
                return false;
            }
            return Registry.TryGetValue(id, out spawnPoint);
        }
    }
}
