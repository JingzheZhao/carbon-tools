using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Newtonsoft.Json.Linq;

using Rhino;
using Rhino.Geometry;

using CarbonTools.Components;

namespace CarbonTools.Sync
{
    /// <summary>
    /// Builds the JSON payload described in the sync protocol
    /// (E:\Carbon\Carbon工具-方法论决策记录.md section 15):
    /// meta { units, origin_offset, timestamp, mesh_tolerance } + groups[ { color, objects[] } ].
    /// Units are converted to meters and geometry is shifted to origin here,
    /// so the web/three.js side never has to deal with model units or huge coordinates.
    /// </summary>
    public static class SyncJsonBuilder
    {
        public static JObject Build(IReadOnlyList<TaggedGeometry> items)
        {
            var doc = RhinoDoc.ActiveDoc;
            var sourceUnits = doc?.ModelUnitSystem ?? UnitSystem.None;
            double unitScale = ResolveUnitScale(sourceUnits);
            double tolerance = (doc?.ModelAbsoluteTolerance ?? 0.01) * unitScale;

            var rawGeometries = items
                .Select(i => i.Geometry?.ScriptVariable() as GeometryBase)
                .Where(g => g != null)
                .ToList();

            var origin = ComputeOrigin(rawGeometries, unitScale);

            var groups = new JArray();
            foreach (var group in items.GroupBy(i => ColorToHex(i.Color)))
            {
                var objects = new JArray();
                foreach (var item in group)
                {
                    var geometry = item.Geometry?.ScriptVariable() as GeometryBase;
                    if (geometry == null)
                    {
                        continue;
                    }

                    objects.Add(BuildObject(geometry, unitScale, origin));
                }

                groups.Add(new JObject
                {
                    ["color"] = group.Key,
                    ["objects"] = objects
                });
            }

            return new JObject
            {
                ["meta"] = new JObject
                {
                    ["units"] = "m",
                    ["source_units"] = sourceUnits.ToString(),
                    ["unit_scale"] = unitScale,
                    ["units_trusted"] = sourceUnits != UnitSystem.None,
                    ["origin_offset"] = new JArray(origin.X, origin.Y, origin.Z),
                    ["timestamp"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["mesh_tolerance"] = tolerance
                },
                ["groups"] = groups
            };
        }

        /// <summary>
        /// Everything this builder emits is in meters, whatever the Rhino document is
        /// set to — millimeters, feet, inches, all of it is scaled here so the web side
        /// never sees model units. The one case we cannot convert is a document with no
        /// unit system at all; we pass the numbers through unscaled and report it via
        /// meta.units_trusted = false, so the viewer can warn rather than quietly
        /// present feet as if they were meters.
        /// </summary>
        private static double ResolveUnitScale(UnitSystem sourceUnits)
        {
            if (sourceUnits == UnitSystem.None)
            {
                return 1.0;
            }

            double scale = RhinoMath.UnitScale(sourceUnits, UnitSystem.Meters);

            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0.0)
            {
                return 1.0;
            }

            return scale;
        }

        private static JObject BuildObject(GeometryBase geometry, double unitScale, Point3d origin)
        {
            var quantity = GeometryQuantities.Compute(geometry, unitScale);
            var mesh = MeshExport.ToMeshData(geometry, unitScale, origin);

            var jObject = new JObject
            {
                ["id"] = GeometryQuantities.StableId(geometry),
                ["quantity"] = new JObject
                {
                    ["area"] = quantity.Area,
                    ["count"] = quantity.Count,
                    ["length"] = quantity.Length,
                    ["volume"] = quantity.Volume
                }
            };

            if (mesh != null)
            {
                jObject["mesh"] = mesh;
            }

            return jObject;
        }

        private static Point3d ComputeOrigin(IReadOnlyList<GeometryBase> geometries, double unitScale)
        {
            if (geometries.Count == 0)
            {
                return Point3d.Origin;
            }

            var box = BoundingBox.Empty;
            foreach (var geometry in geometries)
            {
                box.Union(geometry.GetBoundingBox(true));
            }

            if (!box.IsValid)
            {
                return Point3d.Origin;
            }

            return new Point3d(box.Min.X * unitScale, box.Min.Y * unitScale, box.Min.Z * unitScale);
        }

        private static string ColorToHex(System.Drawing.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
