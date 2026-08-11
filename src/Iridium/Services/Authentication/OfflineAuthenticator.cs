using System.Security.Cryptography;
using System.Text;
using Iridium.Models.Authentication;
using Iridium.Primitives;

namespace Iridium.Services.Authentication;

public sealed class OfflineAuthenticator {
    public OfflineAccount Authenticate(string name, Guid guid = default) {
        var uuid = guid;
        if (uuid == Guid.Empty)
            TryParseUuidFromName(name, out uuid);

        return new OfflineAccount(name, uuid, Guid.NewGuid().ToString("N"));
    }

    private static void TryParseUuidFromName(string name, out Guid uuid) {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));

        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

        uuid = Guid.Parse(new Uuid(hash).ToString());
    }
}