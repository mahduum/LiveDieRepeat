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
        //it is created in editor but the tags are taken from settings,
        //there is option edit tags, all, none, and available tags to choose from, use unity layer mask for that - todo how to do it?
        //the tag gets its bit from a number, that is why all the tags are defined in tags settings file because it is index of that tag info
        //tags are initialized to max int number
        
        /*
         * visual tag representation:
         * 
USTRUCT()
struct ZONEGRAPH_API FZoneGraphTagInfo
{
	GENERATED_BODY()

	bool IsValid() const { return !Name.IsNone(); }

	UPROPERTY(Category = Zone, EditAnywhere)
	FName Name;

	UPROPERTY(Category = Zone, EditAnywhere)
	FColor Color = FColor(ForceInit);

	UPROPERTY(Category = Zone, EditAnywhere)
	FZoneGraphTag Tag;
};
         */
    }
}