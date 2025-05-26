using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DormGO.DTOs.Enums;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipType
{
    [Description("Posts that current user joined")]
    Joined,
    [Description("Posts that current user owns")]
    Own,
    [Description("Posts that current user hasn't joined nor owned")]
    NotJoined
}