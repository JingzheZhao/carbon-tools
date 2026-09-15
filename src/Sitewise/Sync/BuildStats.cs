using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Sitewise.Sync
{
    /// <summary>
    /// A stopwatch breakdown of one payload build, reported on the CC Sync
    /// component so we can see where a slow sync actually spends its time
    /// rather than guessing. Measurement only - nothing here changes what is
    /// built or served.
    ///
    /// Since the per-object work runs in parallel, the buckets add up to CPU
    /// time, not elapsed time: eight threads busy for a second contribute eight
    /// seconds. The wall figure is measured separately and is the one that
    /// answers "how long did I wait". The buckets keep their value as a
    /// comparison between steps.
    ///
    /// Counters are written from several threads, so every one of them goes
    /// through Interlocked.
    /// </summary>
    public sealed class BuildStats
    {
        private int _objects;
        private int _meshed;
        private int _vertices;
        private int _faces;

        private long _originTicks;
        private long _idTicks;
        private long _quantityTicks;
        private long _tessellateTicks;
        private long _meshJsonTicks;
        private long _assembleTicks;

        public int Objects { get { return _objects; } set { _objects = value; } }

        /// <summary>Filled in by the caller: JObject to string is not this class's work.</summary>
        public long SerializeTicks;

        /// <summary>Filled in by the caller: elapsed time for the whole build.</summary>
        public long WallTicks;

        /// <summary>Filled in by the caller: the length of the served JSON, in bytes.</summary>
        public long PayloadBytes;

        public static long Now { get { return Stopwatch.GetTimestamp(); } }

        public static double ToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        public void AddOrigin(long ticks) { Interlocked.Add(ref _originTicks, ticks); }
        public void AddId(long ticks) { Interlocked.Add(ref _idTicks, ticks); }
        public void AddQuantity(long ticks) { Interlocked.Add(ref _quantityTicks, ticks); }
        public void AddTessellate(long ticks) { Interlocked.Add(ref _tessellateTicks, ticks); }
        public void AddMeshJson(long ticks) { Interlocked.Add(ref _meshJsonTicks, ticks); }
        public void AddAssemble(long ticks) { Interlocked.Add(ref _assembleTicks, ticks); }

        public void CountMesh(int vertices, int faces)
        {
            Interlocked.Increment(ref _meshed);
            Interlocked.Add(ref _vertices, vertices);
            Interlocked.Add(ref _faces, faces);
        }

        private double CpuMs
        {
            get
            {
                return ToMs(_originTicks + _idTicks + _quantityTicks + _tessellateTicks
                            + _meshJsonTicks + _assembleTicks + SerializeTicks);
            }
        }

        /// <summary>
        /// Pipeline order rather than slowest first: a fixed order is what makes two
        /// runs comparable at a glance, which is the whole point of printing it.
        /// </summary>
        public string Report()
        {
            double cpu = CpuMs;
            var text = new StringBuilder();

            text.Append("Waited ").Append(Ms(ToMs(WallTicks)))
                .Append("  (").Append(Ms(cpu)).AppendLine(" of CPU across all threads)");

            Line(text, "bounding box", _originTicks, cpu);
            Line(text, "stable id", _idTicks, cpu);
            Line(text, "area/volume", _quantityTicks, cpu);
            Line(text, "tessellate", _tessellateTicks, cpu);
            Line(text, "mesh json", _meshJsonTicks, cpu);
            Line(text, "json build", _assembleTicks, cpu);
            Line(text, "serialize", SerializeTicks, cpu);

            text.Append(N(_objects)).Append(" objects, ").Append(N(_meshed)).AppendLine(" meshed");
            text.Append(N(_vertices)).Append(" vertices, ").Append(N(_faces)).AppendLine(" faces");
            text.Append(Mb(PayloadBytes)).Append(" payload");
            return text.ToString();
        }

        private static void Line(StringBuilder text, string label, long ticks, double cpu)
        {
            double ms = ToMs(ticks);
            double share = cpu > 0.0 ? ms / cpu * 100.0 : 0.0;
            text.Append("  ").Append(label.PadRight(13)).Append(Ms(ms).PadLeft(9))
                .Append("  ").Append(share.ToString("F0", CultureInfo.InvariantCulture)).AppendLine("%");
        }

        private static string N(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string Ms(double ms)
        {
            return ms >= 1000.0
                ? (ms / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " s"
                : ms.ToString("F0", CultureInfo.InvariantCulture) + " ms";
        }

        private static string Mb(long bytes)
        {
            return bytes >= 1024 * 1024
                ? (bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture) + " MB"
                : (bytes / 1024.0).ToString("F0", CultureInfo.InvariantCulture) + " KB";
        }
    }
}
