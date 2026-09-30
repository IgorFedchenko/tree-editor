namespace TreeEditor.Api;

public record NodeDto(int Id, int? ParentId, string Value, bool IsDeleted, bool HasChildren);

public record NodeDetailsDto(int Id, int? ParentId, string Value, bool IsDeleted, List<int> AncestorIds);

public record ApplyRequest(List<CreatedNode>? Created, List<UpdatedNode>? Updated, List<int>? Deleted);

public record CreatedNode(int TempId, int? ParentId, string Value);

public record UpdatedNode(int Id, string Value);

public record ApplyResponse(List<NewId> NewIds);

public record NewId(int TempId, int Id);
