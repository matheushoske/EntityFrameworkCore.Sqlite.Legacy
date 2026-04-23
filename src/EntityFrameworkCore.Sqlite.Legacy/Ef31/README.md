# EntityFrameworkCore.Sqlite.Legacy.Ef31

EF Core 3.1 (`netstandard2.0`) package for legacy encrypted SQLite integration through the bridge host.

## Install

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy.Ef31
```

## When this package is the right choice

Use this package when you are maintaining legacy/mid-life systems that still run EF Core 3.1 and cannot migrate framework/runtime immediately.

Typical scenarios:

- long-lived enterprise applications
- large-scale on-prem modules with frozen runtime constraints
- systems with old encrypted `.db3` files (RC4/RSA legacy profiles)

If you are already on .NET 10 + EF Core 10, use:

- `EntityFrameworkCore.Sqlite.Legacy`

## Basic usage

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqliteLegacy(@"C:\data\legacy.db3", "legacy-password")
    .Options;
```

## Host bootstrap (recommended)

```csharp
SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

Default host release source:

- `https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip`

## Architecture summary

`EF Core 3.1` -> `ADO bridge` -> `Named pipes` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite`

## Production notes

- Keep host/native binaries together in `legacy/`
- Validate original encryption mode and password provisioning
- Use baseline-first migration strategy in legacy DBs
- Prefer explicit SQL for provider-incompatible migration operations

## Related repositories

- Package source: [EntityFrameworkCore.Sqlite.Legacy](https://github.com/matheushoske/EntityFrameworkCore.Sqlite.Legacy)
- Host binary releases: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)
