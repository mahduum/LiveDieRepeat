using Unity.Collections;
using Unity.Entities;

namespace Runtime.CoreSystems
{
    public abstract partial class OneSignalSystem<TSignal> : SignalSystemBase where TSignal : unmanaged, IBufferElementData
    {
        //todo make static array in base for all TSignal arguments to iterate more easily when there are more signals classes
        protected override NativeHashMap<TypeIndex, EntityQuery> GetEntitySignalQueries()
        {
            //todo: make query on system run? and update only?
            EntityQueryDesc queryDesc = new EntityQueryDesc()
            {
                All = new ComponentType[] {typeof(EntitySignalRange), typeof(TSignal)},
                Options = EntityQueryOptions.IncludeSystems
            };
            
            EntityQuery query = GetEntityQuery(queryDesc);

            return new NativeHashMap<TypeIndex, EntityQuery>() {{TypeManager.GetTypeIndex<TSignal>(), query}};
        }
    }
}