using System.Drawing;

using Grasshopper.Kernel.Types;

namespace Sitewise.Components
{
    /// <summary>
    /// Plain payload carried by "Tagged Objects": one piece of geometry, the group
    /// color it was assigned in CC Group, and the name the designer gave that group.
    ///
    /// The name is a label, not an identity: the colour is still the group key. It
    /// exists because a hex code tells a reader nothing about what they are looking
    /// at, and "#343434" is a poor way to ask someone which material to price. Wrapped in a GH_ObjectWrapper so it
    /// can flow through a generic Grasshopper parameter without a custom Goo type.
    /// </summary>
    public class TaggedGeometry
    {
        public IGH_GeometricGoo Geometry { get; }

        public Color Color { get; }

        /// <summary>What the designer called this group. May be empty.</summary>
        public string Name { get; }

        public TaggedGeometry(IGH_GeometricGoo geometry, Color color, string name)
        {
            Geometry = geometry;
            Color = color;
            Name = name;
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
