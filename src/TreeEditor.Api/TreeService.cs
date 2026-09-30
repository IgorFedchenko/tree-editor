using Microsoft.EntityFrameworkCore;

namespace TreeEditor.Api;

public class TreeService(TreeDbContext db, ILogger<TreeService> logger)
{
    public async Task<ApplyResponse> Apply(ApplyRequest request)
    {
        var created = request.Created ?? [];
        var updated = request.Updated ?? [];
        var deleted = request.Deleted ?? [];

        await using var transaction = await db.Database.BeginTransactionAsync();

        var newNodes = new Dictionary<int, Node>();
        foreach (var item in created)
        {
            if (item.TempId >= 0 || newNodes.ContainsKey(item.TempId))
            {
                throw new InvalidRequestException($"Wrong tempId {item.TempId}: it must be negative and unique");
            }

            var parent = await FindParent(item.ParentId, newNodes);
            var node = parent.AddChild(item.Value);
            db.Nodes.Add(node);
            newNodes[item.TempId] = node;
        }

        foreach (var item in updated)
        {
            var node = await db.Nodes.FindAsync(item.Id) ?? throw new NodeNotFoundException(item.Id);
            node.ChangeValue(item.Value);
        }

        await db.SaveChangesAsync();

        foreach (var id in deleted)
        {
            await DeleteSubtree(id);
        }

        await transaction.CommitAsync();

        logger.LogInformation("Applied changes: {Created} created, {Updated} updated, {Deleted} deleted",
            created.Count, updated.Count, deleted.Count);

        return new ApplyResponse(created.Select(c => new NewId(c.TempId, newNodes[c.TempId].Id)).ToList());
    }

    private async Task<Node> FindParent(int? parentId, Dictionary<int, Node> newNodes)
    {
        if (parentId is null)
        {
            throw new InvalidRequestException("parentId is required");
        }

        if (parentId < 0)
        {
            return newNodes.GetValueOrDefault(parentId.Value)
                ?? throw new InvalidRequestException($"Parent {parentId} must be created before its child");
        }

        return await db.Nodes.FindAsync(parentId.Value) ?? throw new NodeNotFoundException(parentId.Value);
    }

    private async Task DeleteSubtree(int id)
    {
        if (!await db.Nodes.AnyAsync(n => n.Id == id))
        {
            throw new NodeNotFoundException(id);
        }

        await db.Database.ExecuteSqlAsync($"""
            WITH RECURSIVE subtree AS (
                SELECT id FROM nodes WHERE id = {id}
                UNION ALL
                SELECT n.id FROM nodes n JOIN subtree ON n.parent_id = subtree.id
            )
            UPDATE nodes SET is_deleted = true, deleted_at = now()
            WHERE id IN (SELECT id FROM subtree) AND NOT is_deleted
            """);
    }
}
