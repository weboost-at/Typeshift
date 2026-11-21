using System.Text.Json;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

// D82: the tool an agent offers with UseQuestions(). Its call always waits for your app, and the app's answers are
// its result, so the function itself never runs
internal sealed class AskUserFunction : AIFunction
{
    public const string ToolName = "ask_user";

    public const int MaxQuestions = 4;

    public const int MaxOptions = 4;

    public static readonly AskUserFunction Instance = new();

    // What the model reads when it calls the tool where no user can be asked: as a sub-agent, in a run without a
    // conversation, or from an agent that does not offer it
    public const string NotAvailable =
        "ask_user is not available here: go on without asking, and say in your answer what you need to know.";

    private const string Declined = "The user declined to answer.";

    // The descriptions the model reads, free of characters JSON escapes
    private const string QuestionsText = "The questions, one to four. Ask what belongs together in one call.";

    private const string QuestionText =
        "The full question, specific enough to answer on its own, ending in a question mark.";

    private const string HeaderText =
        "A label of one or two words that names the question, such as Library. The answer comes back under it.";

    private const string OptionsText =
        "The choices, two to four, each different from the others. The user can always answer in their own words, "
        + "so add no option such as Other.";

    private const string LabelText = "The choice as the user sees it, in one to five words.";

    private const string OptionText = "What the choice means, or what follows from picking it.";

    private const string MultiSelectText =
        "True when the user may pick several options. Leave it out for a single choice.";

    private static readonly JsonElement Schema = JsonElement.Parse(
        $$"""
        {
          "type": "object",
          "properties": {
            "questions": {
              "type": "array",
              "description": "{{QuestionsText}}",
              "minItems": 1,
              "maxItems": {{MaxQuestions}},
              "items": {
                "type": "object",
                "properties": {
                  "question": { "type": "string", "description": "{{QuestionText}}" },
                  "header": { "type": "string", "description": "{{HeaderText}}" },
                  "options": {
                    "type": "array",
                    "description": "{{OptionsText}}",
                    "minItems": 2,
                    "maxItems": {{MaxOptions}},
                    "items": {
                      "type": "object",
                      "properties": {
                        "label": { "type": "string", "description": "{{LabelText}}" },
                        "description": { "type": "string", "description": "{{OptionText}}" }
                      },
                      "required": ["label", "description"]
                    }
                  },
                  "multiSelect": { "type": "boolean", "description": "{{MultiSelectText}}" }
                },
                "required": ["question", "header", "options"]
              }
            }
          },
          "required": ["questions"]
        }
        """);

    private AskUserFunction()
    {
    }

    public override string Name => ToolName;

    public override string Description =>
        "Asks the user one to four questions and ends your turn until they answer. Use it when the task depends on "
        + "a choice or a fact only the user has, such as an unclear requirement or approaches that lead to different "
        + "work. Do not ask what your tools can find out or what you can decide yourself. Do not use it to ask for "
        + "approval of a plan or of a tool call: approval is asked for separately, without this tool. The user "
        + "picks among each question's options or answers in their own words, and may decline to answer.";

    public override JsonElement JsonSchema => Schema;

