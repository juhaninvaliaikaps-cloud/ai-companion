namespace AICompanion.Core
{
    /// <summary>
    /// Lets the animation director name a portrait expression that the five-value
    /// <see cref="Emotion"/> enum cannot express, such as embarrassed, shy or teasing.
    /// The portrait reads this when it is set and falls back to the emotion name otherwise.
    /// </summary>
    public interface IExpressionHint
    {
        string ExpressionName { get; set; }
    }
}
