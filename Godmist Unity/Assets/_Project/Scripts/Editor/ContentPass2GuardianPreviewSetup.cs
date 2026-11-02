namespace Godmist.Editor
{
    // Legacy editor entry point cannot restore the retired T6/T7 design preview.
    public static class ContentPass2GuardianPreviewSetup
    {
        public static void Apply()=>ContentPass2GuardianSetup.Apply();
    }
}
