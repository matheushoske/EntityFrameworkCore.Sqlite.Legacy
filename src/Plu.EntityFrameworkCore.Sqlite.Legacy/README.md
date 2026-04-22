# EntityFrameworkCore.Sqlite.Legacy

`EntityFrameworkCore.Sqlite.Legacy` enables Entity Framework Core to access legacy encrypted SQLite databases by routing ADO calls to a .NET Framework host over named pipes.

## Architecture

`EF Core DbContext` -> `Sqlite.LegacyBridge.Ado` -> `Sqlite.LegacyBridge.Client` -> `Named Pipe` -> `Sqlite.LegacyBridge.Host (.NET Framework 4.8.1)` -> `System.Data.SQLite`.

## Supported encryption

- Legacy SQLite files opened via `System.Data.SQLite` in host mode.
- Password-based access (for example from `PLU_SQLITE_PASSWORD` / `CFE_SQLITE_PASSWORD`).
- RSA/legacy-compatible encryption scenarios supported by the native provider configured in host.

## Prerequisites and limitations

- Host binary must be available in `legacy/` output folder (copied by package targets).
- Host and consumer architecture must be compatible (`x86` in this stack).
- The package supports:
  - `netstandard2.0` + EF Core 3.1.
  - `net10.0` + EF Core 10.
- Runtime depends on named pipes and local host process startup.

## Install and use

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqliteLegacy(@"C:\data\plu.db3", "my-password")
    .Options;
```

You can also configure with options builder callback:

```csharp
optionsBuilder.UseSqliteLegacy(o =>
{
    o.DatabasePath(@"C:\data\cfe.db3");
    o.Password("my-password");
    o.HostExecutablePath(@"C:\app\legacy\Sqlite.LegacyBridge.Host.exe");
});
```

## Migrations

- Recommended for legacy databases: create an empty baseline migration, then add new migrations only for new tables.
- Keep migration assembly configured explicitly when needed:
  - `UseSqliteLegacy(..., configureSqlite: sql => sql.MigrationsAssembly("Your.Assembly"))`.

## Troubleshooting

- **Host not found**: ensure `legacy/Sqlite.LegacyBridge.Host.exe` is copied to output/publish.
- **Architecture mismatch**: use `x86` where required by native SQLite stack.
- **`file is not a database`**: validate password/encryption and file path.
- **EF migrate provider edge-cases**: for legacy providers, consider baseline + manual SQL migration for critical tables.

