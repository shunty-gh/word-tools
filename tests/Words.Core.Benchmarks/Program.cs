using BenchmarkDotNet.Running;

// Benchmarks are added in phase 7, alongside the performance targets in docs/plan-cli.md.
// Run with: dotnet run -c Release --project tests/Words.Core.Benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
