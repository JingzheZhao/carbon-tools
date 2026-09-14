using System;
using System.Security.Cryptography;
using System.Text;

using Rhino.Geometry;

namespace Sitewise.Sync
{
    /// <summary>
    /// Computes area / length / volume / count for a single piece of geometry and
    /// a stable id derived from the geometry itself, per the sync protocol
    /// (docs/decision-record.md section 15).
    /// </summary>
    public static class GeometryQuantities
    {
        public struct Quantity
        {
            public double Area;
            public double Length;
            public double Volume;
            public int Count;
        }

        public static Quantity Compute(GeometryBase geometry, double unitScale)
        {
            var quantity = new Quantity();

            switch (geometry)
            {
                case Point point:
                    quantity.Count = 1;
                    break;

                case Curve curve:
                    quantity.Length = curve.GetLength() * unitScale;
                    break;

                case Brep brep:
                    var brepArea = AreaMassProperties.Compute(brep);
                    if (brepArea != null)
                    {
                        quantity.Area = brepArea.Area * unitScale * unitScale;
                    }

                    if (brep.IsSolid)
                    {
                        var brepVolume = VolumeMassProperties.Compute(brep);
                        if (brepVolume != null)
                        {
                            quantity.Volume = brepVolume.Volume * unitScale * unitScale * unitScale;
                        }
                    }

                    break;

                case Mesh mesh:
                    var meshArea = AreaMassProperties.Compute(mesh);
                    if (meshArea != null)
                    {
                        quantity.Area = meshArea.Area * unitScale * unitScale;
                    }

                    if (mesh.IsClosed)
                    {
                        var meshVolume = VolumeMassProperties.Compute(mesh);
                        if (meshVolume != null)
                        {
                            quantity.Volume = meshVolume.Volume * unitScale * unitScale * unitScale;
                        }
                    }

                    break;

                case Extrusion extrusion:
                    var extrusionBrep = extrusion.ToBrep();
                    if (extrusionBrep != null)
                    {
                        return Compute(extrusionBrep, unitScale);
                    }

                    break;

                case Surface surface:
                    var surfaceArea = AreaMassProperties.Compute(surface);
                    if (surfaceArea != null)
                    {
                        quantity.Area = surfaceArea.Area * unitScale * unitScale;
                    }

                    break;
            }

            return quantity;
        }

        public static string StableId(GeometryBase geometry)
        {
            var bbox = geometry.GetBoundingBox(true);
            var descriptor = new StringBuilder();
            descriptor.Append(geometry.GetType().Name);
            descriptor.Append('|');
            descriptor.Append(bbox.Min.ToString());
            descriptor.Append('|');
            descriptor.Append(bbox.Max.ToString());

            if (geometry is Mesh mesh)
            {
                descriptor.Append('|').Append(mesh.Vertices.Count).Append('|').Append(mesh.Faces.Count);
            }
            else if (geometry is Curve curve)
            {
                descriptor.Append('|').Append(curve.GetLength().ToString("F6"));
            }

            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(descriptor.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 16);
            }
        }
    }
}
