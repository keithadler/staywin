# Contributing

Bug reports and pull requests are welcome. Keep to the shape:

- One switch is one thing, with three sentences a person can act on: what it is, what Stay changes, what you lose. No jargon in the window; the command line may be terse.
- Every registry value a switch touches goes in the catalog with its exact path and value, so the receipt can put it back. No scripts, no `reg.exe`, no services or scheduled tasks.
- Every data source stays behind an interface (`IRegistry`, `IPackages`, `IReceiptStore`) so the self-tests run on fakes and never touch a real PC. `dotnet run --project src/Stay.Selftest` must end in `0 failed` before a pull request.
- No network. Ever.
