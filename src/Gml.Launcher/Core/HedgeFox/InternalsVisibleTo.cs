using System.Runtime.CompilerServices;

// The screenshot harness (tools/shots) and the unit tests (tests/HedgeFox.Launcher.Tests) build pages whose
// constructors are internal. Neither ships with the launcher.
[assembly: InternalsVisibleTo("HedgeFox.Shots")]
[assembly: InternalsVisibleTo("HedgeFox.Launcher.Tests")]
