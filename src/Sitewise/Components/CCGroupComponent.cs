using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;

namespace Sitewise.Components
{
    /// <summary>
    /// CC Group: user picks a color via the swatch on the component body and
    /// feeds geometry in. The color is the group key.
    /// See docs/decision-record.md sections 13-14.
    /// </summary>
    public class CCGroupComponent : GH_Component
    {
        private static readonly Bitmap _icon = CreateIcon();

        public Color CurrentColor { get; private set; } = Color.Red;

        public CCGroupComponent()
            : base(
                "CC Group",
                "CCGroup",
                "Group geometry under one color. The color is the group key.",
                "Carbon",
                "Tools")
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new CCGroupComponentAttributes(this);
        }

        public void SetSwatchColor(Color color)
        {
            CurrentColor = color;
            ExpireSolution(true);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "Geometry",
                "Geometry belonging to this group. Use this for geometry built in Grasshopper, which has no layer to name.",
                GH_ParamAccess.list);
            pManager.AddTextParameter("Layer", "Layer",
                "Layer paths written with slashes, e.g. Option 2/Wood. A path takes that layer and everything nested under it.",
                GH_ParamAccess.list);

            pManager.AddTextParameter("Material Name", "Material Name",
                "What this group is, in your own words - \"Concrete paving\", \"Timber deck\". "
                + "Shown in the viewer instead of the colour's hex code.",
                GH_ParamAccess.item);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Tagged Objects", "T", "Geometry tagged with the group color", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            /* Which input is in play is a question about the parameters, not about
               what parsing produced: text wired into Geometry converts to nothing,
               and counting results cannot tell that apart from an empty canvas. */
            bool onGeometry = Params.Input[0].VolatileDataCount > 0;
            bool onLayer = Params.Input.Count > 1 && Params.Input[1].VolatileDataCount > 0;

            // recorded before the early returns: a path that failed to resolve is
            // exactly the one that must look again when the layers change
            _usesLayers = onLayer;