    // D81: the tool loop's invoker answers the call from the decision's record and never gets here
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken ct) =>
        throw new InvalidOperationException("ask_user is answered by your app's decision, never invoked.");

    // What breaks the shape the schema declares, as the model reads it in the call's place; null for a call that asks
    public static string? Check(JsonElement arguments)
    {
        ReadQuestions(StorableJson.Member(arguments, "questions"), "question", out var wrong);

        return wrong is null
            ? null
            : $"The user was not asked: {wrong} Correct the arguments and call ask_user again.";
    }

    public static IReadOnlyList<Question> Questions(JsonElement arguments) =>
        ReadQuestions(StorableJson.Member(arguments, "questions"), "question", out _);

    // The questions of a JSON array and the first thing that breaks their shape. The tool's arguments name a
    // question's text "question", a decision's JSON "text"
    public static IReadOnlyList<Question> ReadQuestions(JsonElement list, string text, out string? wrong)
    {
        wrong = null;
        var questions = new List<Question>();

        if (list.ValueKind is not JsonValueKind.Array)
        {
            wrong = "questions must be an array of one to four questions.";
            return questions;
        }

        foreach (var (element, index) in list.EnumerateArray().Select((element, index) => (element, index)))
        {
            var at = $"questions[{index}]";

            if (element.ValueKind is not JsonValueKind.Object)
            {
                wrong ??= $"{at} must be an object.";
                continue;
            }

            var options = ReadOptions(StorableJson.Member(element, "options"), at, ref wrong);
            var multiSelect = StorableJson.Member(element, "multiSelect");

            if (multiSelect.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.True or JsonValueKind.False))
                wrong ??= $"{at}.multiSelect must be true or false.";

            questions.Add(new Question(
                Required(element, "header", at, ref wrong),
                Required(element, text, at, ref wrong),
                options,
                multiSelect.ValueKind is JsonValueKind.True));
        }

        if (questions.Count is < 1 or > MaxQuestions)
            wrong ??= $"questions must hold one to four questions, and it holds {questions.Count}.";

        return questions;
    }

    // The answers as they are recorded: one for each question, each the labels its question offers or the user's
    // own words. Words that are a label pick that option
    public static IReadOnlyList<QuestionAnswer> Normalize(
        IReadOnlyList<Question> questions,
        IReadOnlyList<QuestionAnswer> answers,
        string decision)
    {
        if (answers.Count != questions.Count)
        {
            throw new ArgumentException(
                $"The answer to {decision} holds {answers.Count} answers for {questions.Count} questions: pass one "
                + "answer for each question, in the questions' order.",
                nameof(answers));
        }

        return [.. questions.Zip(answers, (question, answer) => Normalize(question, answer, decision))];
    }

    // The call's result: each answer under its question's header, as the model asked
    public static string Result(IReadOnlyList<Question> questions, DecisionAnswer? answer)
    {
        if (answer?.Answers is not { } answers)
            return answer?.Reason is { } reason ? $"{Declined} {reason}" : Declined;

        return StorableJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("answers");

            foreach (var (question, given) in questions.Zip(answers))
            {
                writer.WriteStartObject();
                writer.WriteString("header", question.Header);

                if (given.Selected.Count > 0)
                {
                    writer.WriteStartArray("selected");

                    foreach (var label in given.Selected)
                    {
                        writer.WriteStringValue(label);
                    }

                    writer.WriteEndArray();
                }

                if (given.Text is not null) writer.WriteString("text", given.Text);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static List<QuestionOption> ReadOptions(JsonElement list, string question, ref string? wrong)
    {
        var options = new List<QuestionOption>();

        if (list.ValueKind is not JsonValueKind.Array)
        {
            wrong ??= $"{question}.options must be an array of two to four options.";
            return options;
        }

        foreach (var (element, index) in list.EnumerateArray().Select((element, index) => (element, index)))
        {
            var at = $"{question}.options[{index}]";

            if (element.ValueKind is not JsonValueKind.Object)
            {
                wrong ??= $"{at} must be an object.";
                continue;
            }

            options.Add(new QuestionOption(
                Required(element, "label", at, ref wrong),
                Required(element, "description", at, ref wrong)));
        }

        if (options.Count is < 2 or > MaxOptions)
            wrong ??= $"{question}.options must hold two to four options, and it holds {options.Count}.";

        if (options.GroupBy(option => option.Label).FirstOrDefault(labels => labels.Count() > 1) is { Key: var twice })
            wrong ??= $"{question}.options holds the label '{twice}' twice, and the user answers by label.";

        return options;
    }

    private static string Required(JsonElement element, string member, string at, ref string? wrong)
    {
        if (StorableJson.TryRead(StorableJson.Member(element, member), out string text)
            && !string.IsNullOrWhiteSpace(text))
            return text;

        wrong ??= $"{at}.{member} must be a text that is not empty.";
        return "";
    }

    private static QuestionAnswer Normalize(Question question, QuestionAnswer answer, string decision)
    {
        var selected = answer.Selected.Distinct().ToList();
        var text = string.IsNullOrWhiteSpace(answer.Text) ? null : answer.Text;

        if (selected.Find(label => question.Options.All(option => option.Label != label)) is { } unknown)
        {
            var offered = string.Join(", ", question.Options.Select(option => $"'{option.Label}'"));

            throw new ArgumentException(
                $"The answer to {decision} selects '{unknown}', which the question '{question.Header}' does not "
                + $"offer: select among {offered}, or answer with a text.",
                "answers");
        }

        if (selected.Count > 1 && !question.MultiSelect)
        {
            throw new ArgumentException(
                $"The answer to {decision} selects {selected.Count} options of the question '{question.Header}', "
                + "which takes one: only a question with MultiSelect takes several.",
                "answers");
        }

        if (selected.Count == 0 && text is null)
        {
            throw new ArgumentException(
                $"The answer to {decision} leaves the question '{question.Header}' unanswered: select an option, "
                + "answer with a text, or decline every question with Reject().",
                "answers");
        }

        return selected.Count == 0 && question.Options.Any(option => option.Label == text)
            ? new([text!])
            : new(selected, text);
    }
}
