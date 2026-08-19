using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services.PlanGeneration;

public class ClaudeOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    /// <summary>Pinned rather than floating: a plan the owner approved should not change shape under them.</summary>
    public string Model { get; set; } = "claude-opus-5";
    public int MaxTokens { get; set; } = 16000;
    /// <summary>The desk is waiting on this call, so it gets a hard ceiling rather than the SDK default.</summary>
    public int TimeoutSeconds { get; set; } = 120;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// The AI half of Module 4.2. Structured outputs do the work that a parser would otherwise
/// have to: the response is constrained to a JSON Schema, so a draft either arrives in the
/// exact shape the editor renders or the call fails loudly and the rule-based generator
/// stands in. Exercise ids are validated against the branch's own library afterwards,
/// because a plan naming equipment the floor does not have is worse than no plan.
/// </summary>
public class ClaudePlanGenerator : IPlanGenerator
{
    private readonly ClaudeOptions _options;
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudePlanGenerator> _log;

    public ClaudePlanGenerator(ClaudeOptions options, ILogger<ClaudePlanGenerator> log)
    {
        _options = options;
        _log = log;
        _client = new AnthropicClient { ApiKey = options.ApiKey ?? string.Empty };
    }

    public string Engine => $"claude:{_options.Model}";
    public bool IsLive => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    // ---------------------------------------------------------------- workout

    public async Task<GeneratedWorkoutPlan> GenerateWorkoutAsync(PlanGenerationContext ctx, CancellationToken ct = default)
    {
        var json = await CompleteAsync(WorkoutSystemPrompt(), WorkoutUserPrompt(ctx), WorkoutSchema(), ct);
        var raw = JsonSerializer.Deserialize<RawWorkoutPlan>(json, JsonOpts)
            ?? throw new InvalidOperationException("Model returned an empty workout plan.");

        var valid = ctx.ExerciseLibrary.ToDictionary(e => e.Id);
        var days = new List<GeneratedWorkoutDay>();

        foreach (var day in raw.Days.OrderBy(d => d.DayIndex))
        {
            var exercises = (day.Exercises ?? new List<RawWorkoutExercise>())
                // Silently dropping an unknown id is the right call: the day still stands, and
                // the admin sees a short day rather than a plan referencing equipment we lack.
                .Where(x => valid.ContainsKey(x.ExerciseId))
                .Select(x => new GeneratedWorkoutExercise
                {
                    ExerciseId = x.ExerciseId,
                    Sets = Math.Clamp(x.Sets, 1, 10),
                    RepScheme = string.IsNullOrWhiteSpace(x.RepScheme) ? "8-12" : x.RepScheme.Trim(),
                    RestSeconds = Math.Clamp(x.RestSeconds <= 0 ? 90 : x.RestSeconds, 15, 300),
                    TargetWeightKg = x.TargetWeightKg is > 0 and < 400 ? x.TargetWeightKg : null,
                    SupersetGroup = string.IsNullOrWhiteSpace(x.SupersetGroup) ? null : x.SupersetGroup.Trim()[..1].ToUpperInvariant(),
                    Notes = Trim(x.Notes, 400)
                })
                .ToList();

            if (!day.IsRestDay && exercises.Count == 0)
                throw new InvalidOperationException($"Day {day.DayIndex} came back with no usable exercises.");

            days.Add(new GeneratedWorkoutDay
            {
                DayIndex = day.DayIndex,
                Title = Trim(day.Title, 120) ?? $"Day {day.DayIndex}",
                Focus = Trim(day.Focus, 120),
                IsRestDay = day.IsRestDay,
                Notes = Trim(day.Notes, 600),
                Exercises = exercises
            });
        }

        if (days.Count == 0) throw new InvalidOperationException("Model returned a plan with no days.");

        return new GeneratedWorkoutPlan
        {
            Name = Trim(raw.Name, 160) ?? "Generated programme",
            Summary = Trim(raw.Summary, 900) ?? string.Empty,
            Days = days
        };
    }

