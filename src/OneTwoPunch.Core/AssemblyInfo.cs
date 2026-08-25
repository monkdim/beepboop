using System.Runtime.CompilerServices;

// The engine's internals are open to its own tests. RotationContext in particular has an
// internal constructor - it is built by RotationSession, not by callers - but the readiness
// rules it holds are worth testing directly rather than only through a whole resolve.
[assembly: InternalsVisibleTo("OneTwoPunch.Core.Tests")]
