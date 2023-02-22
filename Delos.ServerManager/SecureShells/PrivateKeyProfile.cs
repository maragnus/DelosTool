using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using LiteDB;
using Renci.SshNet;

namespace Delos.ServerManager.SecureShells;

[PublicAPI]
public record PrivateKeyProfile(ObjectId? Id, string Name, string? Type, string? PrivateKey)
{
    public PrivateKeyProfile() : this(null, "", null, null)
    {
    }

    public string ToRfcPublicKey2()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKey);
        return Convert.ToBase64String(rsa.ExportRSAPublicKey());
    }
    
    public string ToRfcPublicKey()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKey);
        
        using MemoryStream ms = new MemoryStream();
        var prefix = Encoding.Default.GetBytes("ssh-rsa");
        ms.Write(ToBytes(prefix.Length), 0, 4);
        ms.Write(prefix, 0, prefix.Length);

        var e = rsa.ExportParameters(false).Exponent!;
        ms.Write(ToBytes(e.Length), 0, 4);
        ms.Write(e, 0, e.Length);
        
        var n = rsa.ExportParameters(false).Modulus!;
        ms.Write(ToBytes(n.Length + 1), 0, 4); //Remove the +1 if not Emulating Putty Gen
        ms.Write(new byte[] {0}, 0, 1); //Add a 0 to Emulate PuttyGen
        ms.Write(n, 0, n.Length);
        
        ms.Flush();
        return Convert.ToBase64String(ms.ToArray());

        byte[] ToBytes(int i)
        {
            var bts = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bts);
            return bts;
        }
    }

    public PrivateKeyFile ToPrivateKeyFile()
    {
        using var memory = new MemoryStream(Encoding.UTF8.GetBytes(PrivateKey!));
        return new PrivateKeyFile(memory);
    }
};