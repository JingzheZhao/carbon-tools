using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Display;

namespace CarbonTools.Components
{
    /// <summary>
    /// CC Group: user picks a color via the swatch on the component body and
    /// feeds geometry in. The color is the group key.
    /// See E:\Carbon\Carbon工具-方法论决策记录.md sections 13-14.
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
            pManager.AddGeometryParameter("Geometry", "G", "Geometry belonging to this group", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Tagged Objects", "T", "Geometry tagged with the group color", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var geometry = new List<IGH_GeometricGoo>();
            if (!DA.GetDataList(0, geometry))
            {
                return;
            }

            var tagged = new List<GH_ObjectWrapper>(geometry.Count);
            foreach (var goo in geometry)
            {
                tagged.Add(new GH_ObjectWrapper(new TaggedGeometry(goo, CurrentColor)));
            }

            DA.SetDataList(0, tagged);
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
