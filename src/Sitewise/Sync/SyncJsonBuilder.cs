using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Geometry;

using Sitewise.Components;

namespace Sitewise.Sync
{
    /// <summary>
    /// Builds the JSON payload described in the sync protocol
    /// (docs/decision-record.md section 15):
    /// meta { units, origin_offset, timestamp, mesh_tolerance } + groups[ { color, objects[] } ].
    /// Units are converted to meters and geometry is shifted to origin here,
    /// so the web/three.js side never has to deal with model units or huge coordinates.
    /// </summary>
    public static class SyncJsonBuilder
    {
        public static JObject Build(IReadOnlyList<TaggedGeometry> items, BuildStats stats)
        {
            stats.Objects = items.Count;

            var doc = RhinoDoc.ActiveDoc;
            var sourceUnits = doc?.ModelUnitSystem ?? UnitSystem.None;
            double unitScale = ResolveUnitScale(sourceUnits);
            double tolerance = (doc?.ModelAbsoluteTolerance ?? 0.01) * unitScale;

            // Pass one: unwrap, tessellate, measure, hash. Each object is independent
            // of the others, so the whole pass runs across all cores. One
            // MeshingParameters is shared: the build only reads it.
            var meshing = MeshingParameters.Default;
            var prepared = new Prepared[items.Count];
            Parallel.For(0, items.Count, i =>
            {
                prepared[i] = Prepare(items[i].Geometry, meshing, stats);
            });

            long originStart = BuildStats.Now;
            var origin = ComputeOrigin(prepared, unitScale);
            stats.AddOrigin(BuildStats.Now - originStart);

            // Pass two: the coordinates, which could not be written earlier - the
            // origin offset comes from the bounding box of the whole model, so it is
            // not known until pass one has unwrapped everything.
            var built = new JObject[items.Count];
            Parallel.For(0, items.Count, i =>
            {
                if (prepared[i] != null && prepared[i].Geometry != null)
                {
                    built[i] = BuildObject(prepared[i], unitScale, origin, stats);
                }
            });

            // Grouping happens afterwards so the payload keeps the input order,
            // which Parallel.For does not promise.
            var groups = new JArray();
            foreach (var group in Enumerable.Range(0, items.Count)
                         .Where(i => built[i] != null)
                         .GroupBy(i => ColorToHex(items[i].Color)))
            {
                var objects = new JArray();
                foreach (int i in group)
                {
                    objects.Add(built[i]);
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
                    ["mesh_tolerance"] = tolerance,
                    // flat vertex and face arrays; 1 was a node per vertex
                    ["mesh_format"] = 2
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

        /// <summary>One object, unwrapped and measured. Lives for one build.</summary>
        private sealed class Prepared
        {
            public GeometryBase Geometry;
            public Mesh Mesh;
            public GeometryQuantities.Quantity Raw;
            public string Id;
        }

        /// <summary>
        /// Pass one, on a worker thread: everything that depends on the object alone.
        /// </summary>
        private static Prepared Prepare(IGH_GeometricGoo goo,
            MeshingParameters meshing, BuildStats stats)
        {
            var made = new Prepared();
            made.Geometry = goo?.ScriptVariable() as GeometryBase;
            if (made.Geometry == null)
            {
                return made;
            }

            long quantityStart = BuildStats.Now;
            made.Raw = GeometryQuantities.Compute(made.Geometry);
            stats.AddQuantity(BuildStats.Now - quantityStart);

            long tessellateStart = BuildStats.Now;
            made.Mesh = MeshExport.Tessellate(made.Geometry, meshing);
            stats.AddTessellate(BuildStats.Now - tessellateStart);

            long idStart = BuildStats.Now;
            made.Id = GeometryQuantities.StableId(made.Geometry, made.Raw);
            stats.AddId(BuildStats.Now - idStart);

            return made;
        }

        /// <summary>
        /// Pass two, on a worker thread: place the prepared object in the payload.
        /// </summary>
        private static JObject BuildObject(Prepared entry,
            double unitScale, Point3d origin, BuildStats stats)
        {
            var quantity = entry.Raw.Scaled(unitScale);

            long meshJsonStart = BuildStats.Now;
            var mesh = MeshExport.ToMeshData(entry.Mesh, unitScale, origin);
            stats.AddMeshJson(BuildStats.Now - meshJsonStart);

            if (entry.Mesh != null)
            {
                stats.CountMesh(entry.Mesh.Vertices.Count, entry.Mesh.Faces.Count);
            }

            long assembleStart = BuildStats.Now;
            var jObject = new JObject
            {
                ["id"] = entry.Id,
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

            stats.AddAssemble(BuildStats.Now - assembleStart);
            return jObject;
        }

        private static Point3d ComputeOrigin(IReadOnlyList<Prepared> prepared, double unitScale)
        {
            if (prepared.Count == 0)
            {
                return Point3d.Origin;
            }

            var box = BoundingBox.Empty;
            foreach (var entry in prepared)
            {
                if (entry != null && entry.Geometry != null)
                {
                    box.Union(entry.Geometry.GetBoundingBox(true));
                }
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