    // ---------------------------------------------------------------- diet

    public async Task<GeneratedDietPlan> GenerateDietAsync(PlanGenerationContext ctx, CancellationToken ct = default)
    {
        var json = await CompleteAsync(DietSystemPrompt(), DietUserPrompt(ctx), DietSchema(), ct);
        var raw = JsonSerializer.Deserialize<RawDietPlan>(json, JsonOpts)
            ?? throw new InvalidOperationException("Model returned an empty diet plan.");

        if (raw.Meals is null || raw.Meals.Count == 0)
            throw new InvalidOperationException("Model returned a diet plan with no meals.");

        // A calorie target wildly off a sane range is a sign the whole answer is unreliable,
        // not something to clamp and ship — let it fall back to the rule-based plate.
        if (raw.TargetCalories is < 1000 or > 5000)
            throw new InvalidOperationException($"Calorie target {raw.TargetCalories} is outside the safe band.");

        return new GeneratedDietPlan
        {
            Name = Trim(raw.Name, 160) ?? "Generated plate",
            Summary = Trim(raw.Summary, 900) ?? string.Empty,
            TargetCalories = raw.TargetCalories,
            ProteinGrams = Math.Clamp(raw.ProteinGrams, 30, 400),
            CarbGrams = Math.Clamp(raw.CarbGrams, 30, 800),
            FatGrams = Math.Clamp(raw.FatGrams, 20, 250),
            Meals = raw.Meals.Select(m => new GeneratedMeal
            {
                Slot = ParseSlot(m.Slot),
                Title = Trim(m.Title, 120) ?? "Meal",
                Items = Trim(m.Items, 600) ?? string.Empty,
                Calories = Math.Max(0, m.Calories),
                ProteinGrams = Math.Max(0, m.ProteinGrams),
                CarbGrams = Math.Max(0, m.CarbGrams),
                FatGrams = Math.Max(0, m.FatGrams),
                TimingHint = Trim(m.TimingHint, 120)
            }).ToList()
        };
    }

