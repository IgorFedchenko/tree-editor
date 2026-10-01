using TreeEditor.Api;

namespace TreeEditor.Tests;

public class NodeTests
{
    [Fact]
    public void Value_is_trimmed()
    {
        var node = Node.CreateRoot("Root");

        node.ChangeValue("  X  ");

        Assert.Equal("X", node.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b")]
    public void Wrong_value_is_rejected(string value)
    {
        var node = Node.CreateRoot("Root");

        Assert.Throws<InvalidRequestException>(() => node.ChangeValue(value));
        Assert.Throws<InvalidRequestException>(() => node.AddChild(value));
        Assert.Throws<InvalidRequestException>(() => Node.CreateRoot(value));
        Assert.Equal("Root", node.Value);
    }

    [Fact]
    public void Value_can_have_100_characters_but_not_101()
    {
        var node = Node.CreateRoot("Root");

        node.ChangeValue(new string('a', 100));

        Assert.Throws<InvalidRequestException>(() => node.ChangeValue(new string('a', 101)));
        Assert.Equal(100, node.Value.Length);
    }

    [Fact]
    public void Child_keeps_its_parent()
    {
        var root = Node.CreateRoot("Root");

        var child = root.AddChild("A");

        Assert.Same(root, child.Parent);
        Assert.Equal("A", child.Value);
    }
}
