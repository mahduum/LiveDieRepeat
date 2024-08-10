using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Data
{
    public interface IZoneLaneProfile
    {
        ZoneLaneDesc[] GetLaneDescriptions();
        float GetLanesTotalWidth();
        Object GetSourceInstance();
    }
    
    [CreateAssetMenu(fileName = "LaneProfile", menuName = "ZoneGraph/LaneProfile", order = 0)]
    public class ZoneLaneProfile : ScriptableObject, IZoneLaneProfile
    {
        [SerializeField]
        public string _name;

        [SerializeField] public ZoneLaneDesc[] _lanes;

        public event Action OnLaneProfileUpdated;

        public ZoneLaneDesc[] GetLaneDescriptions() => _lanes;
        
        public float GetLanesTotalWidth()//todo should I save it as asset?
        {
            float totalWidth = 0.0f;
            foreach (var zoneLaneDesc in _lanes)
            {
                totalWidth += zoneLaneDesc._width;
            }

            return totalWidth;
        }

        public Object GetSourceInstance()
        {
            return this;
        }

        private void OnValidate()
        {
            OnLaneProfileUpdated?.Invoke();

        }
    }

    public enum ZoneLaneDirection
    {
        None, //no movement, spacer or median
        Forward,
        Backward
    }
    
    [System.Serializable]
    public struct ZoneLaneDesc : IComponentData
    {
        public float _width;
        public ZoneLaneDirection _direction;
        //todo bitmap tag mask
    }
}