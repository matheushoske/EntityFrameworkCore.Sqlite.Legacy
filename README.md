# EntityFrameworkCore.Sqlite.Legacy

Access legacy encrypted SQLite databases from modern EF Core applications without rewriting your data layer.

This repository exists for teams modernizing enterprise systems where the database format and encryption stack cannot be changed quickly. It bridges modern .NET application code to a legacy-compatible SQLite runtime.

## Why this project was created

Many production legacy systems still rely on:

- encrypted SQLite files (`.db3`, `.sqlite`)
- native provider behavior from `System.Data.SQLite`
- encryption combinations that are incompatible with plain `Microsoft.Data.Sqlite` usage

In those systems, full migration is often high-risk and expensive.  
This project was created to let teams:

- keep old encrypted files running
- move business logic to EF Core
- adopt clean architecture/repositories incrementally
- avoid "big bang" database migrations

## What gets published

Two NuGet packages, one architecture:

- `EntityFrameworkCore.Sqlite.Legacy` -> `net10.0` + EF Core 10
- `EntityFrameworkCore.Sqlite.Legacy.Ef31` -> `netstandard2.0` + EF Core 3.1

## Encryption compatibility (legacy focus)

The bridge host is built to work with the same legacy provider family (`System.Data.SQLite`) used in old desktop stacks.

Common real-world scenarios supported by this architecture:

- RC4-based legacy SQLite setups
- RSA-backed encryption workflows used by historical commercial distributions
- password-protected databases opened by provider-specific connection behavior

> Important: exact behavior always depends on the native SQLite/provider build shipped with the host release and the encryption profile used when the database was created.

## High-level architecture

`Application` -> `EF Core` -> `UseSqliteLegacy(...)` -> `ADO Bridge` -> `Named Pipes` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite` -> `Encrypted DB`

The host process isolates legacy provider/native dependencies so the main application can stay modern.

## Installation

Choose package by target/runtime strategy:

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy
```

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy.Ef31
```

## Quick start

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

services.AddDbContext<MyDbContext>(options =>
{
    options.UseSqliteLegacy(o =>
    {
        o.DatabasePath(@"C:\pdv\data\plu.db3");
        o.Password("R@enil2015#");
    });
});
```

## Automatic host setup (recommended)

Call during startup:

```csharp
SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

Behavior:

1. Checks if host files already exist in `legacy/`
2. Downloads latest host zip if missing
3. Extracts host binaries to runtime folder

Default host source:

- `https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip`

Host repository:

- [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)

## Advanced usage

```csharp
optionsBuilder.UseSqliteLegacy(o =>
{
    o.DatabasePath(@"C:\legacy\cfe.db3");
    o.Password("your-password");
    o.HostExecutablePath(@"C:\app\legacy\Sqlite.LegacyBridge.Host.exe");
}, sqlite =>
{
    sqlite.MigrationsAssembly("MyCompany.Migrations");
});
```

## Real-world use cases

- Legacy POS modernization with encrypted SQLite kept in production
- Enterprise modules moved to modern .NET while retaining DB compatibility
- Multi-store systems that cannot re-encrypt/rewrite data at once
- Gradual migration from EF 3.1 to newer EF without changing storage format

## Migration strategy for legacy environments

Recommended:

1. Baseline migration for existing schema (empty `Up()`)
2. Add migrations only for new tables/features
3. Prefer explicit/manual SQL for provider edge cases

## Operational and security notes

- Keep secrets/passwords out of source control
- Validate antivirus and EDR behavior for host process launch
- Keep host + native binaries in the same folder
- Align process architecture with native provider requirements (`x86` when needed)
- Pin release versions in controlled enterprise deployments

## Troubleshooting

- **`file is not a database`**: verify encryption mode (RC4/RSA profile), password, file path, and provider/native compatibility.
- **Host missing**: call `SetupBridgeHost()` early in startup.
- **Pipe timeout/hang**: check endpoint collisions, blocked process launch, antivirus restrictions.
- **Migration SQL incompatibility**: disable unsupported SQL patterns and use manual SQL where necessary.

## Repository layout

- `src/EntityFrameworkCore.Sqlite.Legacy` - main package (`net10.0`)
- `src/EntityFrameworkCore.Sqlite.Legacy/Ef31` - EF 3.1 package (`netstandard2.0`)
- `src/Sqlite.LegacyBridge.Ado` - ADO bridge implementation
- `src/Sqlite.LegacyBridge.Client` - named-pipe client
- `src/Sqlite.LegacyBridge.Protocol` - protocol contracts
- `tests/Plu.LegacyBridge.Verify` - verification tests

## CI/CD and publishing

- Workflow: `.github/workflows/nuget-entityframeworkcore-sqlite-legacy.yml`
- Publishes both packages in one pipeline
- Validates package contents before NuGet push
- Auto-tags CI releases on mainline

## Related project

- Host runtime and binary releases: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)

