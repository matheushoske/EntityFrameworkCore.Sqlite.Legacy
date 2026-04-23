# EntityFrameworkCore.Sqlite.Legacy

Acesse bancos SQLite legados criptografados a partir de aplicacoes modernas com EF Core, sem reescrever toda a camada de dados.

Este repositorio foi criado para modernizacao gradual de sistemas enterprise/de grande escala que ainda dependem de arquivos SQLite criptografados e comportamento legado do `System.Data.SQLite`.

## Por que este projeto existe

Em muitos ambientes reais, a base de dados utiliza:

- arquivos `.db3` legados;
- stacks de criptografia historicas (incluindo cenarios RC4/RSA);
- requisitos de compatibilidade nativa que nao funcionam bem com acesso direto moderno.

Neste contexto, migrar tudo de uma vez costuma ser arriscado.  
Este projeto permite evoluir o sistema por etapas, mantendo compatibilidade.

## Pacotes publicados

- `EntityFrameworkCore.Sqlite.Legacy` (`net10.0` + EF Core 10)
- `EntityFrameworkCore.Sqlite.Legacy.Ef31` (`netstandard2.0` + EF Core 3.1)

## Arquitetura

`Aplicacao` -> `EF Core` -> `UseSqliteLegacy(...)` -> `ADO bridge` -> `Named Pipes` -> `Sqlite.LegacyBridge.Host (net462)` -> `System.Data.SQLite` -> `banco criptografado`

## Instalacao

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy
```

```bash
dotnet add package EntityFrameworkCore.Sqlite.Legacy.Ef31
```

## Exemplo rapido

```csharp
using EntityFrameworkCore.Sqlite.Legacy;
using Microsoft.EntityFrameworkCore;

services.AddDbContext<MyDbContext>(options =>
{
    options.UseSqliteLegacy(o =>
    {
        o.DatabasePath(@"C:\pdv\data\plu.db3");
        o.Password("sua-senha");
    });
});
```

## Setup automatico do host

No startup:

```csharp
SqliteLegacyDbContextOptionsExtensions.SetupBridgeHost();
```

Se o host nao existir, o pacote baixa:

- `https://github.com/matheushoske/Sqlite.LegacyBridge.Host/releases/latest/download/Sqlite.LegacyBridge.Host.zip`

## Casos de uso

- modernizacao gradual de sistema legado sem migracao total de banco;
- manutencao de compatibilidade com criptografia historica (RC4/RSA);
- adocao de padroes EF Core em cima de base antiga;
- rollouts por etapas em ambientes com alto risco operacional.

## Boas praticas operacionais

- manter senha em segredo/configuracao segura;
- manter host e bins nativos no mesmo diretorio `legacy/`;
- validar requisitos de arquitetura (`x86` quando exigido);
- aplicar baseline em migrations de bases legadas antes de novas tabelas.

## Troubleshooting

- **`file is not a database`**: confira senha, modo de criptografia e provider nativo.
- **Host ausente**: chamar `SetupBridgeHost()` antes de configurar contexto.
- **Pipe travando**: verificar bloqueios de antivirus/EDR e colisoes de endpoint.
- **Erro de migration**: usar SQL manual quando o provider nao suportar certos recursos.

## Projeto relacionado

- Host e releases binarios: [Sqlite.LegacyBridge.Host](https://github.com/matheushoske/Sqlite.LegacyBridge.Host)
