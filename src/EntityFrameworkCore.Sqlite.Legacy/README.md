# EntityFrameworkCore.Sqlite.Legacy

NuGet package for EF Core 10 projects that need legacy encrypted SQLite compatibility through a bridge host.

## Install

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy
```

## Designed for legacy encrypted datasets

This package is commonly used when the database was created with provider/encryption stacks historically found in large-scale legacy enterprise systems, including scenarios with RC4 or RSA-oriented legacy encryption workflows.

It keeps EF Core at application level while delegating low-level DB opening/execution to a host that runs with `System.Data.SQLite`.

## How it works

`DbContext (EF Core 10)` -> `UseSqliteLegacy(...)` -> `Named Pipe Bridge` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite` -> `legacy encrypted db`

## Setup in Startup

```csharp
using EntityFrameworkCore.Sqlite.Legacy;

SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

Then configure your context:

```csharp
services.AddDbContext<MyDbContext>(options =>
{
    options.UseSqliteLegacy(o =>
    {
        o.DatabasePath(@"C:\data\plu.db3");
        o.Password("your-password");
    });
});
```

## Automatic host provisioning

`SetupBridgeHost()` checks runtime `legacy/` content and downloads host binaries if missing:

- `https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip`

## Advanced configuration example

```csharp
optionsBuilder.UseSqliteLegacy(o =>
{
    o.DatabasePath(@"C:\data\cfe.db3");
    o.Password("your-password");
    o.HostExecutablePath(@"C:\myapp\legacy\Sqlite.LegacyBridge.Host.exe");
}, sqlite =>
{
    sqlite.MigrationsAssembly("My.Migrations");
});
```

## Compatibility

- Target: `net10.0`
- EF family: `10.x`

Need EF Core 3.1? Use:

- `EntityFrameworkCore.Sqlite.Legacy.Ef31`

## Notes for production legacy environments

- Validate encryption/password compatibility with your original DB creator stack.
- Keep host and native SQLite files together.
- Prefer controlled rollout with fixed package/release versions.
- For migration edge-cases in old providers, use baseline + manual SQL strategy.

## Troubleshooting

- **`file is not a database`**: wrong password, wrong file, incompatible encryption/provider pairing.
- **Host not found**: call `SetupBridgeHost()` before DbContext wiring.
- **Interop/runtime errors**: check architecture requirements (`x86` in many legacy setups).
- **Startup blocked**: verify antivirus rules for host process creation.

## Links

- Source repository: [EntityFrameworkCore.Sqlite.Legacy](https://github.com/matheushoske/EntityFrameworkCore.Sqlite.Legacy)
- Host binaries: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)