            if (onGeometry && onLayer)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Connect geometry or a layer, not both. A group comes from one place so that "
                    + "it is clear where its objects came from.");
                return;
            }

            if (!onGeometry && !onLayer)
            {
                return;   // nothing wired yet: not an error, just nothing to say
            }

            // The colour is the group key, so two components sharing one would make
            // a group whose contents come from two places with nothing on the canvas
            // to show it.
            string clash = ColourClash();
            if (clash != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Another CC Group already uses {Hex(CurrentColor)} ({clash}). "
                    + "One colour is one group: give this one a colour of its own.");
                return;
            }

            var geometry = new List<IGH_GeometricGoo>();

            if (onGeometry)
            {
                DA.GetDataList(0, geometry);
                geometry.RemoveAll(g => g == null);

                if (geometry.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Nothing on Geometry could be read as geometry. If that is a layer path, "
                        + "connect it to Layer instead.");
                    return;
                }
            }
            else
            {
                var typedPaths = new List<string>();
                DA.GetDataList(1, typedPaths);

                var paths = typedPaths.Select(LayerSource.Normalise).Where(p => p != null).ToList();

                if (paths.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Layer is connected but no path arrived. Write it from the top with slashes, "
                        + "e.g. Option 2/Wood.");
                    return;
                }

                string overlap = PathOverlap(paths);
                if (overlap != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, overlap);
                    return;
                }

                var doc = RhinoDoc.ActiveDoc;
                foreach (var path in paths)
                {
                    if (!LayerSource.TryCollect(doc, path, geometry, out string error))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                        return;
                    }
                }

                if (geometry.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        paths.Count == 1
                            ? $"Nothing is on “{LayerSource.Display(paths[0])}” yet."
                            : "Nothing is on those layers yet.");
                }
            }

            string name = null;
            DA.GetData(2, ref name);
            name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

            var tagged = new List<GH_ObjectWrapper>(geometry.Count);
            foreach (var goo in geometry)
            {
                tagged.Add(new GH_ObjectWrapper(new TaggedGeometry(goo, CurrentColor, name)));
            }

            DA.SetDataList(0, tagged);
        }

        private static string Hex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// The nickname of another CC Group holding this colour, or null. Scanning
        /// the document is cheap next to a solve, and reporting it here puts the
        /// message on the component whose swatch is wrong.
        /// </summary>
        private string ColourClash()
        {
            var document = OnPingDocument();
            if (document == null)
            {
                return null;
            }

            foreach (var other in document.Objects.OfType<CCGroupComponent>())
            {
                if (!ReferenceEquals(other, this) && !other.Locked &&
                    other.CurrentColor.ToArgb() == CurrentColor.ToArgb())
                {
                    return string.IsNullOrWhiteSpace(other.NickName) ? "another component" : other.NickName;
                }
            }

            return null;
        }

        /// <summary>
        /// Overlapping paths send the same object twice: "op1/bench" contains
        /// "op1/bench/wood", because a path is read as a subtree. Checked within
        /// this component and against the others, best effort - another component
        /// that has not solved yet has nothing to compare.
        /// </summary>
        private string PathOverlap(IReadOnlyList<string[]> paths)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                for (int j = i + 1; j < paths.Count; j++)
                {
                    if (LayerSource.Overlaps(paths[i], paths[j]))
                    {
                        return $"“{LayerSource.Display(paths[i])}” and “{LayerSource.Display(paths[j])}” "
                             + "overlap, so the objects under both would be counted twice.";
                    }
                }
            }

            var document = OnPingDocument();
            if (document == null)
            {
                return null;
            }

            foreach (var other in document.Objects.OfType<CCGroupComponent>())
            {
                if (ReferenceEquals(other, this) || other.Locked)
                {
                    continue;
                }

                foreach (var theirs in other.DeclaredPaths())
                {
                    foreach (var mine in paths)
                    {
                        if (LayerSource.Overlaps(mine, theirs))
                        {
                            return $"“{LayerSource.Display(mine)}” overlaps “{LayerSource.Display(theirs)}” "
                                 + $"on {(string.IsNullOrWhiteSpace(other.NickName) ? "another CC Group" : other.NickName)}, "
                                 + "so the objects under both would be counted twice.";
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The paths on this component's Layer input, read from the parameter rather
        /// than from the last solve: a component that has not run yet still knows
        /// what it was asked for.
        /// </summary>
        private IEnumerable<string[]> DeclaredPaths()
        {
            if (Params.Input.Count < 2)
            {
                yield break;
            }

            foreach (var goo in Params.Input[1].VolatileData.AllData(true))
            {
                var path = LayerSource.Normalise(goo?.ToString());
                if (path != null)
                {
                    yield return path;
                }
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (Locked)
            {
                return;
            }

            foreach (var goo in Params.Output[0].VolatileData.AllData(true))
            {
                if (TaggedGeometry.TryUnwrap(goo, out var tagged) && tagged.Geometry is IGH_PreviewData previewData)
                {
                    var wireArgs = new GH_PreviewWireArgs(args.Viewport, args.Display, tagged.Color, args.DefaultCurveThickness);
                    previewData.DrawViewportWires(wireArgs);
                }
            }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if (Locked)
            {
                return;
            }

            foreach (var goo in Params.Output[0].VolatileData.AllData(true))
            {
                if (TaggedGeometry.TryUnwrap(goo, out var tagged) && tagged.Geometry is IGH_PreviewData previewData)
                {
                    var material = new DisplayMaterial(tagged.Color);
                    var meshArgs = new GH_PreviewMeshArgs(args.Viewport, args.Display, material, args.MeshingParameters);
                    previewData.DrawViewportMeshes(meshArgs);
                }
            }
        }

        /* No Refresh button. Grasshopper does not recompute when a Rhino layer
           changes, and a button nobody remembers to press is how a viewer ends up
           showing last week's model with nothing to say it is wrong. The component
           listens instead, and only to the layers it actually read. */

        private bool _listening;

        /// <summary>Set every solve: a component reading geometry does not care about layers.</summary>
        private bool _usesLayers;

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            Listen(true);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            Listen(false);
            base.RemovedFromDocument(document);
        }

        private void Listen(bool on)
        {
            if (on == _listening)
            {
                return;
            }

            _listening = on;

            if (!on)
            {
                Pending.Remove(this);   // never expire a component that has left
            }

            if (on)
            {
                RhinoDoc.AddRhinoObject += OnObjectAdded;
                RhinoDoc.DeleteRhinoObject += OnObjectDeleted;
                RhinoDoc.UndeleteRhinoObject += OnObjectUndeleted;
                RhinoDoc.PurgeRhinoObject += OnObjectPurged;
                RhinoDoc.ReplaceRhinoObject += OnObjectReplaced;
                RhinoDoc.ModifyObjectAttributes += OnAttributesChanged;
                RhinoDoc.LayerTableEvent += OnLayerTableChanged;
            }
            else
            {
                RhinoDoc.AddRhinoObject -= OnObjectAdded;
                RhinoDoc.DeleteRhinoObject -= OnObjectDeleted;
                RhinoDoc.UndeleteRhinoObject -= OnObjectUndeleted;
                RhinoDoc.PurgeRhinoObject -= OnObjectPurged;
                RhinoDoc.ReplaceRhinoObject -= OnObjectReplaced;
                RhinoDoc.ModifyObjectAttributes -= OnAttributesChanged;
                RhinoDoc.LayerTableEvent -= OnLayerTableChanged;
            }
        }

        /// <summary>
        /// A path names layers, so the layer table is part of what this component
        /// reads - renaming a layer can invalidate a path without touching a single
        /// object, and watching only the objects left the component showing results
        /// from a path that no longer existed.
        ///
        /// Deliberately unfiltered. The watched indices are the layers a path
        /// resolved to, not the layers above them, so renaming "Option 2" would not
        /// match a component watching "Option 2/Conc" - which is exactly the case
        /// that went wrong. Layer edits are rare next to object edits, and the
        /// coalescing timer absorbs a burst of them.
        /// </summary>
        private void OnLayerTableChanged(object sender, LayerTableEventArgs e) => DocumentChanged();

        /* Every one of these says the same thing: the document changed, so a
           component that reads the document should look again.

           There used to be a filter here. Each event was checked against the set
           of layer indices the last solve had resolved, so that editing an
           unrelated part of the model cost nothing. It was an optimisation, and it
           produced a correctness bug every time the set turned out to be the wrong
           question: renaming a parent layer, whose index is not in the set because
           only the leaves are; moving an object out of a watched layer, where the
           new layer is the one reported and the old one is the one that cares.
           Each failure looked identical from the outside - the group quietly went
           on showing geometry it no longer owned.

           Trading correctness for speed is a bad trade when being wrong is silent,
           so the filter is gone. Anything that changes the document expires every
           component that reads a layer, and the coalescing timer keeps a burst of
           changes down to one recomputation. */

        private void OnObjectAdded(object sender, RhinoObjectEventArgs e) => DocumentChanged();

        private void OnObjectDeleted(object sender, RhinoObjectEventArgs e) => DocumentChanged();

        /* Undoing a delete does not re-add the object, it undeletes it, and these
           are separate events. Listening to one and not the other left the group
           empty after the geometry was already back on screen. */

        private void OnObjectUndeleted(object sender, RhinoObjectEventArgs e) => DocumentChanged();

        private void OnObjectPurged(object sender, RhinoObjectEventArgs e) => DocumentChanged();

        private void OnObjectReplaced(object sender, RhinoReplaceObjectEventArgs e) => DocumentChanged();

        private void OnAttributesChanged(object sender, RhinoModifyObjectAttributesEventArgs e) => DocumentChanged();

        private void DocumentChanged()
        {
            if (_usesLayers)
            {
                Expire();
            }
        }

        /// <summary>
        /// Marked for recomputation, not recomputed here: a Rhino event can arrive
        /// in the middle of an operation that is still changing other objects, and
        /// solving inside it would run against a half-finished document.
        /// </summary>
        /* Deleting a hundred objects raises a hundred events, and recomputing on
           each one made a batch delete crawl. One user operation should cost one
           recomputation, so an event only marks the component and restarts a shared
           timer; the recomputation happens once the document has been quiet for a
           moment. The timer is shared across every CC Group so that eight of them
           watching the same layer still produce a single solution. */

        private const int QuietMilliseconds = 200;

        private static readonly HashSet<CCGroupComponent> Pending = new HashSet<CCGroupComponent>();
        private static System.Windows.Forms.Timer _coalesce;

        private void Expire()
        {
            // Rhino raises these on the UI thread, which is where the timer lives
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                Pending.Add(this);

                if (_coalesce == null)
                {
                    _coalesce = new System.Windows.Forms.Timer { Interval = QuietMilliseconds };
                    _coalesce.Tick += OnQuiet;
                }

                _coalesce.Stop();    // restart: the operation is still going
                _coalesce.Start();
            }));
        }

        private static void OnQuiet(object sender, EventArgs e)
        {
            _coalesce.Stop();

            var due = Pending.ToArray();
            Pending.Clear();

            // mark them all first, then ask each document for one solution
            var documents = new HashSet<GH_Document>();
            foreach (var component in due)
            {
                var document = component.OnPingDocument();
                if (document == null || document.SolutionState == GH_ProcessStep.Process)
                {
                    continue;
                }

                component.ExpireSolution(false);
                documents.Add(document);
            }

            foreach (var document in documents)
            {
                document.NewSolution(false);
            }
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetDrawingColor("SwatchColor", CurrentColor);
            return base.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (reader.ItemExists("SwatchColor"))
            {
                CurrentColor = reader.GetDrawingColor("SwatchColor");
            }

            return base.Read(reader);
        }

        protected override Bitmap Icon => _icon;

        public override Guid ComponentGuid => new Guid("3d2f8b1a-7c4e-4a9d-8e1b-5f6a7c8d9e0f");

        private static Bitmap CreateIcon()
        {
            var bitmap = new Bitmap(24, 24);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                using (var brush = new SolidBrush(Color.FromArgb(230, 126, 34)))
                {
                    graphics.FillEllipse(brush, 2, 2, 20, 20);
                }

                using (var pen = new Pen(Color.Black, 1.5f))
                {
                    graphics.DrawEllipse(pen, 2, 2, 20, 20);
                }
            }

            return bitmap;
        }
    }
}
