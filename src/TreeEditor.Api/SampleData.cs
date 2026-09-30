using Microsoft.EntityFrameworkCore;

namespace TreeEditor.Api;

public static class SampleData
{
    public static async Task Reset(TreeDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync("TRUNCATE nodes RESTART IDENTITY");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO nodes (id, parent_id, value) VALUES
                (1, NULL, 'Node 1'),
                (2, 1, 'Node 1.1'),
                (3, 2, 'Node 1.1.1'),
                (4, 3, 'Node 1.1.1.1'),
                (5, 4, 'Node 1.1.1.1.1'),
                (6, 5, 'Node 1.1.1.1.1.1'),
                (7, 4, 'Node 1.1.1.1.2'),
                (8, 3, 'Node 1.1.1.2'),
                (9, 2, 'Node 1.1.2'),
                (10, 1, 'Node 1.2'),
                (11, 10, 'Node 1.2.1'),
                (12, 10, 'Node 1.2.2'),
                (13, NULL, 'Node 2'),
                (14, 13, 'Node 2.1'),
                (15, 14, 'Node 2.1.1'),
                (16, 15, 'Node 2.1.1.1'),
                (17, 13, 'Node 2.2')
            """);
        // explicit ids do not move the identity sequence
        await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('nodes', 'id'), (SELECT max(id) FROM nodes))");

        await transaction.CommitAsync();
    }
}
