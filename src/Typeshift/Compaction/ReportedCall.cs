namespace Typeshift;

// D32: compaction's estimate of a model call as it was sent and the input tokens its provider reported for it; how far
// the two lie apart corrects the later estimates
internal sealed record ReportedCall(long Estimated, long Reported)
{
    public double Calibration => Math.Clamp(Reported / (double)Estimated, 0.25, 4);
}
