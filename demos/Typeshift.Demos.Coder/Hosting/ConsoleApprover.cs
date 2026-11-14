using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder.Hosting;

// Turns the decisions a response waits for into answers, asking at the console: a plan is approved into auto mode,
// into the default mode or rejected with feedback, a tool call approved once, for the conversation, or rejected,
// and a question answered by an option's number or in the user's own words
public static class ConsoleApprover
{
    public static IReadOnlyList<DecisionAnswer> Answers(ChatResponse response, Func<string> readLine, bool approveAll)
    {
        var answers = new List<DecisionAnswer>();

        foreach (var decision in response.Decisions)
        {
            switch (decision)
            {
                case PlanDecision plan:
                    Console.WriteLine();
                    Console.WriteLine("---- The plan ----");
                    Console.WriteLine(plan.Plan);
                    Console.WriteLine("------------------");
                    Console.Write("Approve the plan? [a]uto mode, [d]efault mode (ask per tool), or type feedback: ");
                    var feedback = approveAll ? "a" : readLine();

                    answers.Add(feedback switch
                    {
                        "a" or "" => plan.Approve(AgentMode.Auto),
                        "d" => plan.Approve(AgentMode.Default),
                        _ => plan.Reject(feedback)
                    });

                    break;

                case ApprovalDecision call:
                    var arguments = Demo.Arguments(call.Arguments);
                    Console.Write($"Run {call.Tool}({arguments})? [y]es, [a]lways for this conversation, [n]o: ");
                    var reply = approveAll ? "y" : readLine();

                    answers.Add(reply switch
                    {
                        "a" => call.ApproveForConversation(),
                        "y" or "" => call.Approve(),
                        _ => call.Reject("The user did not allow this call.")
                    });

                    break;

                case QuestionDecision asked:
                    var replies = asked.Questions.Select(question => Ask(question, readLine, approveAll));
                    answers.Add(asked.Answer([.. replies]));
                    break;
            }
        }

        return answers;
    }

    // One question with its options by number. A reply that is no number of an option is the user's own answer
    private static QuestionAnswer Ask(Question question, Func<string> readLine, bool pickFirst)
    {
        Console.WriteLine();
        Console.WriteLine($"{question.Header}: {question.Text}");

        foreach (var (option, index) in question.Options.Select((option, index) => (option, index)))
            Console.WriteLine($"  {index + 1}. {option.Label}: {option.Description}");

        Console.Write(question.MultiSelect ? "Numbers such as 1,3, or your own answer: " : "A number, or your own answer: ");
        var reply = pickFirst ? "1" : readLine();

        var picked = reply
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .Select(number => question.Options.ElementAtOrDefault(number - 1)?.Label)
            .ToList();

        var numbers = picked.Count > 0 && picked.All(label => label is not null);

        return numbers && (question.MultiSelect || picked.Count == 1) ? new QuestionAnswer([.. picked.OfType<string>()])
            : reply.Length > 0 ? reply
            : "No preference.";
    }
}
