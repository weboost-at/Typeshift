namespace Typeshift.Modules;

// Marks a module as registered, so a second AddTypeshift() call on one container adds nothing twice
internal sealed record RegisteredModule(Type Module);
