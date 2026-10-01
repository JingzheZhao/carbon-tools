using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Sitewise.Components
{
    /// <summary>
    /// Turns a layer path typed by the user - "Option 2/Wood" - into the geometry
    /// sitting under it.
    ///
    /// A path is carried as its segments and resolved by walking the layer tree one
    /// level at a time, matching each segment against the children of the layer
    /// above it. The obvious alternative is to join the segments with Rhino's own
    /// separator and call FindByFullPath, and that was the first attempt: it rests
    /// on knowing what the separator is, which is not something this code can check
    /// for itself, and a wrong guess fails by finding nothing at all. Walking the
    /// tree asks Rhino for the answer at every step instead of assuming one.
    ///
    /// Matching is case-insensitive. It is not space-insensitive: "Option2" will
    /// not find "Option 2", because quietly accepting a name the designer did not
    /// write is how the wrong layer gets costed. The failure names what it could
    /// not find and lists what was available at that level.
    ///
    /// A path is read as a subtree: "Option 2" takes everything on that layer and
    /// on every layer nested under it. Writing the leaf - "Option 2/Wood" - narrows
    /// it. The consequence is that two paths can overlap, and overlapping paths send
    /// the same object twice, which CCGroupComponent refuses.
    /// </summary>
    internal static class LayerSource
    {
        /// <summary>What the user types between levels.</summary>
        public const char TypedSeparator = '/';

        /// <summary>
        /// The segments of a typed path, or null when the text held none. No lookup
        /// happens here: an unwritable path and an unknown layer are different
        /// problems and deserve different messages.
        /// </summary>
        public static string[] Normalise(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed))
            {
                return null;
            }

            var parts = typed.Split(TypedSeparator)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            return parts.Length == 0 ? null : parts;
        }

        /// <summary>The path written the way the user writes it, for messages.</summary>
        public static string Display(IReadOnlyList<string> segments)
        {
            return segments == null ? string.Empty : string.Join(TypedSeparator.ToString(), segments);
        }

        /// <summary>
        /// True when one path contains the other, equality included. Compared
        /// segment by segment, so "Option 2/Bench" is not a parent of
        /// "Option 2/Benches".
        /// </summary>
        public static bool Overlaps(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            int shared = Math.Min(a.Count, b.Count);
            for (int i = 0; i < shared; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;   // one is a prefix of the other, or they are the same
        }

        /// <summary>
        /// Collects the geometry under one path. Returns false with a message the
        /// component can show verbatim. An empty layer is not a failure: a designer
        /// who has not drawn there yet should see an empty group, not an error.
        /// </summary>
        public static bool TryCollect(RhinoDoc doc, IReadOnlyList<string> segments,
            List<IGH_GeometricGoo> into, out string error)
        {
            error = null;

            if (doc == null)
            {
                error = "No Rhino document is open.";
                return false;
            }

            var root = Resolve(doc, segments, out int failedAt);
            if (root == null)
            {
                error = NotFound(doc, segments, failedAt);
                return false;
            }

            foreach (var layer in Subtree(doc, root))
            {
                foreach (var obj in doc.Objects.FindByLayer(layer))
                {
                    if (obj == null || obj.IsDeleted)
                    {
                        continue;
                    }

                    var goo = ToGoo(obj);
                    if (goo != null)
                    {
                        into.Add(goo);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Walks the tree a segment at a time. On failure, failedAt is the index of
        /// the segment that had no match, so the message can say where it went wrong
        /// rather than only that it did.
        /// </summary>
        private static Layer Resolve(RhinoDoc doc, IReadOnlyList<string> segments, out int failedAt)
        {
            Guid parent = Guid.Empty;
            Layer current = null;

            for (int i = 0; i < segments.Count; i++)
            {
                current = Children(doc, parent)
                    .FirstOrDefault(l => string.Equals(l.Name, segments[i], StringComparison.OrdinalIgnoreCase));

                if (current == null)
                {
                    failedAt = i;
                    return null;
                }

                parent = current.Id;
            }

            failedAt = -1;
            return current;
        }

        /// <summary>Names what was missing and what stood in its place.</summary>
        private static string NotFound(RhinoDoc doc, IReadOnlyList<string> segments, int failedAt)
        {
            string asked = Display(segments);
            string missing = segments[failedAt];

            // the layer whose children we were searching when it failed
            Guid parent = Guid.Empty;
            for (int i = 0; i < failedAt; i++)
            {
                parent = Children(doc, parent)
                    .First(l => string.Equals(l.Name, segments[i], StringComparison.OrdinalIgnoreCase)).Id;
            }

            string where = failedAt == 0
                ? "at the top level"
                : $"under “{Display(segments.Take(failedAt).ToList())}”";

            var available = Children(doc, parent).Select(l => l.Name).Take(8).ToList();

            if (available.Count == 0)
            {
                return $"“{asked}”: there are no layers {where}.";
            }

            return $"“{asked}”: there is no “{missing}” {where}. "
                 + "There is " + string.Join(", ", available.Select(n => "“" + n + "”")) + ".";
        }

        private static IEnumerable<Layer> Children(RhinoDoc doc, Guid parentId)
        {
            foreach (var layer in doc.Layers)
            {
                if (layer != null && !layer.IsDeleted && layer.ParentLayerId == parentId)
                {
                    yield return layer;
                }
            }
        }

        /// <summary>The layer plus every layer nested under it, at any depth.</summary>
        private static IEnumerable<Layer> Subtree(RhinoDoc doc, Layer root)
        {
            var pending = new Queue<Layer>();
            pending.Enqueue(root);

            while (pending.Count > 0)
            {
                var layer = pending.Dequeue();
                yield return layer;

                foreach (var child in Children(doc, layer.Id))
                {
                    pending.Enqueue(child);
                }
            }
        }

        /// <summary>
        /// Wraps document geometry so it can travel the same path as geometry wired
        /// in by hand. The Rhino object id goes along: nothing reads it yet, but it
        /// is the only stable name these objects have and throwing it away here
        /// would be hard to undo later.
        /// </summary>
        private static IGH_GeometricGoo ToGoo(RhinoObject obj)
        {
            IGH_GeometricGoo goo;

            switch (obj.Geometry)
            {
                case Brep brep:
                    goo = new GH_Brep(brep);
                    break;
                case Extrusion extrusion:
                    goo = new GH_Brep(extrusion.ToBrep());
                    break;
                case Mesh mesh:
                    goo = new GH_Mesh(mesh);
                    break;
                case Curve curve:
                    goo = new GH_Curve(curve);
                    break;
                case Surface surface:
                    goo = new GH_Surface(surface);
                    break;
                case Rhino.Geometry.Point point:
                    goo = new GH_Point(point.Location);
                    break;
                default:
                    return null;
            }

            goo.ReferenceID = obj.Id;
            return goo;
        }
    }
}
