using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;
internal class GetResourceResponse
{
    [JsonPropertyName("identifier")]
    public string Identifier;

    [JsonPropertyName("title")]
    public Title Title;

    [JsonPropertyName("description")]
    public Description Description;

    [JsonPropertyName("rightDescription")]
    public RightDescription RightDescription;

    [JsonPropertyName("homepage")]
    public string Homepage;

    [JsonPropertyName("status")]
    public string Status;

    [JsonPropertyName("contactPoints")]
    public List<object> ContactPoints;

    [JsonPropertyName("isPartOf")]
    public string IsPartOf;

    [JsonPropertyName("resourceReferences")]
    public List<object> ResourceReferences;

    [JsonPropertyName("delegable")]
    public bool? Delegable;

    [JsonPropertyName("visible")]
    public bool? Visible;

    [JsonPropertyName("hasCompetentAuthority")]
    public HasCompetentAuthority HasCompetentAuthority;

    [JsonPropertyName("keywords")]
    public List<object> Keywords;

    [JsonPropertyName("limitedByRRR")]
    public bool? LimitedByRRR;

    [JsonPropertyName("selfIdentifiedUserEnabled")]
    public bool? SelfIdentifiedUserEnabled;

    [JsonPropertyName("enterpriseUserEnabled")]
    public bool? EnterpriseUserEnabled;

    [JsonPropertyName("resourceType")]
    public string ResourceType;
}
internal class Description
{
    [JsonPropertyName("en")]
    public string En;

    [JsonPropertyName("nb-no")]
    public string NbNo;

    [JsonPropertyName("nn-no")]
    public string NnNo;
}

internal class HasCompetentAuthority
{
    [JsonPropertyName("organization")]
    public string Organization;

    [JsonPropertyName("orgcode")]
    public string Orgcode;

    [JsonPropertyName("name")]
    public Name Name;
}

internal class Name
{
    [JsonPropertyName("en")]
    public string En;

    [JsonPropertyName("nb-no")]
    public string NbNo;

    [JsonPropertyName("nn-no")]
    public string NnNo;
}

internal class RightDescription
{
    [JsonPropertyName("en")]
    public string En;

    [JsonPropertyName("nb-no")]
    public string NbNo;

    [JsonPropertyName("nn-no")]
    public string NnNo;
}


internal class Title
{
    [JsonPropertyName("en")]
    public string En;

    [JsonPropertyName("nb-no")]
    public string NbNo;

    [JsonPropertyName("nn-no")]
    public string NnNo;
}


