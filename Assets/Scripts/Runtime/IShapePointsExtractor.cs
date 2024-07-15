using System.Collections.Generic;

namespace Runtime
{
    public interface IShapePointsExtractor
    {
        IEnumerable<ZoneShapePoint> GetShapePoints(float spacingT);//todo get method that accepts tolerance object
    }
}