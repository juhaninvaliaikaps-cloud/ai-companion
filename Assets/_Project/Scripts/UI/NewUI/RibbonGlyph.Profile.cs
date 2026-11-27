using AICompanion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    sealed partial class RibbonGlyph
    {
        internal static Mark MoodMark(CompanionMood mood) => mood switch
        {
            CompanionMood.Happy => Mark.MoodHappy,
            CompanionMood.Affectionate => Mark.Heart,
            CompanionMood.Flustered => Mark.MoodFlustered,
            CompanionMood.Annoyed => Mark.MoodAnnoyed,
            CompanionMood.Sad => Mark.MoodSad,
            CompanionMood.Anxious => Mark.MoodAnxious,
            CompanionMood.Sleepy => Mark.MoodSleepy,
            CompanionMood.Intimate => Mark.MoodIntimate,
            _ => Mark.MoodContent,
        };

        void ProfileRing(VertexHelper mesh, float x, float y, float radius)
            => Ring(mesh, new Vector2(x / 18f, 1f - y / 18f), radius / 18f);

        void DrawProfileMark(VertexHelper mesh)
        {
            switch (_mark)
            {
                case Mark.MoodContent:
                    TraitPath(mesh, 1, 11, 4, 8, 7, 8, 10, 11, 13, 11, 17, 7);
                    break;
                case Mark.MoodHappy:
                    ProfileRing(mesh, 9, 9, 7.3f);
                    TraitPath(mesh, 6, 6, 6, 7.4f);
                    TraitPath(mesh, 12, 6, 12, 7.4f);
                    TraitPath(mesh, 5.5f, 10.5f, 7.2f, 12.3f, 10.8f, 12.3f, 12.5f, 10.5f);
                    break;
                case Mark.MoodFlustered:
                    ProfileRing(mesh, 9, 9, 7.3f);
                    TraitPath(mesh, 5, 7.5f, 7, 6, 7.5f, 8);
                    TraitPath(mesh, 10.5f, 8, 11, 6, 13, 7.5f);
                    TraitPath(mesh, 4, 10, 3.5f, 11.5f);
                    TraitPath(mesh, 14.5f, 10, 14, 11.5f);
                    TraitPath(mesh, 7, 12, 9, 10.7f, 11, 12);
                    break;
                case Mark.MoodAnnoyed:
                    TraitPath(mesh, 10, 1, 3.5f, 10, 8, 10, 7, 17, 14.5f, 7.5f, 10, 7.5f, 10, 1);
                    break;
                case Mark.MoodSad:
                    TraitPath(mesh, 9, 1.5f, 4, 8, 3.5f, 11.5f, 5, 14.5f, 9, 16, 13, 14.5f, 14.5f, 11.5f, 14, 8, 9, 1.5f);
                    TraitPath(mesh, 6.2f, 10.6f, 6.5f, 12, 7.8f, 13.1f);
                    break;
                case Mark.MoodAnxious:
                    TraitPath(mesh, 8.5f, 2, 11, 6, 7, 9, 11, 13, 8.5f, 16);
                    TraitPath(mesh, 3.5f, 5, 2, 9, 3.5f, 13);
                    TraitPath(mesh, 14.5f, 5, 16, 9, 14.5f, 13);
                    break;
                case Mark.MoodSleepy:
                    TraitPath(mesh, 9, 2, 5.5f, 3.5f, 3, 7, 3, 11, 5, 14.5f, 8.5f, 16, 12.5f, 15.5f,
                        15.5f, 12.5f, 12, 12, 9.3f, 10, 8, 6.5f, 9, 2);
                    break;
                case Mark.MoodIntimate:
                    TraitPath(mesh, 9, 1, 12, 6.5f, 15, 9.5f, 15, 13.5f, 12, 16, 7, 16, 4, 13, 4, 9.5f, 6.5f, 11, 9, 1);
                    TraitPath(mesh, 9, 10.5f, 11.3f, 13.3f, 10.3f, 15.6f);
                    break;
                case Mark.Link:
                    TraitPath(mesh, 9, 6, 7, 4, 5, 4, 2, 7, 2, 10, 4, 12, 6, 12, 8, 10);
                    TraitPath(mesh, 9, 12, 11, 14, 13, 14, 16, 11, 16, 8, 14, 6, 12, 6, 10, 8);
                    TraitPath(mesh, 6, 10, 12, 8);
                    break;
                case Mark.Heart:
                case Mark.CrackedHeart:
                    TraitPath(mesh, 9, 16, 2, 9, 1.5f, 6, 3.5f, 3.5f, 6.5f, 3.5f, 9, 6,
                        11.5f, 3.5f, 14.5f, 3.5f, 16.5f, 6, 16, 9, 9, 16);
                    if (_mark == Mark.CrackedHeart)
                        TraitPath(mesh, 9, 6, 7, 9, 10, 10, 8, 13);
                    break;
                case Mark.People:
                    ProfileRing(mesh, 6, 5.5f, 2.2f);
                    ProfileRing(mesh, 12.5f, 6, 1.8f);
                    TraitPath(mesh, 1, 15, 2.5f, 11, 4.5f, 10, 7.5f, 10, 9.5f, 11, 11, 15, 1, 15);
                    TraitPath(mesh, 11, 11, 14, 10.5f, 16, 12, 17, 15, 13, 15);
                    break;
                case Mark.Activity:
                    TraitPath(mesh, 1, 10, 5, 10, 7, 5, 10, 14, 12, 9, 17, 9);
                    break;
                case Mark.Rise:
                    TraitPath(mesh, 4.5f, 7.5f, 9, 3, 13.5f, 7.5f);
                    TraitPath(mesh, 9, 3, 9, 15);
                    break;
                case Mark.Fall:
                    TraitPath(mesh, 4.5f, 10.5f, 9, 15, 13.5f, 10.5f);
                    TraitPath(mesh, 9, 3, 9, 15);
                    break;
                case Mark.Key:
                    ProfileRing(mesh, 6, 5.5f, 3.5f);
                    TraitPath(mesh, 8.4f, 8.2f, 15.5f, 15.5f, 17, 14);
                    TraitPath(mesh, 13.2f, 13.2f, 15, 11.4f);
                    break;
                case Mark.Compass:
                    ProfileRing(mesh, 9, 9, 7.5f);
                    TraitPath(mesh, 5, 13, 7.2f, 7.2f, 13, 5, 10.8f, 10.8f, 5, 13);
                    break;
                case Mark.Calendar:
                    Box(mesh, 0.12f, 0.15f, 0.88f, 0.76f);
                    TraitPath(mesh, 2.2f, 8, 15.8f, 8);
                    TraitPath(mesh, 5.5f, 2, 5.5f, 6);
                    TraitPath(mesh, 12.5f, 2, 12.5f, 6);
                    TraitPath(mesh, 5.2f, 11.8f, 6.1f, 11.8f);
                    TraitPath(mesh, 9, 11.8f, 9.9f, 11.8f);
                    TraitPath(mesh, 12.8f, 11.8f, 13.7f, 11.8f);
                    break;
                case Mark.Book:
                    TraitPath(mesh, 9, 5, 6, 3.3f, 1.8f, 3.7f, 1.8f, 13.2f, 6, 12.8f, 9, 14.5f,
                        12, 12.8f, 16.2f, 13.2f, 16.2f, 3.7f, 12, 3.3f, 9, 5, 9, 14.5f);
                    break;
                case Mark.HollowDot:
                    ProfileRing(mesh, 9, 9, 5f);
                    break;
                case Mark.Spark:
                    TraitPath(mesh, 9, 1, 10.8f, 6.9f, 17, 9, 10.8f, 11, 9, 17, 7.2f, 11, 1, 9, 7.2f, 7, 9, 1);
                    break;
            }
        }
    }
}
