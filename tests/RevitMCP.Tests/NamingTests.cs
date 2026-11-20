using RevitMCP.Shared.Utils;
using Xunit;

namespace RevitMCP.Tests
{
    public class NamingTests
    {
        [Theory]
        [InlineData("rebar", "Creating rebar", "MCP: Creating rebar")]
        [InlineData("area_reinforcement", "Area reinforcement", "MCP: Area reinforcement")]
        [InlineData("wall", "Creating wall", "MCP: Creating wall")]
        [InlineData("slab", "Creating floor", "MCP: Creating floor")]
        [InlineData("floor", "Creating floor", "MCP: Creating floor")]
        [InlineData("beam", "Creating beam", "MCP: Creating beam")]
        [InlineData("column", "Creating column", "MCP: Creating column")]
        [InlineData("room", "Creating room", "MCP: Creating room")]
        [InlineData("grid", "Creating grid", "MCP: Creating grid")]
        [InlineData("level", "Creating level", "MCP: Creating level")]
        [InlineData("family_instance", "Placing family instance", "MCP: Placing family instance")]
        public void TestCreateNaming(string action, string expectedSummary, string expectedTx)
        {
            Assert.Equal(expectedSummary, HumanFriendlyNaming.GetCreateActivitySummary(action));
            Assert.Equal(expectedTx, HumanFriendlyNaming.GetCreateTransactionName(action));
        }

        [Theory]
        [InlineData("delete", 1, "Deleting (1 items)", "MCP: Delete Elements")]
        [InlineData("move", 5, "Moving (5 items)", "MCP: Move Elements")]
        [InlineData("rotate", 2, "Rotating (2 items)", "MCP: Rotate Elements")]
        [InlineData("mirror", 3, "Mirroring (3 items)", "MCP: Mirror Elements")]
        [InlineData("copy", 10, "Copying (10 items)", "MCP: Copy Elements")]
        public void TestTransformNaming(string action, int count, string expectedSummary, string expectedTx)
        {
            Assert.Equal(expectedSummary, HumanFriendlyNaming.GetTransformActivitySummary(action, count));
            Assert.Equal(expectedTx, HumanFriendlyNaming.GetTransformTransactionName(action));
        }

        [Theory]
        [InlineData("override_graphics", 1, "Graphic override (1 items)", "MCP: Graphic Override")]
        [InlineData("set_rebar_presentation", 37, "Rebar presentation (37 items)", "MCP: Rebar Presentation")]
        [InlineData("isolate", 2, "Isolating elements (2 items)", "MCP: Isolate Elements")]
        [InlineData("hide", 4, "Hiding elements (4 items)", "MCP: Hide Elements")]
        [InlineData("reset", 0, "Resetting graphics", "MCP: Reset Graphics")]
        public void TestVisualOverrideNaming(string action, int count, string expectedSummary, string expectedTx)
        {
            Assert.Equal(expectedSummary, HumanFriendlyNaming.GetVisualOverrideActivitySummary(action, count));
            Assert.Equal(expectedTx, HumanFriendlyNaming.GetVisualOverrideTransactionName(action));
        }

        [Fact]
        public void TestParameterNaming()
        {
            Assert.Equal("Writing parameters (3 items)", HumanFriendlyNaming.GetParametersActivitySummary("set", 3));
            Assert.Equal("Reading parameters (5 items)", HumanFriendlyNaming.GetParametersActivitySummary("get", 5));
            Assert.Equal("MCP: Set Parameters", HumanFriendlyNaming.GetParametersTransactionName());
        }

        [Theory]
        // English user session
        [InlineData("Create Slab Reinforcement", "Creating bottom rebar mesh", "MCP: Create Slab Reinforcement", "Creating bottom rebar mesh")]
        // German user session
        [InlineData("Deckenbewehrung erstellen", "Erstellen der Deckenbewehrungsmatte (Ø12 s=200)", "MCP: Deckenbewehrung erstellen", "Erstellen der Deckenbewehrungsmatte (Ø12 s=200)")]
        // French user session
        [InlineData("Ferraillage de la dalle", "Création des épingles en U sur le bord de dalle", "MCP: Ferraillage de la dalle", "Création des épingles en U sur le bord de dalle")]
        // Already prefixed with MCP:
        [InlineData("MCP: Custom Action", "Custom activity summary", "MCP: Custom Action", "Custom activity summary")]
        public void TestMultilingualAiResolution(string aiTx, string aiSummary, string expectedTx, string expectedSummary)
        {
            string tx = HumanFriendlyNaming.ResolveTransactionName(aiTx, "rebar", "create");
            string summary = HumanFriendlyNaming.ResolveActivitySummary(aiSummary, "rebar", "create");

            Assert.Equal(expectedTx, tx);
            Assert.Equal(expectedSummary, summary);
        }

        [Fact]
        public void TestMultilingualFallbackWhenAiOmitsNaming()
        {
            string tx = HumanFriendlyNaming.ResolveTransactionName(null, "rebar", "create");
            string summary = HumanFriendlyNaming.ResolveActivitySummary(null, "rebar", "create");

            Assert.Equal("MCP: Creating rebar", tx);
            Assert.Equal("Creating rebar", summary);

            string querySummary = HumanFriendlyNaming.ResolveActivitySummary(null, "OST_Floors", "query_elements");
            Assert.Equal("Query Elements (OST_Floors)", querySummary);

            string geomSummary = HumanFriendlyNaming.ResolveActivitySummary(null, null, "inspect_element_geometry", 2969454);
            Assert.Equal("Inspect Geometry (ID 2969454)", geomSummary);
        }
    }
}
