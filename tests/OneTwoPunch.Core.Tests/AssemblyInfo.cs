using Xunit;

// The action tables are static by design - one process, verified once at load. That makes
// the verifier tests, which deliberately rebind ids, unsafe to run alongside anything else.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
