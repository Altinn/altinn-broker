﻿using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Models.ResourceOwner;

public class ResourceOwnerInitializeExt
{
    /// <summary>
    /// This should be on the form countrycode:organizationnumber. For instance 0192:922444555 for a Norwegian organization with org number 923 444 555. Corresponds to consumer.id in Maskinporten token.
    /// </summary>
    [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "ResourceOwnerId should be on the Maskinporten form with countrycode:organizationnumber, for instance 0192:910753614")]
    public string OrganizationId { get; set; }
    public string Name { get; set; }
    public string DeletionTime { get; set; } // ISO8601 Duration
}