namespace LCRanked
{
    public static class SeedManager
    {
        public static void Apply(StartOfRound startOfRound, int seed)
        {
            startOfRound.overrideRandomSeed = true;
            startOfRound.overrideSeedNumber = seed;
            Plugin.Log.LogInfo($"[LCRanked] Seed override applied: {seed}");
        }

        public static void Clear(StartOfRound startOfRound)
        {
            startOfRound.overrideRandomSeed = false;
        }
    }
}
