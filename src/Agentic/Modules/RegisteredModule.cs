namespace Agentic.Modules;

// Marks a module as registered, so a second AddAgentic() call on one container adds nothing twice
internal sealed record RegisteredModule(Type Module);
