using System.Text.Json;
using System.Text.Json.Serialization;
using Typeshift.Generated;

namespace Typeshift;

/// <summary>
/// The questions a run asks the user with <c>ask_user</c>, the tool an agent offers through <c>UseQuestions()</c>.
/// Answer it with one <see cref="QuestionAnswer"/> for each question, or decline it with <c>Reject</c>.
/// </summary>
[JsonConverter(typeof(StorableJsonConverter<QuestionDecision>))]
public sealed record QuestionDecision : Decision, IJsonStorable<QuestionDecision>
{
    internal const string KindName = "question";

    internal QuestionDecision(string id, string agent, DateTimeOffset askedAt, IReadOnlyList<Question> questions)
        : base(id, agent, askedAt) =>
        Questions = questions;

    /// <summary>
    /// The questions, one to four, in the order the model asked them.
    /// </summary>
    public IReadOnlyList<Question> Questions { get; }

    private protected override string Kind => KindName;

    /// <summary>
    /// Answers the questions. The model reads the answers as the call's result and goes on.
    /// </summary>
    /// <param name="answers">
    /// One answer for each question, in the questions' order. A string is an answer too: an option when it equals
    /// the option's label, the user's own words otherwise.
    /// </param>
    /// <returns>The answer, for <c>AnswerAsync</c> or <c>RecordAnswersAsync</c>.</returns>
    public DecisionAnswer Answer(params IReadOnlyList<QuestionAnswer> answers) => DecisionAnswer.Answer(Id, answers);

    void IJsonStorable<QuestionDecision>.WriteJson(Utf8JsonWriter writer) => Write(writer);

    static bool IJsonStorable<QuestionDecision>.TryReadJson(JsonElement element, out QuestionDecision value)
    {
        value = element.ValueKind is JsonValueKind.Object ? Read(element) : null!;
        return value is not null;
    }

    internal static QuestionDecision Read(JsonElement element) =>
        new(
            Text(element, "id"),
            Text(element, "agent"),
            Time(element, "askedAt"),
            AskUserFunction.ReadQuestions(StorableJson.Member(element, "questions"), "text", out _));

    private protected override void WriteMembers(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("questions");

        foreach (var question in Questions)
        {
            writer.WriteStartObject();
            writer.WriteString("header", question.Header);
            writer.WriteString("text", question.Text);
            writer.WriteStartArray("options");

            foreach (var option in question.Options)
            {
                writer.WriteStartObject();
                writer.WriteString("label", option.Label);
                writer.WriteString("description", option.Description);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("multiSelect", question.MultiSelect);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// One question of a <see cref="QuestionDecision"/>. The user picks among its options or answers in their own words.
/// </summary>
/// <param name="Header">A short label for the question, which names it in a compact UI and in the answers.</param>
/// <param name="Text">The question.</param>
/// <param name="Options">The options to pick from, two to four.</param>
/// <param name="MultiSelect">Whether the user may pick more than one option.</param>
public sealed record Question(string Header, string Text, IReadOnlyList<QuestionOption> Options, bool MultiSelect);

/// <summary>
/// One option of a <see cref="Question"/>.
/// </summary>
/// <param name="Label">The option as the user sees it. An answer selects the option by this label.</param>
/// <param name="Description">What the option means, or what follows from picking it.</param>
public sealed record QuestionOption(string Label, string Description);

/// <summary>
/// The user's answer to one <see cref="Question"/>: the labels of the options they picked, their own words, or both.
/// A string converts to an answer: it picks the option whose label it equals, and is the user's own words otherwise.
/// </summary>
/// <param name="Selected">The labels of the picked options; empty when the user answered in their own words.</param>
/// <param name="Text">The user's own words; null for none.</param>
public sealed record QuestionAnswer(IReadOnlyList<string> Selected, string? Text = null)
{
    /// <summary>
    /// The labels of the picked options; empty when the user answered in their own words.
    /// </summary>
    // JSON that leaves the member out binds as null
    public IReadOnlyList<string> Selected { get; init; } = Selected ?? [];

    /// <summary>
    /// An answer of one string: the option whose label it equals, or the user's own words.
    /// </summary>
    /// <param name="answer">A label of the question, or a text.</param>
    public static implicit operator QuestionAnswer(string answer) => new([], answer);

    /// <summary>
    /// Whether two answers pick the same options in the same order and carry the same words.
    /// </summary>
    /// <param name="other">The other answer.</param>
    /// <returns>True when they are the same answer.</returns>
    public bool Equals(QuestionAnswer? other) =>
        other is not null && Text == other.Text && Selected.SequenceEqual(other.Selected);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Text);

        foreach (var label in Selected)
        {
            hash.Add(label);
        }

        return hash.ToHashCode();
    }
}
