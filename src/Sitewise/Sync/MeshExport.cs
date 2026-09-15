using System;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Rhino.Geometry;

namespace Sitewise.Sync
{
    /// <summary>
    /// Converts geometry to the { vertices, faces } mesh block of the sync protocol.
    /// Vertices are scaled to meters and shifted by the shared origin offset so the
    /// three.js side never receives large geo-referenced coordinates.
    ///
    /// Tessellating and writing JSON are two calls, not one, because only the first
    /// can be cached: the origin offset depends on the bounding box of the whole
    /// model, so moving any object rewrites every other object's coordinates while
    /// leaving their meshes untouched.
    ///
    /// The JSON is written as text rather than assembled as JObject nodes. A node
    /// per vertex and per face meant roughly 1.7 million short-lived objects per
    /// build on a 1,200-object model, and once tessellation was cached that garbage
    /// was 96% of the remaining cost - every thread doing nothing but allocating.
    /// Handing Newtonsoft one JRaw per mesh replaces the whole graph with a string
    /// it copies straight through.
    /// </summary>
    public static class MeshExport
    {
        /// <summary>Coordinates are written to 0.1 mm, which is finer than a concept model means.</summary>
        private const double CoordScale = 10000.0;

        /// <summary>The mesh in model units, with nothing applied to it yet.</summary>
        public static Mesh Tessellate(GeometryBase geometry, MeshingParameters parameters)
        {
            switch (geometry)
            {
                case Mesh m:
                    return m;

                case Brep brep:
                    return JoinMeshes(Mesh.CreateFromBrep(brep, parameters));

                case Extrusion extrusion:
                    return JoinMeshes(Mesh.CreateFromBrep(extrusion.ToBrep(), parameters));

                case Surface surface:
                    return JoinMeshes(Mesh.CreateFromBrep(surface.ToBrep(), parameters));
            }

            return null;
        }

        /// <summary>
        /// { "vertices": [x,y,z, ...], "faces": [a,b,c,d, ...] }
        ///
        /// Both arrays are flat. Faces always carry four indices: Rhino's own
        /// MeshFace does the same and marks a triangle by repeating the third, so
        /// the viewer reads d == c as "triangle" and no per-face bracket is needed.
        /// </summary>
        public static JRaw ToMeshData(Mesh mesh, double unitScale, Point3d origin)
        {
            if (mesh == null || mesh.Vertices.Count == 0)
            {
                return null;
            }

            // ~9 characters a number, give or take; one allocation instead of a
            // doubling chain through several hundred kilobytes
            var text = new StringBuilder(mesh.Vertices.Count * 30 + mesh.Faces.Count * 24 + 32);

            text.Append("{\"vertices\":[");
            bool first = true;
            foreach (var vertex in mesh.Vertices)
            {
                if (!first)
                {
                    text.Append(',');
                }

                first = false;
                AppendCoord(text, vertex.X * unitScale - origin.X);
                text.Append(',');
                AppendCoord(text, vertex.Y * unitScale - origin.Y);
                text.Append(',');
                AppendCoord(text, vertex.Z * unitScale - origin.Z);
            }

            text.Append("],\"faces\":[");
            first = true;
            foreach (var face in mesh.Faces)
            {
                if (!first)
                {
                    text.Append(',');
                }

                first = false;
                text.Append(face.A).Append(',').Append(face.B).Append(',')
                    .Append(face.C).Append(',').Append(face.D);
            }

            text.Append("]}");
            return new JRaw(text.ToString());
        }

        /// <summary>
        /// A fixed-point decimal, written digit by digit. double.ToString would
        /// emit seventeen significant digits by default and allocate a string for
        /// every coordinate on the way.
        /// </summary>
        private static void AppendCoord(StringBuilder text, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                text.Append('0');
                return;
            }

            long scaled = (long)Math.Round(value * CoordScale, MidpointRounding.AwayFromZero);
            if (scaled < 0)
            {
                text.Append('-');
                scaled = -scaled;
            }

            text.Append(scaled / 10000);

            long frac = scaled % 10000;
            if (frac == 0)
            {
                return;
            }

            text.Append('.');

            // leading zeros come from the unshortened value, trailing zeros go after
            if (frac < 1000)
            {
                text.Append('0');
            }

            if (frac < 100)
            {
                text.Append('0');
            }

            if (frac < 10)
            {
                text.Append('0');
            }

            while (frac % 10 == 0)
            {
                frac /= 10;
            }

            text.Append(frac);
        }

        private static Mesh JoinMeshes(Mesh[] meshes)
        {
            if (meshes == null || meshes.Length == 0)
            {
                return null;
            }

            var joined = new Mesh();
            joined.Append(meshes.Where(m => m != null));
            return joined;
        }
    }
}
