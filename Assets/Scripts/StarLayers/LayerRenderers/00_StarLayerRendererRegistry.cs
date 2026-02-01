using System.Collections.Generic;

public sealed class StarLayerRendererRegistry
{
    readonly Dictionary<string, IStarLayerRenderer> renderers = new Dictionary<string, IStarLayerRenderer>();
    readonly IStarLayerRenderer fallback = new StarLayerRendererBaseImpl();

    public StarLayerRendererRegistry()
    {
        Register(new Layer01_CoreRenderer());
        Register(new Layer02_BodyCoatRenderer());
        Register(new Layer03_AdhesionCoatRenderer());
        Register(new Layer04_DelayRenderer());
        Register(new Layer05_BarrierBlindRenderer());
        Register(new Layer06_SealCoatRenderer());
        Register(new Layer07_PrimeRenderer());
        Register(new Layer08_StarterRenderer());
        Register(new Layer09_DarkPrimeRenderer());
        Register(new Layer10_SteadyColorHighRenderer());
        Register(new Layer11_SteadyColorLowRenderer());
        Register(new Layer12_IlluminationFlareRenderer());
        Register(new Layer13_ChrysanthemumTailRenderer());
        Register(new Layer14_WillowKamuroTailRenderer());
        Register(new Layer15_WabiTailRenderer());
        Register(new Layer16_TigerTailRenderer());
        Register(new Layer17_BrocadeGoldTailRenderer());
        Register(new Layer18_GoldDustRenderer());
        Register(new Layer19_SilverLineRenderer());
        Register(new Layer20_SilverKamuroRenderer());
        Register(new Layer21_IronSparksRenderer());
        Register(new Layer22_GraniteSpreaderRenderer());
        Register(new Layer23_GlitterRenderer());
        Register(new Layer24_TremolantRenderer());
        Register(new Layer25_FlitterSpangleRenderer());
        Register(new Layer26_FireflyOverlayRenderer());
        Register(new Layer27_StrobeRenderer());
        Register(new Layer28_CrackleRenderer());
        Register(new Layer29_CrossetteSplitRenderer());
        Register(new Layer30_FallingLeavesRenderer());
        Register(new Layer31_AfterglowRenderer());
        Register(new Layer32_TerminalFlashRenderer());
        Register(new Layer33_TerminalSlowFadeRenderer());
    }

    void Register(IStarLayerRenderer renderer)
    {
        if (renderer == null || string.IsNullOrEmpty(renderer.Id)) return;
        renderers[renderer.Id] = renderer;
    }

    public IStarLayerRenderer Get(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return fallback;
        if (renderers.TryGetValue(def.id, out var renderer))
            return renderer;
        return fallback;
    }

    sealed class StarLayerRendererBaseImpl : StarLayerRendererBase { }
}
