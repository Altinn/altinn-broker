using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;

internal class GetResourceResponse
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; }

    [JsonPropertyName("title")]
    public Title Title { get; set; }

    [JsonPropertyName("description")]
    public Description Description { get; set; }

    [JsonPropertyName("rightDescription")]
    public RightDescription RightDescription { get; set; }

    [JsonPropertyName("homepage")]
    public string Homepage { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("contactPoints")]
    public List<object> ContactPoints { get; set; }

    [JsonPropertyName("isPartOf")]
    public string IsPartOf { get; set; }

    [JsonPropertyName("resourceReferences")]
    public List<object> ResourceReferences { get; set; }

    [JsonPropertyName("delegable")]
    public bool? Delegable { get; set; }

    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }

    [JsonPropertyName("hasCompetentAuthority")]
    public HasCompetentAuthority HasCompetentAuthority { get; set; }

    [JsonPropertyName("keywords")]
    public List<object> Keywords { get; set; }

    [JsonPropertyName("limitedByRRR")]
    public bool? LimitedByRRR { get; set; }

    [JsonPropertyName("selfIdentifiedUserEnabled")]
    public bool? SelfIdentifiedUserEnabled { get; set; }

    [JsonPropertyName("enterpriseUserEnabled")]
    public bool? EnterpriseUserEnabled { get; set; }

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; }
}
internal class Description
{
    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("nb-no")]
    public string NbNo { get; set; }

    [JsonPropertyName("nn-no")]
    public string NnNo { get; set; }
}

internal class HasCompetentAuthority
{
    [JsonPropertyName("organization")]
    public string Organization { get; set; }

    [JsonPropertyName("orgcode")]
    public string Orgcode { get; set; }

    [JsonPropertyName("name")]
    public Name Name { get; set; }
}

internal class Name
{
    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("nb-no")]
    public string NbNo { get; set; }

    [JsonPropertyName("nn-no")]
    public string NnNo { get; set; }
}

internal class RightDescription
{
    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("nb-no")]
    public string NbNo { get; set; }

    [JsonPropertyName("nn-no")]
    public string NnNo { get; set; }
}


internal class Title
{
    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("nb-no")]
    public string NbNo { get; set; }

    [JsonPropertyName("nn-no")]
    public string NnNo { get; set; }
}


