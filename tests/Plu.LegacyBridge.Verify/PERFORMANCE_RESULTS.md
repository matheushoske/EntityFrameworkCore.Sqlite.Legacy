# Legacy Bridge Performance Results

Environment: local Windows runner, Release build, real encrypted database at `c:\teste_plu\plu.db3`.

## Baseline (before session pooling)

Command:

```powershell
dotnet run --project tests/Plu.LegacyBridge.Verify/Plu.LegacyBridge.Verify.csproj -c Release --no-build
```

Result:

- Exit code: `0`
- Total E2E runtime: `74,144 ms`
- Observed per-open cost in ADO/EF path: about `1,400 ms`
- Behavior: repeated host process startup and shutdown across EF operations

## After session pooling and startup cleanup

Changes measured:

- Reuse `LegacyBridgeProcessSession` through an ADO-level pool
- Return healthy sessions to pool on `DbConnection.Close()`
- Clear all pooled hosts at verifier shutdown
- Remove fixed `1,000 ms` startup delay before pipe connect
- Move hot-path bridge startup logs from console to debug output

Results:

- Post-pool E2E run 1: `20,463 ms`
- Post-pool E2E run 2: `12,268 ms`
- Final run with unit check: `14,581 ms`
- Pool smoke sample: first EF open `28 ms`, second EF open `12 ms`

## Comparison

Using baseline `74,144 ms` and final unit+E2E run `14,581 ms`:

- Absolute reduction: `59,563 ms`
- Relative reduction: about `80.3%`

The largest win comes from avoiding repeated process creation, SQLite native initialization, pipe handshake, and connection open for every EF operation.
