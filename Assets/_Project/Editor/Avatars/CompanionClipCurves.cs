namespace AICompanion.Editor.Avatars
{
    /// <summary>
    /// Muscle curves for the generated clips. The committed .anim files use the same
    /// numbers. Rebuild writes them back through the Unity animation API.
    /// </summary>
    static class CompanionClipCurves
    {
        public static void Populate(UnityEngine.AnimationClip clip)
        {
            switch (clip.name)
            {
                case "Idle":
                    CompanionAnimatorBaker.Curve(clip, "Spine Front-Back", 4f, 0.02f, 0.035f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Chest Front-Back", 4f, 0.01f, 0.02f, 1f, 0.6f);
                    CompanionAnimatorBaker.Curve(clip, "UpperChest Front-Back", 4f, 0f, 0.015f, 1f, 0.4f);
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 4f, 0f, 0.03f, 0.5f, 0.2f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 4f, 0f, 0.045f, 0.5f, 1f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Down-Up", 4f, -0.5f, 0.02f, 0.5f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 4f, -0.5f, 0.02f, 0.5f, 0.4f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Front-Back", 4f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Front-Back", 4f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Forearm Stretch", 4f, 0.2f, 0.02f, 0.5f, 0.5f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 4f, 0.2f, 0.02f, 0.5f, 0.8f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Twist In-Out", 4f, 0.2f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Twist In-Out", 4f, -0.2f, 0f, 0f, 0f);
                    break;
                case "Happy":
                    CompanionAnimatorBaker.Curve(clip, "Spine Front-Back", 2f, -0.04f, 0.04f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Chest Front-Back", 2f, -0.03f, 0.03f, 2f, 0.2f);
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 2f, 0.1f, 0.04f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 2f, 0f, 0.03f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Down-Up", 2f, -0.18f, 0.06f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 2f, -0.18f, 0.06f, 2f, 0.15f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Front-Back", 2f, 0.02f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Front-Back", 2f, 0.02f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Forearm Stretch", 2f, 0.12f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 2f, 0.12f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Shoulder Down-Up", 2f, 0.12f, 0.04f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Shoulder Down-Up", 2f, 0.12f, 0.04f, 2f, 0.15f);
                    break;
                case "Sad":
                    CompanionAnimatorBaker.Curve(clip, "Spine Front-Back", 3f, 0.18f, 0.02f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Chest Front-Back", 3f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "UpperChest Front-Back", 3f, 0.06f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 3f, -0.28f, 0.03f, 1f, 0.5f);
                    CompanionAnimatorBaker.Curve(clip, "Head Tilt Left-Right", 3f, 0.08f, 0.03f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 3f, 0.04f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Down-Up", 3f, -0.72f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 3f, -0.72f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Shoulder Down-Up", 3f, -0.22f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Shoulder Down-Up", 3f, -0.22f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Forearm Stretch", 3f, 0.22f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 3f, 0.22f, 0f, 0f, 0f);
                    break;
                case "Surprised":
                    CompanionAnimatorBaker.Curve(clip, "Spine Front-Back", 1.6f, -0.1f, 0.02f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Chest Front-Back", 1.6f, -0.06f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 1.6f, 0.22f, 0.03f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 1.6f, 0f, 0.02f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Down-Up", 1.6f, 0.05f, 0.04f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 1.6f, 0.05f, 0.04f, 1f, 0.2f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Front-Back", 1.6f, -0.12f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Front-Back", 1.6f, -0.12f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Forearm Stretch", 1.6f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 1.6f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Jaw Close", 1.6f, -0.12f, 0.04f, 2f, 0f);
                    break;
                case "Thinking":
                    CompanionAnimatorBaker.Curve(clip, "Head Tilt Left-Right", 3.2f, 0.22f, 0.03f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 3.2f, 0.14f, 0.02f, 1f, 0.4f);
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 3.2f, -0.06f, 0.02f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Spine Twist Left-Right", 3.2f, 0.06f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Down-Up", 3.2f, -0.05f, 0.02f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Arm Front-Back", 3.2f, 0.42f, 0.02f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Left Forearm Stretch", 3.2f, 0.62f, 0.03f, 1f, 0.3f);
                    CompanionAnimatorBaker.Curve(clip, "Left Shoulder Front-Back", 3.2f, 0.15f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 3.2f, -0.55f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Front-Back", 3.2f, 0.08f, 0f, 0f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 3.2f, 0.2f, 0f, 0f, 0f);
                    break;
                case "Talk":
                    CompanionAnimatorBaker.Curve(clip, "Head Nod Down-Up", 1.2f, 0f, 0.06f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Head Turn Left-Right", 1.2f, 0f, 0.03f, 1f, 0.5f);
                    CompanionAnimatorBaker.Curve(clip, "Jaw Close", 1.2f, -0.08f, 0.07f, 2f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Spine Front-Back", 1.2f, 0f, 0.025f, 2f, 0.3f);
                    CompanionAnimatorBaker.Curve(clip, "Chest Front-Back", 1.2f, 0f, 0.02f, 2f, 0.5f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Down-Up", 1.2f, 0f, 0.1f, 1f, 0f);
                    CompanionAnimatorBaker.Curve(clip, "Right Arm Front-Back", 1.2f, 0f, 0.06f, 1f, 0.4f);
                    CompanionAnimatorBaker.Curve(clip, "Right Forearm Stretch", 1.2f, 0f, 0.05f, 1f, 0.2f);
                    break;
            }
        }
    }
}
