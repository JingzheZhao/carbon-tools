using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Rhino.Geometry;

namespace Sitewise.Sync
{
    /// <summary>
    /// Computes area / length / volume / count for a single piece of geometry and
    /// a stable id derived from the geometry itself, per the sync protocol
    /// (docs/decision-record.md section 15).
    ///
    /// Every mass-property call passes (true, false, false, false): area or volume
    /// only, no moments. The default overload integrates the centroid and the second
    /// and product moments as well, none of which reach the payload, and on a
    /// 1,230-object model that unused work was 78% of the sync.
    /// </summary>
    public static class GeometryQuantities
    {
        public struct Quantity
        {
            public double Area;
            public double Length;
            public double Volume;
            public int Count;

            /// <summary>Model units to metres. Area goes as the square, volume as the cube.</summary>
            public Quantity Scaled(double unitScale)
            {
                return new Quantity
                {
                    Area = Area * unitScale * unitScale,
                    Length = Length * unitScale,
                    Volume = Volume * unitScale * unitScale * unitScale,
                    Count = Count
                };
            }
        }

        /// <summary>In model units. Call Quantity.Scaled to convert.</summary>
        public static Quantity Compute(GeometryBase geometry)
        {
            var quantity = new Quantity();

            switch (geometry)
            {
                case Point point:
                    quantity.Count = 1;
                    break;

                case Curve curve:
                    quantity.Length = curve.GetLength();
                    break;

                case Brep brep:
                    var brepArea = AreaMassProperties.Compute(brep, true, false, false, false);
                    if (brepArea != null)
                    {
                        quantity.Area = brepArea.Area;
                    }

                    if (brep.IsSolid)
                    {
                        var brepVolume = VolumeMassProperties.Compute(brep, true, false, false, false);
                        if (brepVolume != null)
                        {
                            quantity.Volume = brepVolume.Volume;
                        }
                    }

                    break;

                case Mesh mesh:
                    var meshArea = AreaMassProperties.Compute(mesh, true, false, false, false);
                    if (meshArea != null)
                    {
                        quantity.Area = meshArea.Area;
                    }

                    if (mesh.IsClosed)
                    {
                        var meshVolume = VolumeMassProperties.Compute(mesh, true, false, false, false);
                        if (meshVolume != null)
                        {
                            quantity.Volume = meshVolume.Volume;
                        }
                    }

                    break;

                case Extrusion extrusion:
                    var extrusionBrep = extrusion.ToBrep();
                    if (extrusionBrep != null)
                    {
                        return Compute(extrusionBrep);
                    }

                    break;

                case Surface surface:
                    var surfaceArea = AreaMassProperties.Compute(surface, true, false, false, false);
                    if (surfaceArea != null)
                    {
                        quantity.Area = surfaceArea.Area;
                    }

                    break;
            }

            return quantity;
        }

        /// <summary>
        /// A hash of what the object is, used to recognise the same object arriving
        /// twice. Type and bounding box alone are not enough - a cylinder and a box
        /// can share both - so the measurements we have already taken go in as well,
        /// along with whatever topology counts are free to read. Two objects have to
        /// agree on all of it to collide, which in practice means they are the same
        /// object or an exact duplicate sitting in the same place. Either way,
        /// counting it twice would be wrong.
        /// </summary>
        public static string StableId(GeometryBase geometry, Quantity raw)
        {
            var bbox = geometry.GetBoundingBox(true);
            var descriptor = new StringBuilder();
            descriptor.Append(geometry.GetType().Name);
            descriptor.Append('|');
            descriptor.Append(bbox.Min.ToString());
            descriptor.Append('|');
            descriptor.Append(bbox.Max.ToString());

            // already computed, so free, and far more discriminating than a box
            descriptor.Append('|').Append(raw.Area.ToString("R", CultureInfo.InvariantCulture));
            descriptor.Append('|').Append(raw.Volume.ToString("R", CultureInfo.InvariantCulture));
            descriptor.Append('|').Append(raw.Length.ToString("R", CultureInfo.InvariantCulture));

            if (geometry is Mesh mesh)
            {
                descriptor.Append('|').Append(mesh.Vertices.Count).Append('|').Append(mesh.Faces.Count);
            }
            else if (geometry is Brep brep)
            {
                // O(1) property reads: no topology is built to answer them
                descriptor.Append('|').Append(brep.Faces.Count)
                          .Append('|').Append(brep.Edges.Count)
                          .Append('|').Append(brep.Vertices.Count)
                          .Append('|').Append(brep.IsSolid);
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
