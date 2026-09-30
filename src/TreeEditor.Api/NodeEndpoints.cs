using Microsoft.EntityFrameworkCore;

namespace TreeEditor.Api;

public static class NodeEndpoints
{
    public static void MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/nodes/roots", (TreeDbContext db) =>
            LoadNodes(db, db.Nodes.Where(n => n.ParentId == null)));

        api.MapGet("/nodes/{id:int}/children", async (int id, TreeDbContext db) =>
        {
            if (!await db.Nodes.AnyAsync(n => n.Id == id))
            {
                throw new NodeNotFoundException(id);
            }

            return await LoadNodes(db, db.Nodes.Where(n => n.ParentId == id));
        });

        api.MapGet("/nodes/{id:int}", async (int id, TreeDbContext db) =>
        {
            var node = await db.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id)
                ?? throw new NodeNotFoundException(id);

            var ancestorIds = await db.Database.SqlQuery<AncestorRow>($"""
                WITH RECURSIVE up AS (
                    SELECT id, parent_id, 0 AS depth FROM nodes WHERE id = {id}
                    UNION ALL
                    SELECT n.id, n.parent_id, up.depth + 1 FROM nodes n JOIN up ON n.id = up.parent_id
                )
                SELECT id AS "Id", depth AS "Depth" FROM up WHERE id <> {id}
                """)
                .OrderByDescending(a => a.Depth)
                .Select(a => a.Id)
                .ToListAsync();

            return new NodeDetailsDto(node.Id, node.ParentId, node.Value, node.IsDeleted, ancestorIds);
        });

        api.MapPost("/apply", (ApplyRequest request, TreeService tree) => tree.Apply(request));

        api.MapPost("/reset", async (TreeDbContext db, ILogger<Program> logger) =>
        {
            await SampleData.Reset(db);
            logger.LogInformation("Database is reset to the sample data");
            return Results.NoContent();
        });
    }

    private static Task<List<NodeDto>> LoadNodes(TreeDbContext db, IQueryable<Node> nodes) =>
        nodes.OrderBy(n => n.Id)
            .Select(n => new NodeDto(n.Id, n.ParentId, n.Value, n.IsDeleted, db.Nodes.Any(c => c.ParentId == n.Id)))
            .ToListAsync();

    private record AncestorRow(int Id, int Depth);
}
