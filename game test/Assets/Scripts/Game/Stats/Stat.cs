using System.Collections.Generic;

namespace IndieGame.Gameplay.Stats
{
    public class Stat
    {
        private float _baseValue;
        private float _cachedValue;
        private bool _isDirty = true;
        private readonly List<float> _modifiers = new List<float>();

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                if (_baseValue == value) return;
                _baseValue = value;
                _isDirty = true;
            }
        }

        public float Value
        {
            get
            {
                if (_isDirty)
                {
                    Recalculate();
                }
                return _cachedValue;
            }
        }

        public void AddModifier(float value)
        {
            _modifiers.Add(value);
            _isDirty = true;
        }

        public void RemoveModifier(float value)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i] != value) continue;
                _modifiers.RemoveAt(i);
                _isDirty = true;
                return;
            }
        }

        private void Recalculate()
        {
            float sum = _baseValue;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                sum += _modifiers[i];
            }
            _cachedValue = sum;
            _isDirty = false;
        }
    }
}
