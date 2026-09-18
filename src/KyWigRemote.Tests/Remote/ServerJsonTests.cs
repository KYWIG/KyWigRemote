using System.Text.Json;
using System.Text.Json.Serialization;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Tests.Remote;

/// <summary>
/// Vérifie que l'arborescence sérialisée par le serveur (options « web » + enums en texte)
/// se redésérialise côté client SANS perdre les collections imbriquées
/// (SubFolders / Connections sont en lecture seule : piège classique de System.Text.Json).
/// </summary>
public class ServerJsonTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void ArborescenceSerialiseePuisDeserialisee_ConserveDossiersEtConnexions()
    {
        var racine = new ConnectionFolder { Id = 1, Name = "KyWig", CredentialMode = CredentialMode.Personal };
        var sous = new ConnectionFolder { Id = 2, Name = "Serveurs", CredentialMode = CredentialMode.Inherited };
        sous.Connections.Add(new RemoteConnection
        {
            Id = 10, Name = "Serveur (démo)", Protocol = RemoteProtocol.Rdp, Host = "192.0.2.14",
            Port = 3389, CredentialMode = CredentialMode.Personal,
        });
        racine.SubFolders.Add(sous);

        string json = JsonSerializer.Serialize(new List<ConnectionFolder> { racine }, Options);
        List<ConnectionFolder>? roundTrip = JsonSerializer.Deserialize<List<ConnectionFolder>>(json, Options);

        Assert.NotNull(roundTrip);
        ConnectionFolder kywig = Assert.Single(roundTrip!);
        Assert.Equal("KyWig", kywig.Name);
        ConnectionFolder serveurs = Assert.Single(kywig.SubFolders);
        RemoteConnection connection = Assert.Single(serveurs.Connections);
        Assert.Equal("Serveur (démo)", connection.Name);
        Assert.Equal(RemoteProtocol.Rdp, connection.Protocol);
    }
}
