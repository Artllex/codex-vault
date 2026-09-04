using System.Security;

namespace WindowsSecretManager.Core;

public interface ISecretStore
{
    IReadOnlyList<string> ListNames();
    bool Exists(string name);
    void Save(string name, SecureString value);
    SecureString Read(string name);
    bool Delete(string name);
}
