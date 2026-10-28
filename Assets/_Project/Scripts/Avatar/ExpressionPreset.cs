namespace AICompanion.Avatar
{
    /// <summary>
    /// Expression slots shared by VRM presets, ARKit blendshapes, and Daz-style names.
    /// <see cref="Blush"/> stays last. <c>EmotionFace.Collect</c> walks every slot up to it.
    /// </summary>
    public enum ExpressionPreset
    {
        None = 0,
        Aa,
        Ih,
        Ou,
        Ee,
        Oh,
        Blink,
        Joy,
        Angry,
        Sorrow,
        Fun,
        Surprised,
        EyeSquint,
        BrowInnerUp,
        BrowOuterUp,
        EyeWide,
        EyeLookDown,
        Blush
    }
}
