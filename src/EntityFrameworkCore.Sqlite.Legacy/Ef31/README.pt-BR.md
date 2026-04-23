# EntityFrameworkCore.Sqlite.Legacy.Ef31

Pacote EF Core 3.1 (`netstandard2.0`) para acesso a SQLite legado criptografado via host de compatibilidade.

## Instalacao

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy.Ef31
```

## Quando usar este pacote

Use este pacote quando sua aplicacao ainda esta em EF Core 3.1 e nao pode migrar de runtime imediatamente.

Cenarios comuns:

- sistemas enterprise legados de longa vida em producao;
- modulos on-premise de grande escala com stack congelada;
- bancos `.db3` criptografados com perfis legados (RC4/RSA).

Se sua aplicacao ja estiver em .NET 10 + EF Core 10, prefira:

- `EntityFrameworkCore.Sqlite.Legacy`

## Uso rapido

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqliteLegacy(@"C:\data\legacy.db3", "senha-legada")
    .Options;
```

## Bootstrap do host

```csharp
SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

## Arquitetura

`EF Core 3.1` -> `ADO bridge` -> `Named Pipes` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite`

## Dicas de producao

- manter host e bins nativos juntos em `legacy/`;
- validar modo de criptografia/senha conforme base original;
- em migrations legadas, preferir baseline + SQL manual para casos limite.

## Repositorios relacionados

- Codigo-fonte: [EntityFrameworkCore.Sqlite.Legacy](https://github.com/matheushoske/EntityFrameworkCore.Sqlite.Legacy)
- Host binario: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)
