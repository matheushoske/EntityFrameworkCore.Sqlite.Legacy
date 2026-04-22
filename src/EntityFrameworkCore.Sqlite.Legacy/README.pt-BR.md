# EntityFrameworkCore.Sqlite.Legacy

O `EntityFrameworkCore.Sqlite.Legacy` permite usar Entity Framework Core em bancos SQLite legados criptografados, roteando as chamadas ADO para um host .NET Framework via named pipes.

## Arquitetura

`DbContext EF Core` -> `Sqlite.LegacyBridge.Ado` -> `Sqlite.LegacyBridge.Client` -> `Named Pipe` -> `Sqlite.LegacyBridge.Host (.NET Framework 4.8.1)` -> `System.Data.SQLite`.

## Criptografia suportada

- Bases SQLite legadas abertas pelo `System.Data.SQLite` no host.
- Acesso com senha (por exemplo `PLU_SQLITE_PASSWORD` / `CFE_SQLITE_PASSWORD`).
- Cenarios de criptografia RSA/legado suportados pelo provider nativo configurado no host.

## Pre-requisitos e limitacoes

- O binario do host deve estar em `legacy/` na saida (copiado pelos targets do pacote).
- Arquitetura do host e do consumidor deve ser compativel (`x86` nesta stack).
- O pacote suporta:
  - `netstandard2.0` + EF Core 3.1.
  - `net10.0` + EF Core 10.
- Em runtime ha dependencia de named pipes e inicializacao do processo host local.

## Instalacao e uso

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqliteLegacy(@"C:\data\plu.db3", "minha-senha")
    .Options;
```

Configuracao com builder:

```csharp
optionsBuilder.UseSqliteLegacy(o =>
{
    o.DatabasePath(@"C:\data\cfe.db3");
    o.Password("minha-senha");
    o.HostExecutablePath(@"C:\app\legacy\Sqlite.LegacyBridge.Host.exe");
});
```

## Migrations

- Recomendado para base legada: baseline vazia e novas migrations apenas para tabelas novas.
- Quando necessario, configurar assembly de migration explicitamente:
  - `UseSqliteLegacy(..., configureSqlite: sql => sql.MigrationsAssembly("Seu.Assembly"))`.

## Troubleshooting

- **Host nao encontrado**: confirme `legacy/Sqlite.LegacyBridge.Host.exe` na pasta de output/publish.
- **Incompatibilidade de arquitetura**: use `x86` quando exigido pela stack nativa SQLite.
- **`file is not a database`**: valide senha/criptografia e caminho do arquivo.
- **Erros de migrate em provider legado**: considere baseline + migration SQL manual para tabelas criticas.

