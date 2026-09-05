namespace AppCatalog.Api.Data;

/// <summary>
/// Paramètres de connexion à Neo4j. Alimentés par la configuration
/// (section « Neo4j ») ou par variables d'environnement issues d'un secret :
/// Neo4j__Uri, Neo4j__User, Neo4j__Password. Le mot de passe n'est jamais en dur.
/// </summary>
public class Neo4jSettings
{
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string User { get; set; } = "neo4j";
    public string Password { get; set; } = string.Empty;
}
