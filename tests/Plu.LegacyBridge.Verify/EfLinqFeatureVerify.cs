using Microsoft.EntityFrameworkCore;
using EntityFrameworkCore.Sqlite.Legacy;

namespace Plu.LegacyBridge.Verify;

/// <summary>
/// Garante que <see cref="UseSqliteLegacy"/> usa o mesmo pipeline de consultas do provider SQLite do EF;
/// o bridge só substitui a execução ADO. Estes testes exercitam LINQ, rastreamento e persistência.
/// </summary>
internal static class EfLinqFeatureVerify
{
    public static async Task<string?> RunAllAsync(
        string sourcePluDbPath,
        string password,
        string? hostExecutablePath,
        CancellationToken cancellationToken = default)
    {
        var tempDb = Path.Combine(Path.GetTempPath(), "plu_ef_linq_" + Guid.NewGuid().ToString("N") + ".db3");
        File.Copy(sourcePluDbPath, tempDb, overwrite: true);

        try
        {
            var opts = new DbContextOptionsBuilder<EfLinqTestContext>()
                .UseSqliteLegacy(o =>
                {
                    o.DatabasePath(tempDb);
                    o.Password(password);
                    if (!string.IsNullOrEmpty(hostExecutablePath))
                        o.HostExecutablePath(hostExecutablePath);
                })
                .Options;

            await using (var ctx = new EfLinqTestContext(opts))
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS _ef_parent (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL);
                    CREATE TABLE IF NOT EXISTS _ef_child (Id INTEGER PRIMARY KEY AUTOINCREMENT, ParentId INTEGER NOT NULL, Label TEXT NOT NULL, FOREIGN KEY (ParentId) REFERENCES _ef_parent(Id) ON DELETE CASCADE);
                    """,
                    cancellationToken);

                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM _ef_child;", cancellationToken);
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM _ef_parent;", cancellationToken);

                // --- LINQ leitura em tabela real (versao) na cópia ---
                var verCount = await ctx.Versao.AsNoTracking().CountAsync(cancellationToken);
                if (verCount < 1)
                    return "Versao: esperado pelo menos 1 linha na cópia.";

                var verOrdered = await ctx.Versao.AsNoTracking()
                    .Where(v => v.EXECUCAO != null && v.EXECUCAO != "")
                    .OrderBy(v => v.EXECUCAO)
                    .Select(v => v.EXECUCAO!)
                    .Take(3)
                    .ToListAsync(cancellationToken);
                if (verOrdered.Count < 1)
                    return "Versao: Where + OrderBy + Select + Take.";

                var verAny = await ctx.Versao.AsNoTracking().AnyAsync(v => v.EXECUCAO != null, cancellationToken);
                if (!verAny)
                    return "Versao: AnyAsync.";

                var namesFilter = new[] { verOrdered[0] };
                var verIn = await ctx.Versao.AsNoTracking()
                    .Where(v => v.EXECUCAO != null && namesFilter.Contains(v.EXECUCAO))
                    .CountAsync(cancellationToken);
                if (verIn < 1)
                    return "Versao: Contains (traduz para IN).";

                var verFirst = await ctx.Versao.AsNoTracking()
                    .OrderByDescending(v => v.EXECUCAO)
                    .FirstOrDefaultAsync(cancellationToken);
                if (verFirst is null)
                    return "Versao: OrderByDescending + FirstOrDefaultAsync.";

                // --- Add + SaveChangesAsync + navegação ---
                var parent = new EfParent { Name = "P1" };
                parent.Children.Add(new EfChild { Label = "c1" });
                parent.Children.Add(new EfChild { Label = "c2" });
                ctx.Parents.Add(parent);
                await ctx.SaveChangesAsync(cancellationToken);
                if (parent.Id == 0)
                    return "SaveChanges: Id de pai não preenchido (identity).";

                // --- Include + filtro ---
                var loaded = await ctx.Parents.AsNoTracking()
                    .Include(p => p.Children)
                    .Where(p => p.Id == parent.Id)
                    .SingleAsync(cancellationToken);
                if (loaded.Children.Count != 2)
                    return "Include: esperado 2 filhos.";

                var hasC1 = await ctx.Parents.AsNoTracking()
                    .Where(p => p.Children.Any(c => c.Label == "c1"))
                    .Select(p => p.Id)
                    .SingleAsync(cancellationToken);
                if (hasC1 != parent.Id)
                    return "Where com Any em coleção.";

                // --- OrderBy / ThenBy / Skip / Take ---
                var orderedChildren = await ctx.Children.AsNoTracking()
                    .Where(c => c.ParentId == parent.Id)
                    .OrderByDescending(c => c.Label)
                    .ThenBy(c => c.Id)
                    .ToListAsync(cancellationToken);
                if (orderedChildren.Count != 2 || orderedChildren[0].Label != "c2")
                    return "OrderByDescending + ThenBy.";

                var second = await ctx.Children.AsNoTracking()
                    .Where(c => c.ParentId == parent.Id)
                    .OrderBy(c => c.Id)
                    .Skip(1)
                    .Take(1)
                    .SingleAsync(cancellationToken);
                if (second.Label != "c2")
                    return "Skip + Take.";

                // --- GroupBy ---
                var groups = await ctx.Children.AsNoTracking()
                    .GroupBy(c => c.ParentId)
                    .Select(g => new { g.Key, Cnt = g.Count() })
                    .ToListAsync(cancellationToken);
                if (!groups.Any(g => g.Key == parent.Id && g.Cnt == 2))
                    return "GroupBy + Count.";

                // --- Max / Min ---
                var maxLabel = await ctx.Children.AsNoTracking()
                    .Where(c => c.ParentId == parent.Id)
                    .MaxAsync(c => c.Label, cancellationToken);
                if (maxLabel != "c2")
                    return "MaxAsync.";

                // --- Rastreamento + SaveChangesAsync (update) ---
                var tracked = await ctx.Parents.FirstAsync(p => p.Id == parent.Id, cancellationToken);
                tracked.Name = "P1-upd";
                var written = await ctx.SaveChangesAsync(cancellationToken);
                if (written < 1)
                    return "SaveChangesAsync (update) não persistiu linhas.";

                await using (var readCtx = new EfLinqTestContext(opts))
                {
                    var name = await readCtx.Parents.AsNoTracking()
                        .Where(p => p.Id == parent.Id)
                        .Select(p => p.Name)
                        .SingleAsync(cancellationToken);
                    if (name != "P1-upd")
                        return "Update não refletido na nova instância do contexto.";
                }

                // --- FindAsync ---
                await using (var findCtx = new EfLinqTestContext(opts))
                {
                    var found = await findCtx.Parents.FindAsync([parent.Id], cancellationToken);
                    if (found is null || found.Name != "P1-upd")
                        return "FindAsync.";
                }

                // --- Entry + ReloadAsync ---
                var entryParent = await ctx.Parents.FirstAsync(p => p.Id == parent.Id, cancellationToken);
                await ctx.Entry(entryParent).ReloadAsync(cancellationToken);
                if (entryParent.Name != "P1-upd")
                    return "ReloadAsync.";

                // --- Transação explícita ---
                await using (var tx = await ctx.Database.BeginTransactionAsync(cancellationToken))
                {
                    ctx.Parents.Add(new EfParent { Name = "TxP" });
                    await ctx.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }

                var txCount = await ctx.Parents.CountAsync(p => p.Name == "TxP", cancellationToken);
                if (txCount != 1)
                    return "BeginTransactionAsync + Commit.";

                // --- ExecuteUpdateAsync (EF 7+) ---
                var upd = await ctx.Parents
                    .Where(p => p.Name == "TxP")
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "TxP2"), cancellationToken);
                if (upd != 1)
                    return "ExecuteUpdateAsync.";

                // --- ExecuteDeleteAsync ---
                var del = await ctx.Parents.Where(p => p.Name == "TxP2").ExecuteDeleteAsync(cancellationToken);
                if (del != 1)
                    return "ExecuteDeleteAsync.";

                // --- Remove + SaveChanges (cascade) ---
                var toRemove = await ctx.Parents.Include(p => p.Children).FirstAsync(p => p.Id == parent.Id, cancellationToken);
                ctx.Parents.Remove(toRemove);
                await ctx.SaveChangesAsync(cancellationToken);
                var left = await ctx.Parents.CountAsync(cancellationToken);
                if (left != 0)
                    return "Remove pai: esperado 0 pais após cascade.";
                var childLeft = await ctx.Children.CountAsync(cancellationToken);
                if (childLeft != 0)
                    return "Remove pai: esperado 0 filhos (cascade).";

                // --- Like (função traduzida) ---
                await ctx.Database.ExecuteSqlRawAsync(
                    "INSERT INTO _ef_parent (Name) VALUES ('LikeA'), ('LikeB');",
                    cancellationToken);
                var likeN = await ctx.Parents.AsNoTracking()
                    .CountAsync(p => EF.Functions.Like(p.Name, "Like%"), cancellationToken);
                if (likeN != 2)
                    return "EF.Functions.Like.";

                var likeAId = await ctx.Parents.AsNoTracking()
                    .Where(p => p.Name == "LikeA")
                    .Select(p => p.Id)
                    .SingleAsync(cancellationToken);
                await ctx.Database.ExecuteSqlRawAsync(
                    "INSERT INTO _ef_child (ParentId, Label) VALUES ({0}, 'sm1');",
                    [likeAId],
                    cancellationToken);

                var flatLabels = await ctx.Parents.AsNoTracking()
                    .Where(p => p.Name.StartsWith("Like"))
                    .SelectMany(p => p.Children)
                    .Select(c => c.Label)
                    .ToListAsync(cancellationToken);
                if (!flatLabels.Contains("sm1") || flatLabels.Count != 1)
                    return "SelectMany em navegação.";

                var avgId = await ctx.Children.AsNoTracking()
                    .Where(c => c.Label == "sm1")
                    .AverageAsync(c => (double)c.Id, cancellationToken);
                if (avgId < 1d)
                    return "AverageAsync.";
            }

            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(tempDb))
                    File.Delete(tempDb);
            }
            catch
            {
                // ignore
            }
        }
    }
}

public sealed class EfLinqTestContext : DbContext
{
    public EfLinqTestContext(DbContextOptions<EfLinqTestContext> options)
        : base(options)
    {
    }

    public DbSet<VersaoVerifyRow> Versao => Set<VersaoVerifyRow>();
    public DbSet<EfParent> Parents => Set<EfParent>();
    public DbSet<EfChild> Children => Set<EfChild>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VersaoVerifyRow>(e =>
        {
            e.ToTable("versao");
            e.HasNoKey();
        });

        modelBuilder.Entity<EfParent>(e =>
        {
            e.ToTable("_ef_parent", tb => tb.UseSqlReturningClause(false));
            e.Property(p => p.Name).IsRequired();
        });

        modelBuilder.Entity<EfChild>(e =>
        {
            e.ToTable("_ef_child", tb => tb.UseSqlReturningClause(false));
            e.Property(c => c.Label).IsRequired();
            e.HasOne(c => c.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public sealed class EfParent
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<EfChild> Children { get; set; } = new();
}

public sealed class EfChild
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Label { get; set; } = "";
    public EfParent? Parent { get; set; }
}