    // ---------------------------------------------------------------- transport

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<string> CompleteAsync(
        string system, string user, IDictionary<string, JsonElement> schema, CancellationToken ct)
    {
        if (!IsLive) throw new InvalidOperationException("No Anthropic API key is configured.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            // The library and the member's constraints are the same on every call for a given
            // branch, so the stable half sits in the system block where caching can reach it.
            System = new List<TextBlockParam>
            {
                new() { Text = system, CacheControl = new CacheControlEphemeral() }
            },
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High,
                Format = new JsonOutputFormat { Schema = new Dictionary<string, JsonElement>(schema) }
            },
            Messages = [new() { Role = Role.User, Content = user }]
        }, cancellationToken: timeout.Token);

        if (response.StopReason == "refusal")
            throw new InvalidOperationException(
                $"The model declined the request ({response.StopDetails?.Category ?? "unspecified"}).");

        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(b => b.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("The model returned no text content.");

        _log.LogInformation(
            "Claude plan draft: {Input} in / {Output} out tokens, stop={Stop}",
            response.Usage.InputTokens, response.Usage.OutputTokens, response.StopReason);

        return text;
    }

    // ---------------------------------------------------------------- prompts

    private static string WorkoutSystemPrompt() =>
        """
        You are a strength and conditioning coach writing a training programme for a member of
        an Indian multi-branch gym. A human coach reviews and approves every plan you write
        before the member sees it, so write what you would defend in that review.

        Rules that are not negotiable:
        - Only prescribe exercises from the library given in the request, referenced by their
          numeric id. Never invent an exercise or an id.
        - Respect the stated injuries absolutely. If a movement loads an injured joint, choose
          a different one and say why in that exercise's note.
        - Match the number of days to the member's stated availability. Include rest days
          inside the rotation where the split needs them, and give each rest day an actual
          instruction rather than the word "rest".
        - Target weights are in kilograms and must be rounded to the nearest 2.5 kg. Only
          suggest a weight for a lift the member has a recorded best on; otherwise leave it null.
        - Superset groups are a single capital letter shared by the exercises performed together.

        Write the summary for the member, not for the coach: one short paragraph saying what
        this block is trying to achieve and what will feel hard about it.
        """;

    private static string WorkoutUserPrompt(PlanGenerationContext ctx)
    {
        var library = string.Join("\n", ctx.ExerciseLibrary.Select(e =>
            $"  {e.Id}\t{e.Name} · {e.PrimaryMuscle} · {e.Equipment} · {e.Level}{(e.IsStrengthTracked ? " · strength-tracked" : string.Empty)}"));

        var bests = ctx.RecentBests.Count == 0
            ? "  (no logged lifts yet)"
            : string.Join("\n", ctx.RecentBests.Select(b =>
                $"  {b.ExerciseName}: best e1RM {b.BestEstimatedOneRepMax:0.#} kg, last performed {b.LastPerformedOn:dd MMM yyyy}"));

        return $"""
        MEMBER
          Goal: {ctx.Goal}
          Experience level: {ctx.Level}
          Age: {ctx.Age?.ToString() ?? "not recorded"} · Sex: {ctx.Gender}
          Height: {Fmt(ctx.HeightCm)} cm · Weight: {Fmt(ctx.WeightKg)} kg · Body fat: {Fmt(ctx.BodyFatPercent)}%
          Training sessions in the last 30 days: {ctx.SessionsLast30Days}
          Injuries: {Blank(ctx.InjuryNotes)}
          Medical notes: {Blank(ctx.MedicalNotes)}
          Coach note: {Blank(ctx.TrainerNote)}

        PROGRAMME
          Days per week: {ctx.DaysPerWeek}
          Block length: {ctx.DurationWeeks} weeks
          Equipment available at this branch: {string.Join(", ", ctx.AvailableEquipment)}

        RECENT BESTS
        {bests}

        EXERCISE LIBRARY (id, name, muscle, equipment, level)
        {library}

        Write the programme.
        """;
    }

    private static string DietSystemPrompt() =>
        """
        You are a sports nutritionist writing a daily eating plan for a member of an Indian
        gym. A human coach approves every plan before the member sees it.

        Rules that are not negotiable:
        - Meals must be Indian household food, described in portions a person can actually
          shop for and cook: "2 roti + 1 katori rajma + 100g paneer bhurji", not "protein
          source + complex carbohydrate".
        - If the member is vegetarian, no meal may contain meat, fish or eggs.
        - Per-meal calories and macros must add up to roughly the daily targets you set.
        - Never prescribe supplements beyond whey protein, creatine and a multivitamin, and
          never give medical advice. If the medical notes suggest a clinical condition, say in
          the summary that a doctor should review the plan.
        - Use the slot names given in the schema, in the order the member would eat them.

        Write the summary for the member: what the plate is doing and the one habit that
        matters most for it to work.
        """;

    private static string DietUserPrompt(PlanGenerationContext ctx) => $"""
        MEMBER
          Goal: {ctx.Goal}
          Age: {ctx.Age?.ToString() ?? "not recorded"} · Sex: {ctx.Gender}
          Height: {Fmt(ctx.HeightCm)} cm · Weight: {Fmt(ctx.WeightKg)} kg · Body fat: {Fmt(ctx.BodyFatPercent)}%
          Training sessions in the last 30 days: {ctx.SessionsLast30Days} across {ctx.DaysPerWeek} days a week
          Vegetarian: {(ctx.IsVegetarian ? "yes" : "no")}
          Medical notes: {Blank(ctx.MedicalNotes)}
          Coach note: {Blank(ctx.TrainerNote)}

        Write one day of eating that the member can repeat, with the daily macro targets it hits.
        """;

    private static string Fmt(decimal? value) => value?.ToString("0.#") ?? "not recorded";
    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? "none recorded" : value.Trim();

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static MealSlot ParseSlot(string? slot) =>
        Enum.TryParse<MealSlot>(slot?.Replace("-", string.Empty).Replace(" ", string.Empty), true, out var parsed)
            ? parsed
            : MealSlot.Snack;

    // ---------------------------------------------------------------- schemas

    private static Dictionary<string, JsonElement> Schema(object shape) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(shape))!;

    private static Dictionary<string, JsonElement> WorkoutSchema() => Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "name", "summary", "days" },
        properties = new
        {
            name = new { type = "string", description = "Short programme name, e.g. 'Foundation Strength · 4-day'." },
            summary = new { type = "string", description = "One paragraph, written to the member." },
            days = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "dayIndex", "title", "isRestDay", "exercises" },
                    properties = new
                    {
                        dayIndex = new { type = "integer", description = "1-based position in the weekly rotation." },
                        title = new { type = "string" },
                        focus = new { type = new[] { "string", "null" } },
                        isRestDay = new { type = "boolean" },
                        notes = new { type = new[] { "string", "null" } },
                        exercises = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "exerciseId", "sets", "repScheme", "restSeconds" },
                                properties = new
                                {
                                    exerciseId = new { type = "integer", description = "Must be an id from the supplied library." },
                                    sets = new { type = "integer" },
                                    repScheme = new { type = "string", description = "e.g. '5', '8-12', '30s'." },
                                    restSeconds = new { type = "integer" },
                                    targetWeightKg = new { type = new[] { "number", "null" }, description = "Nearest 2.5 kg, or null." },
                                    supersetGroup = new { type = new[] { "string", "null" }, description = "Single capital letter, or null." },
                                    notes = new { type = new[] { "string", "null" } }
                                }
                            }
                        }
                    }
                }
            }
        }
    });

    private static Dictionary<string, JsonElement> DietSchema() => Schema(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "name", "summary", "targetCalories", "proteinGrams", "carbGrams", "fatGrams", "meals" },
        properties = new
        {
            name = new { type = "string" },
            summary = new { type = "string" },
            targetCalories = new { type = "integer" },
            proteinGrams = new { type = "integer" },
            carbGrams = new { type = "integer" },
            fatGrams = new { type = "integer" },
            meals = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "slot", "title", "items", "calories", "proteinGrams", "carbGrams", "fatGrams" },
                    properties = new
                    {
                        slot = new
                        {
                            type = "string",
                            @enum = new[] { "Breakfast", "MidMorning", "Lunch", "PreWorkout", "PostWorkout", "Snack", "Dinner" }
                        },
                        title = new { type = "string" },
                        items = new { type = "string", description = "Indian household portions." },
                        calories = new { type = "integer" },
                        proteinGrams = new { type = "integer" },
                        carbGrams = new { type = "integer" },
                        fatGrams = new { type = "integer" },
                        timingHint = new { type = new[] { "string", "null" } }
                    }
                }
            }
        }
    });

    // ---------------------------------------------------------------- wire shapes

    private sealed record RawWorkoutPlan(string? Name, string? Summary, List<RawWorkoutDay> Days);

    private sealed record RawWorkoutDay(
        int DayIndex, string? Title, string? Focus, bool IsRestDay, string? Notes, List<RawWorkoutExercise>? Exercises);

    private sealed record RawWorkoutExercise(
        int ExerciseId, int Sets, string? RepScheme, int RestSeconds, decimal? TargetWeightKg,
        string? SupersetGroup, string? Notes);

    private sealed record RawDietPlan(
        string? Name, string? Summary, int TargetCalories, int ProteinGrams, int CarbGrams, int FatGrams,
        List<RawMeal>? Meals);

    private sealed record RawMeal(
        string? Slot, string? Title, string? Items, int Calories, int ProteinGrams, int CarbGrams,
        int FatGrams, string? TimingHint);
}
