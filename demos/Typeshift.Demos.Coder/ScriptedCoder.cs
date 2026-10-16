using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder;

// Two canned models for runs without a network: a coder that plans, asks the explorer, edits greet.sh and runs the
// tests, and an explorer that answers one question from the files. Each decides its next step from what it did so far
internal static class ScriptedCoder
{
    private const string Plan =
        """
        1. Read greet.sh and test.sh to see what the tests expect.
        2. Add the missing exclamation mark to the greeting in greet.sh.
        3. Run ./test.sh and confirm both tests pass.
        """;

    public static ScriptedChatClient Coder() => new(Next);

    public static ScriptedChatClient Explorer() => new(ExploreNext);

    private static ChatMessage Next(ScriptedCall call)
    {
        // Plan mode: a to-do list, a look at the files, then the plan
        if (call.Offers("exit_plan_mode"))
        {
            if (call.CallsOf("write_todos") == 0)
            {
                return ScriptedCall.Call("write_todos", ("todos", new[]
                {
                    new { title = "Read greet.sh and test.sh", status = "InProgress" },
                    new { title = "Fix the greeting", status = "Pending" },
                    new { title = "Run the tests", status = "Pending" }
                }));
            }

            if (call.CallsOf("read_file") == 0) return ScriptedCall.Call("read_file", ("path", "test.sh"));
            if (call.CallsOf("read_file") == 1) return ScriptedCall.Call("read_file", ("path", "greet.sh"));

            return ScriptedCall.Call("exit_plan_mode", ("plan", Plan));
        }

        // The explorer's answer arrives as a message once the work is done: acknowledge it and stop
        if (call.CallsOf("write_todos") >= 2 && call.Last.Role == ChatRole.User)
            return ScriptedCall.Text("The explorer confirmed where the greeting is printed; nothing else to do.");

        // The plan was approved: ask the explorer in the background, fix the file, run the tests, report
        if (call.CallsOf("explorer_agent") == 0)
            return ScriptedCall.Call("explorer_agent", ("input", "Which line of which file prints the greeting?"));

        if (call.CallsOf("edit_file") == 0)
            return ScriptedCall.Call("edit_file", ("path", "greet.sh"), ("oldText", "echo \"Hello, $name\""), ("newText", "echo \"Hello, $name!\""));

        if (call.CallsOf("run_command") == 0) return ScriptedCall.Call("run_command", ("command", "sh test.sh"));

        var tests = call.LastResultOf("run_command") ?? "";
        var passed = tests.Contains("passed", StringComparison.Ordinal);

        if (call.CallsOf("write_todos") == 1)
        {
            return ScriptedCall.Call("write_todos", ("todos", new[]
            {
                new { title = "Read greet.sh and test.sh", status = "Completed" },
                new { title = "Fix the greeting", status = "Completed" },
                new { title = "Run the tests", status = passed ? "Completed" : "InProgress" }
            }));
        }

        return ScriptedCall.Text(passed
            ? "Done: greet.sh now ends the greeting with an exclamation mark, and both tests pass."
            : $"The tests still fail: {tests}");
    }

    private static ChatMessage ExploreNext(ScriptedCall call) =>
        call.CallsOf("search_files") == 0
            ? ScriptedCall.Call("search_files", ("pattern", "echo"))
            : ScriptedCall.Text($"The greeting is printed here: {call.LastResultOf("search_files")}");
}
