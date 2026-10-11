using ReWASD.UI;

namespace ReWASD.UI.Mac.Tests;

public class BuildInfoTests
{
    [Fact]
    public void TheBuildText_NamesTheVersion_AndTheCommitWhenKnown()
    {
        Assert.StartsWith("v" + BuildInfo.Version, BuildInfo.Text);
        Assert.Matches("^(|[0-9a-f]{7})$", BuildInfo.Revision);
        if (BuildInfo.Revision.Length > 0) Assert.EndsWith(" · " + BuildInfo.Revision, BuildInfo.Text);
    }
}
