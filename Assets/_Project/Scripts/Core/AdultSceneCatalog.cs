using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: names for the third-window adult clips. They are not part of the 2D video
    /// avatar's own reaction list, so the model needs them in the animation prompt too.
    /// Only offered while the 18+ gate is open.
    /// </summary>
    public static class AdultSceneCatalog
    {
        public static string PromptLine()
        {
            if (!IntimateExpressions.Active)
                return string.Empty;

            return "Third-window 18+ clip cues. Use reaction=\"name\" when the scene clearly matches one: "
                + "blowjob_kneeling (she kneels and gives him a blowjob), "
                + "blowjob_lying (she lies down and gives him a blowjob), "
                + "blowjob_ontop (she is on top and gives him a blowjob), "
                + "lying_kiss (a kiss while you lie together), "
                + "lying_make_out or lying_make_out_2 (a longer lying make-out loop). "
                + "Use them only when the scene is already intimate and both of you clearly want it. "
                + "Do not use them as ordinary emotions or gestures.";
        }
    }
}
