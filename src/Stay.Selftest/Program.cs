// Runs the engine's checks on any OS, against a PC that does not exist. The app runs the same suites on Windows
// with "stay selftest".
using Stay.Core;
return SelfTest.Run(Console.Out, args.Length > 0 ? args[0] : null) == 0 ? 0 : 1;
