using NUnit.Framework;

// Every test, including each scenario and Examples row, may run in parallel (needs Reqnroll 2.2+).
[assembly: Parallelizable(ParallelScope.Children)]

// A fresh fixture instance per test, so [SetUp] fields aren't shared between parallel tests.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
