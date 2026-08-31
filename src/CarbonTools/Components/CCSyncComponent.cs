using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;

using CarbonTools.Sync;

namespace CarbonTools.Components
{
    /// <summary>
    /// CC Sync: collects Tagged Objects from one or more CC Group components and
    /// serves them as JSON over a local HTTP server, per the sync protocol
    /// (docs/decision-record.md sections 15-16).
    /// Run keeps the server alive and refreshed every solve; Open launches the
    /// browser once per click, so dragging a slider doesn't spawn dozens of tabs.
    /// </summary>
    public class CCSyncComponent : GH_Component, IDisposable
    {
        private static readonly Bitmap _icon = CreateIcon();

        private readonly CarbonSyncServer _server = new CarbonSyncServer();
        private bool _previousOpen;

        public CCSyncComponent()
            : base(
                "CC Sync",
                "CCSync",
                "Serve tagged geometry as JSON over a local server for the web viewer",
                "Carbon",
                "Tools")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Objects", "O", "Tagged Objects from one or more CC Group components", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "R", "While true, keep the local sync server running and refreshed every solve", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Open", "Op", "Open the sync page in the default browser (fires once per click)", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Current sync status", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Port", "P", "Local server port (-1 when not running)", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var wrapped = new List<IGH_Goo>();
            DA.GetDataList(0, wrapped);

            bool run = false;
            DA.GetData(1, ref run);

            bool open = false;
            DA.GetData(2, ref open);

            var tagged = new List<TaggedGeometry>();
            foreach (var goo in wrapped)
            {
                if (TaggedGeometry.TryUnwrap(goo, out var item))
                {
                    tagged.Add(item);
                }
            }

            string status;
            int port = -1;

            if (run)
            {
                _server.Start();

                JObject payload = SyncJsonBuilder.Build(tagged);
                int groupCount = tagged.Select(t => t.Color.ToArgb()).Distinct().Count();
                string statusLine = $"{tagged.Count} objects across {groupCount} groups";

                _server.UpdateData(payload.ToString(Newtonsoft.Json.Formatting.None), statusLine);

                port = _server.Port;
                status = $"Running on http://localhost:{port}/ - {statusLine}";
            }
            else
            {
                _server.Stop();
                status = "Idle (Run is off)";
            }

            if (open && !_previousOpen && _server.IsRunning)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"http://localhost:{_server.Port}/",
                    UseShellExecute = true
                });
            }

            _previousOpen = open;

            DA.SetData(0, status);
            DA.SetData(1, port);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            _server.Stop();
            base.RemovedFromDocument(document);
        }

        public void Dispose()
        {
            _server.Dispose();
        }

        protected override Bitmap Icon => _icon;

        public override Guid ComponentGuid => new Guid("7e3a1c4b-9d5e-4f2a-8b6c-1a2b3c4d5e6f");

        private static Bitmap CreateIcon()
        {
            var bitmap = new Bitmap(24, 24);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    graphics.FillEllipse(brush, 2, 2, 20, 20);
                }

                using (var pen = new Pen(Color.Black, 1.5f))
                {
                    graphics.DrawEllipse(pen, 2, 2, 20, 20);
                }

                using (var arrowPen = new Pen(Color.White, 2f))
                {
                    graphics.DrawArc(arrowPen, 6, 6, 12, 12, -30, 210);
                    graphics.DrawArc(arrowPen, 6, 6, 12, 12, 150, 210);
                }
            }

            return bitmap;
        }
    }
}
