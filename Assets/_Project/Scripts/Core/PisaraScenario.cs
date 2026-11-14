using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>One scripted message in a scenario: how it landed, her cue intensity, and the chat time after it.</summary>
    public struct ScenarioStep
    {
        public LandingEvent Landing;
        public float Intensity;
        public double Seconds;

        public ScenarioStep(LandingEvent landing, float intensity = 0.5f, double seconds = 30d)
        {
            Landing = landing;
            Intensity = intensity;
            Seconds = seconds;
        }
    }

    /// <summary>Where things stood after one step.</summary>
    public struct ScenarioRow
    {
        public int Step;
        public LandingEvent Landing;
        public float Affection;
        public float Familiarity;
        public CompanionMood Shown;
        public string Reason;
    }

    /// <summary>
    /// A fixed persona and a scripted conversation, run through the real relationship math
    /// and mood state (JUH-117). Shows how affection, familiarity and her mood move without
    /// a model or a scene, so the numbers in <see cref="RelationshipMath"/> can be tuned and
    /// the JUH-115 expectations checked by tests. Runs at 14:00, so night sleepiness stays out.
    /// </summary>
    public sealed class PisaraScenario
    {
        public const float AfternoonHour = 14f;

        public readonly string Name;
        public readonly SheetRelationship Start;
        public readonly string Temperament;
        public readonly ScenarioStep[] Steps;

        public PisaraScenario(string name, SheetRelationship start, string temperament, params ScenarioStep[] steps)
        {
            Name = name ?? string.Empty;
            Start = start ?? new SheetRelationship();
            Temperament = temperament ?? PersonaTemperaments.DefaultId;
            Steps = steps ?? new ScenarioStep[0];
        }

        /// <summary>The same message <paramref name="count"/> times.</summary>
        public static ScenarioStep[] Repeat(LandingEvent landing, int count, float intensity = 0.5f, double seconds = 30d)
        {
            if (count < 0)
                count = 0;
            var steps = new ScenarioStep[count];
            for (var i = 0; i < count; i++)
                steps[i] = new ScenarioStep(landing, intensity, seconds);
            return steps;
        }

        /// <summary>A starting relationship, as a reading would give it.</summary>
        public static SheetRelationship Relationship(string role, string label, float affection, float familiarity, bool sharedHistory)
        {
            return new SheetRelationship
            {
                role = role,
                label = label ?? string.Empty,
                affection = affection,
                familiarity = familiarity,
                sharedHistory = sharedHistory,
            };
        }

        public ScenarioResult Run()
        {
            float affection;
            float familiarity;
            RelationshipMath.Start(Start, out affection, out familiarity);

            var mood = new CompanionMoodState { Temperament = PersonaTemperaments.Get(Temperament) };
            var history = new RelationshipHistory();
            var rows = new List<ScenarioRow>(Steps.Length + 1)
            {
                new ScenarioRow { Step = 0, Landing = LandingEvent.Neutral, Affection = affection, Familiarity = familiarity, Shown = mood.Shown, Reason = "start" },
            };

            for (var i = 0; i < Steps.Length; i++)
            {
                var step = Steps[i];
                var change = RelationshipMath.Land(step.Landing, step.Intensity, affection, familiarity);
                RelationshipMath.PushMoods(mood, change, step.Intensity);
                mood.Tick((float)step.Seconds, AfternoonHour);
                affection = change.Affection;
                familiarity = RelationshipMath.FamiliarityAfter(change.Familiarity, step.Seconds);
                var reason = history.Record(change);
                rows.Add(new ScenarioRow
                {
                    Step = i + 1,
                    Landing = step.Landing,
                    Affection = affection,
                    Familiarity = familiarity,
                    Shown = mood.Shown,
                    Reason = reason ?? string.Empty,
                });
            }

            return new ScenarioResult(Name, rows, mood);
        }
    }

    /// <summary>A scenario's trajectory, and the mood state it ended on.</summary>
    public sealed class ScenarioResult
    {
        public readonly string Name;
        public readonly IReadOnlyList<ScenarioRow> Rows;
        public readonly CompanionMoodState Mood;

        public ScenarioResult(string name, IReadOnlyList<ScenarioRow> rows, CompanionMoodState mood)
        {
            Name = name ?? string.Empty;
            Rows = rows;
            Mood = mood;
        }

        public ScenarioRow First
        {
            get { return Rows[0]; }
        }

        public ScenarioRow Last
        {
            get { return Rows[Rows.Count - 1]; }
        }

        /// <summary>True when familiarity never went down from one step to the next.</summary>
        public bool FamiliarityNeverDrops()
        {
            for (var i = 1; i < Rows.Count; i++)
            {
                if (Rows[i].Familiarity < Rows[i - 1].Familiarity)
                    return false;
            }

            return true;
        }

        /// <summary>A plain table, one line per step, for the test log or a tuning session.</summary>
        public string Report()
        {
            var sb = new StringBuilder(64 + Rows.Count * 72);
            sb.Append("Scenario: ").Append(Name).Append('\n');
            sb.Append("step  landed        affection  familiarity  mood          reason\n");
            for (var i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                sb.Append(r.Step.ToString(CultureInfo.InvariantCulture).PadRight(6))
                    .Append(LandingEvents.Name(r.Landing).PadRight(14))
                    .Append(r.Affection.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture).PadRight(11))
                    .Append(r.Familiarity.ToString("0.0", CultureInfo.InvariantCulture).PadRight(13))
                    .Append(r.Shown.ToString().PadRight(14))
                    .Append(r.Reason)
                    .Append('\n');
            }

            return sb.ToString();
        }
    }

    /// <summary>The fixed personas the scenario tests use (JUH-115 expectations).</summary>
    public static class PisaraScenarios
    {
        public static SheetRelationship Stranger()
        {
            return PisaraScenario.Relationship(RelationshipRoles.Stranger, string.Empty, 0f, 0f, false);
        }

        public static SheetRelationship Stepsister()
        {
            return PisaraScenario.Relationship(RelationshipRoles.Family, "stepsister", 25f, 70f, true);
        }

        public static SheetRelationship Partner()
        {
            return PisaraScenario.Relationship(RelationshipRoles.Partner, "girlfriend", 70f, 85f, true);
        }

        public static SheetRelationship Rival()
        {
            return PisaraScenario.Relationship(RelationshipRoles.Rival, "rival", -30f, 30f, true);
        }
    }
}
