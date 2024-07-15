using UnityEngine;
using UnityEngine.Serialization;

namespace Data
{
    [CreateAssetMenu(fileName = "LaneProfile", menuName = "ZoneGraph/LaneProfile", order = 0)]
    public class ZoneLaneProfile : ScriptableObject
    {
        [SerializeField]
        public string _name;

        [SerializeField] public ZoneLaneDesc[] _lanes;

        public float GetLanesTotalWidth()
        {
            float totalWidth = 0.0f;
            foreach (var zoneLaneDesc in _lanes)
            {
                totalWidth += zoneLaneDesc._width;
            }

            return totalWidth;
        }
    }

    public enum ZoneLaneDirection
    {
        None, //no movement, spacer or median
        Forward,
        Backward
    }
    
    [System.Serializable]
    public struct ZoneLaneDesc
    {
        public float _width;

        public ZoneLaneDirection _direction;
        //todo bitmap tag mask
    }
}