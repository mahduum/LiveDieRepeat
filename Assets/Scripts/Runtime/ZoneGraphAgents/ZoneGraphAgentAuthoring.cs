using Unity.Entities;
using UnityEngine;

namespace Runtime.ZoneGraphAgents
{
    public class ZoneGraphAgentAuthoring : MonoBehaviour
    {
        private class ZoneAgentAuthoringBaker : Baker<ZoneGraphAgentAuthoring>
        {
            public override void Bake(ZoneGraphAgentAuthoring authoring)
            {
            }
        }
    }
    
}