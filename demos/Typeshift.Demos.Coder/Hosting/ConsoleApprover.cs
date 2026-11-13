using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder.Hosting;

// Turns the decisions a response waits for into answers, asking at the console: a plan is approved into auto mode,
// into the default mode or rejected with feedback, a tool call approved once, for the conversation, or rejected
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
            }
        }

        return answers;
    }
}
