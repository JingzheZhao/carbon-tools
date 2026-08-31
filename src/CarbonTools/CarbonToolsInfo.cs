using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace CarbonTools
{
    public class CarbonToolsInfo : GH_AssemblyInfo
    {
        public override string Name => "Carbon Tools";

        public override Bitmap Icon => null;

        public override string Description =>
            "Grasshopper-side geometry/color grouping tool for carbon assessment";

        public override Guid Id => new Guid("6a5c9f2e-6f4d-4b3a-9e7a-1f2c3d4e5a6b");

        public override string AuthorName => "Carbon Tools";

        public override string AuthorContact => "";
    }
}
