using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder.Hosting;

// Turns a response's approval requests into the answers of the next run, asking at the console: a plan is
// approved into auto mode or rejected with feedback, a tool call approved once, for the conversation, or rejected
public static class ConsoleApprover
{
    public static ChatMessage? Answer(ChatResponse response, Func<string> readLine, bool approveAll)
    {
        if (response.ApprovalRequests.Count == 0) return null;

        var answers = new List<AIContent>();

        foreach (var request in response.ApprovalRequests)
        {
            if (request.ToolCall is not FunctionCallContent call) continue;

            if (call.Name == "exit_plan_mode")
            {
                Console.WriteLine();
                Console.WriteLine("---- The plan ----");
                Console.WriteLine(call.Arguments?["plan"]?.ToString());
                Console.WriteLine("------------------");
                Console.Write("Approve the plan? [a]uto mode, [d]efault mode (ask per tool), or type feedback: ");
                var answer = approveAll ? "a" : readLine();

                answers.Add(answer switch
                {
                    "a" or "" => request.ApprovePlan(AgentMode.Auto),
                    "d" => request.ApprovePlan(AgentMode.Default),
                    _ => request.CreateResponse(approved: false, answer)
                });

                continue;
            }

            Console.Write($"Run {call.Name}({Demo.Arguments(call)})? [y]es, [a]lways for this conversation, [n]o: ");
            var decision = approveAll ? "y" : readLine();

            answers.Add(decision switch
            {
                "a" => request.ApproveForConversation(),
                "y" or "" => request.CreateResponse(approved: true),
                _ => request.CreateResponse(approved: false, "The user did not allow this call.")
            });
        }

        return new ChatMessage(ChatRole.User, answers);
    }
}
