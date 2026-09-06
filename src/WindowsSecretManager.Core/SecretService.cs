using System.Security;

namespace WindowsSecretManager.Core;

public sealed class SecretService
{
    private readonly ISecretStore _store;
    public SecretService(ISecretStore store) => _store = store;

    public IReadOnlyList<string> List() => _store.ListNames()
        .Where(SecretNames.IsSupported)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    public string Save(string name, SecureString value, bool overwrite)
    {
        var normalized = SecretNames.Normalize(name);
        SecretNames.Validate(normalized);
        if (value.Length == 0) throw new ArgumentException("Sekret nie może być pusty.", nameof(value));
        if (value.Length > 2560) throw new ArgumentException("Sekret jest zbyt długi (maksymalnie 2560 znaków).", nameof(value));
        if (!overwrite && _store.Exists(normalized)) throw new InvalidOperationException("Wpis o tej nazwie już istnieje.");
        _store.Save(normalized, value);
        return normalized;
    }

    public SecureString Read(string name) { SecretNames.Validate(name); return _store.Read(name); }
    public bool Delete(string name) { SecretNames.Validate(name); return _store.Delete(name); }

    public string Rename(string oldName, string newName)
    {
        SecretNames.Validate(oldName);
        newName = newName.Trim();
        SecretNames.Validate(newName);
        if (string.Equals(oldName, newName, StringComparison.Ordinal)) return oldName;
        if (_store.Exists(newName)) throw new InvalidOperationException("Wpis o nowej nazwie już istnieje.");

        using var value = _store.Read(oldName);
        _store.Save(newName, value);
        try
        {
            if (!_store.Delete(oldName)) throw new InvalidOperationException("Nie znaleziono wpisu o starej nazwie.");
        }
        catch
        {
            _store.Delete(newName);
            throw;
        }
        return newName;
    }

    public string Update(string oldName, string newName, SecureString value)
    {
        SecretNames.Validate(oldName);
        newName = newName.Trim();
        SecretNames.Validate(newName);
        if (value.Length == 0) throw new ArgumentException("Sekret nie może być pusty.", nameof(value));
        if (value.Length > 2560) throw new ArgumentException("Sekret jest zbyt długi (maksymalnie 2560 znaków).", nameof(value));

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            _store.Save(oldName, value);
            return oldName;
        }
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            var temporaryName = oldName + ".case-change-" + Guid.NewGuid().ToString("N");
            _store.Save(temporaryName, value);
            try
            {
                if (!_store.Delete(oldName)) throw new InvalidOperationException("Nie znaleziono wpisu o starej nazwie.");
                _store.Save(newName, value);
                _store.Delete(temporaryName);
                return newName;
            }
            catch
            {
                if (!_store.Exists(oldName)) _store.Save(oldName, value);
                _store.Delete(temporaryName);
                throw;
            }
        }
        if (_store.Exists(newName)) throw new InvalidOperationException("Wpis o nowej nazwie już istnieje.");

        _store.Save(newName, value);
        try
        {
            if (!_store.Delete(oldName)) throw new InvalidOperationException("Nie znaleziono wpisu o starej nazwie.");
        }
        catch
        {
            _store.Delete(newName);
            throw;
        }
        return newName;
    }
}
