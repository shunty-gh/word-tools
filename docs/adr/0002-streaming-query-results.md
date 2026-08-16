# Queries return IAsyncEnumerable rather than a materialised list

Engine queries return `IAsyncEnumerable<Match>` with a `CancellationToken`, not
`Task<IReadOnlyList<Match>>`. Most queries return few enough matches that a list would
be fine; composition is the exception, and it can produce thousands of matches from a
search the user may well want to abandon part-way. Streaming lets a caller display
matches as they arrive, stop early, and impose its own limit — so the result cap
becomes the caller's policy rather than something the engine hard-codes.

## Consequences

Streaming is also what keeps a future Blazor WebAssembly front end responsive. The
.NET runtime on WASM is effectively single-threaded, so a CPU-bound search that runs
to completion before returning will freeze the UI — and marking such a method `async`
changes nothing on its own, because nothing ever yields. Because the search is
enumerated in chunks, it can `await Task.Yield()` between them and hand control back to
the browser event loop. A method returning a completed list has no such opportunity.

This is the one place where the engine must actively cooperate with its hosts: the
chunked yield is not an optimisation and should not be removed as one.

## Update: the web app is API-backed

The planned web front end calls a service rather than running the engine in WebAssembly, so
the single-threaded-host argument above no longer applies to it. The decision stands on its
original grounds — a composition can produce thousands of matches from a search the user may
abandon, and streaming is what makes early exit and cancellation possible. The chunked yield
is now load-bearing for **cancellation** rather than for browser responsiveness; it is what
makes Ctrl-C interrupt a long composition promptly.
