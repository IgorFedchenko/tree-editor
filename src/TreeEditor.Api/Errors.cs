namespace TreeEditor.Api;

public class NodeNotFoundException(int id) : Exception($"Node {id} not found");

public class NodeDeletedException(int id) : Exception($"Node {id} is deleted");

public class InvalidRequestException(string message) : Exception(message);
