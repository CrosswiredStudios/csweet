using CSweet.Agents.ChiefOfStaff;
using System.Text.Json;

namespace CSweet.Agents.ChiefOfStaff.Tests;

public sealed class ChiefOfStaffProfileTests
{
    [Fact]
    public void Profile_UsesThirdPartyIdentityAndCompatibleConversationContract()
    {
        Assert.Equal("com.csweet.chief-of-staff", ChiefOfStaffProfile.AgentId);
        Assert.Equal("assistant.converse.v1", ChiefOfStaffProfile.ConverseCapability);
        Assert.Equal("com.csweet.user.message.received.v1", ChiefOfStaffProfile.UserMessageReceivedEvent);
        Assert.Equal("com.csweet.assistant.response.chunk.v1", ChiefOfStaffProfile.AssistantResponseChunkEvent);
    }

    [Fact]
    public void RootManifest_UsesImporterCompatibleActivationMode()
    {
        var manifestPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "csweet-agent.json"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.Equal(
            "AlwaysOn",
            manifest.RootElement
                .GetProperty("runtime")
                .GetProperty("defaultActivationMode")
                .GetString());
    }
}
