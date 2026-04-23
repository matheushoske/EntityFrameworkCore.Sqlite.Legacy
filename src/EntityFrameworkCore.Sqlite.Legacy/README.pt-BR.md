# EntityFrameworkCore.Sqlite.Legacy

Pacote NuGet para projetos EF Core 10 que precisam acessar bancos SQLite legados criptografados por meio de um host de compatibilidade.

## Instalacao

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy
```

## Foco em ambientes legados criptografados

Este pacote e voltado para cenarios comuns em sistemas enterprise legados/de grande escala, incluindo perfis historicos com criptografia RC4/RSA e comportamentos especificos do `System.Data.SQLite`.

Ele permite:

- manter dominio, repositorios e consultas no EF Core;
- delegar abertura/execucao fisica para o host legado;
- preservar compatibilidade sem exigir migracao imediata de dados.

## Uso basico

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqliteLegacy(@"C:\data\plu.db3", "minha-senha")
    .Options;
```

## Como funciona

`DbContext (EF Core 10)` -> `UseSqliteLegacy(...)` -> `bridge ADO` -> `Named Pipes` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite` -> `banco legado`

## Configuracao avancada

```csharp
optionsBuilder.UseSqliteLegacy(o =>
{
    o.DatabasePath(@"C:\data\cfe.db3");
    o.Password("minha-senha");
    o.HostExecutablePath(@"C:\app\legacy\Sqlite.LegacyBridge.Host.exe");
}, sql =>
{
    sql.MigrationsAssembly("Meu.Assembly.Migrations");
});
```

## Setup automatico do host

No startup da aplicacao:

```csharp
SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

Se o host nao existir, o pacote baixa automaticamente:

- `https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip`

e extrai para `legacy/` no diretorio da aplicacao.

## Compatibilidade

- Target: `net10.0`
- Linha EF Core: `10.x`

Para EF Core 3.1 (`netstandard2.0`), use o pacote:

- `EntityFrameworkCore.Sqlite.Legacy.Ef31`

## Problemas comuns

- **Host nao encontrado**: chame `SetupBridgeHost()` antes da configuracao de DbContext.
- **`file is not a database`**: valide senha, modo de criptografia (RC4/RSA), caminho e compatibilidade do provider nativo.
- **Erro de arquitetura**: alinhe com requisitos nativos do SQLite legado (em muitos cenarios, `x86`).
- **Incompatibilidade em migrations**: use baseline + SQL manual para operacoes nao suportadas.
- **Bloqueio de inicializacao**: valide antivirus/EDR para criacao de processo e uso de pipe local.

## Repositorios relacionados

- Codigo-fonte e docs: [EntityFrameworkCore.Sqlite.Legacy](https://github.com/matheushoske/EntityFrameworkCore.Sqlite.Legacy)
- Host runtime: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)

