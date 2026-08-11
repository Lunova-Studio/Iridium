using System.Text.Json.Serialization;
using Iridium.Enums;

namespace Iridium.Models.Authentication;

[JsonDerivedType(typeof(OfflineAccount), "offline")]
[JsonDerivedType(typeof(MicrosoftAccount), "microsoft")]
[JsonDerivedType(typeof(YggdrasilAccount), "yggdrasil")]
public abstract record Account(string Name, Guid Uuid, string AccessToken) {
    public abstract AccountType Type { get; }
    
    public virtual bool ProfileEquals(Account other) {
        return Type == other.Type
               && Uuid == other.Uuid
               && Name == other.Name;
    }
}

public sealed record MicrosoftAccount(
    string Name,
    Guid Uuid,
    string AccessToken,
    string RefreshToken, 
    DateTime LastRefreshTime) : Account(Name, Uuid, AccessToken) {
    public override AccountType Type => AccountType.Microsoft;
    
    public override bool ProfileEquals(Account other) {
        return other is MicrosoftAccount account
               && account.Uuid == Uuid;
    }
}

public sealed record YggdrasilAccount(
    string Name,
    Guid Uuid,
    string AccessToken,
    string YggdrasilServerUrl,
    string ClientToken) : Account(Name, Uuid, AccessToken) {
    public override AccountType Type => AccountType.Yggdrasil;

    public Dictionary<string,string> MetaData { get; init; } = [];
    
    public override bool ProfileEquals(Account other) {
        return other is YggdrasilAccount account
               && account.Uuid == Uuid
               && account.YggdrasilServerUrl == YggdrasilServerUrl;
    }
}

public record OfflineAccount(string Name, Guid Uuid, string AccessToken) : Account(Name, Uuid, AccessToken) {
    public override AccountType Type => AccountType.Offline;
}