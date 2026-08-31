using System.Linq;

using Newtonsoft.Json.Linq;

using Rhino.Geometry;

namespace CarbonTools.Sync
{
    /// <summary>
    /// Converts geometry to the { vertices, faces } mesh block of the sync protocol.
    /// Vertices are scaled to meters and shifted by the shared origin offset so the
    /// three.js side never receives large geo-referenced coordinates.
    /// </summary>
    public static class MeshExport
    {
        public static JObject ToMeshData(GeometryBase geometry, double unitScale, Point3d origin)
        {
            Mesh mesh = null;

            switch (geometry)
            {
                case Mesh m:
                    mesh = m;
                    break;

                case Brep brep:
                    mesh = JoinMeshes(Mesh.CreateFromBrep(brep, MeshingParameters.Default));
                    break;

                case Extrusion extrusion:
                    mesh = JoinMeshes(Mesh.CreateFromBrep(extrusion.ToBrep(), MeshingParameters.Default));
                    break;

                case Surface surface:
                    mesh = JoinMeshes(Mesh.CreateFromBrep(surface.ToBrep(), MeshingParameters.Default));
                    break;
            }

            if (mesh == null || mesh.Vertices.Count == 0)
            {
                return null;
            }

            var vertices = new JArray();
            foreach (var vertex in mesh.Vertices)
            {
                vertices.Add(new JArray(
                    vertex.X * unitScale - origin.X,
                    vertex.Y * unitScale - origin.Y,
                    vertex.Z * unitScale - origin.Z));
            }

            var faces = new JArray();
            foreach (var face in mesh.Faces)
            {
                faces.Add(face.IsQuad
                    ? new JArray(face.A, face.B, face.C, face.D)
                    : new JArray(face.A, face.B, face.C));
            }

            return new JObject
            {
                ["vertices"] = vertices,
                ["faces"] = faces
            };
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
