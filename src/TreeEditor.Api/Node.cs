namespace TreeEditor.Api;

public class Node
{
    public const int MaxValueLength = 100;

    public int Id { get; }

    public int? ParentId { get; }

    public Node? Parent { get; private set; }

    public string Value { get; private set; } = "";

    public bool IsDeleted { get; }

    public DateTimeOffset? DeletedAt { get; }

    private Node()
    {
    }

    public static Node CreateRoot(string value) => new() { Value = CheckValue(value) };

    // the parent is set only here, so a node can never be moved
    public Node AddChild(string value)
    {
        if (IsDeleted)
        {
            throw new NodeDeletedException(Id);
        }

        return new Node { Parent = this, Value = CheckValue(value) };
    }

    public void ChangeValue(string value)
    {
        if (IsDeleted)
        {
            throw new NodeDeletedException(Id);
        }

        Value = CheckValue(value);
    }

    private static string CheckValue(string? value)
    {
        // the value is null when it is missing in the request json
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length is 0 or > MaxValueLength)
        {
            throw new InvalidRequestException($"Value must be from 1 to {MaxValueLength} characters");
        }

        // PostgreSQL cannot store a zero character in text
        if (trimmed.Contains('\0'))
        {
            throw new InvalidRequestException("Value must not contain a zero character");
        }

        return trimmed;
    }
}
