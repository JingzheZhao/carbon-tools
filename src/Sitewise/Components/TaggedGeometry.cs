using System.Drawing;

using Grasshopper.Kernel.Types;

namespace Sitewise.Components
{
    /// <summary>
    /// Plain payload carried by "Tagged Objects": one piece of geometry plus the
    /// group color it was assigned in CC Group. Wrapped in a GH_ObjectWrapper so it
    /// can flow through a generic Grasshopper parameter without a custom Goo type.
    /// </summary>
    public class TaggedGeometry
    {
        public IGH_GeometricGoo Geometry { get; }

        public Color Color { get; }

        public TaggedGeometry(IGH_GeometricGoo geometry, Color color)
        {
            Geometry = geometry;
            Color = color;
        }

        public static bool TryUnwrap(object goo, out TaggedGeometry tagged)
        {
            if (goo is GH_ObjectWrapper wrapper && wrapper.Value is TaggedGeometry value)
            {
                tagged = value;
                return true;
            }

            tagged = null;
            return false;
        }
    }
}
