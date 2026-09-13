using System.Runtime.InteropServices;
using System.Security;
using WindowsSecretManager.Core;

var tests = new (string Name, Action Run)[]
{
    ("Normalize adds namespace", () => Equal("Codex.Shared/AI/Test", SecretNames.Normalize("AI/Test"))),
    ("Normalize keeps namespace", () => Equal("Codex.Shared/AI/Test", SecretNames.Normalize("Codex.Shared/AI/Test"))),
    ("Invalid empty suffix rejected", () => Throws<ArgumentException>(() => SecretNames.Validate("Codex.Shared/"))),
    ("Shared scope accepted", () => SecretNames.Validate("SharedSecrets/AI/Key")),
    ("Application scope accepted", () => SecretNames.Validate("Acme.Mail/ImapPassword")),
    ("Recovery-code scope accepted", () => SecretNames.Validate("RecoveryCodes/GitHub")),
    ("List filters and sorts", TestList),
    ("Save refuses duplicate", TestDuplicate),
    ("Rotate replaces secret", TestRotate),
    ("Delete removes secret", TestDelete),
    ("Empty secret rejected", TestEmpty)
    ,("Rename moves secret", TestRename)
    ,("Rename refuses duplicate target", TestRenameDuplicate)
    ,("Update changes name and value", TestUpdate)
    ,("Update permits case-only rename", TestCaseOnlyUpdate)
    ,("Password-protected archive round-trips", TestProtectedArchive)
    ,("Unprotected archive round-trips", TestUnprotectedArchive)
    ,("Archive rejects a wrong password", TestWrongArchivePassword)
};

var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.GetType().Name} - {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
return failed;

void TestList()
{
    var store = new MemoryStore();
    using var value = Secure("x");
    store.Save("Other/App", value); store.Save("Codex.Shared/Z", value); store.Save("Codex.Shared/a", value);
    Sequence(new[] { "Codex.Shared/a", "Codex.Shared/Z" }, new SecretService(store).List());
}
void TestDuplicate() { var store = new MemoryStore(); var service = new SecretService(store); using var v = Secure("one"); service.Save("AI/Key", v, false); Throws<InvalidOperationException>(() => service.Save("AI/Key", v, false)); }
void TestRotate() { var store = new MemoryStore(); var service = new SecretService(store); using var a = Secure("a"); using var b = Secure("b"); service.Save("AI/Key", a, false); service.Save("AI/Key", b, true); using var read = service.Read("Codex.Shared/AI/Key"); Equal("b", Plain(read)); }
void TestDelete() { var store = new MemoryStore(); var service = new SecretService(store); using var v = Secure("x"); var n = service.Save("Test", v, false); if (!service.Delete(n) || store.Exists(n)) throw new Exception("Delete failed"); }
void TestEmpty() { using var empty = new SecureString(); Throws<ArgumentException>(() => new SecretService(new MemoryStore()).Save("Test", empty, false)); }
void TestRename() { var store = new MemoryStore(); var service = new SecretService(store); using var v = Secure("x"); service.Save("Old", v, false); var renamed = service.Rename("Codex.Shared/Old", "SharedSecrets/New"); Equal("SharedSecrets/New", renamed); if (store.Exists("Codex.Shared/Old") || !store.Exists(renamed)) throw new Exception("Rename did not move the entry"); }
void TestRenameDuplicate() { var store = new MemoryStore(); var service = new SecretService(store); using var v = Secure("x"); service.Save("One", v, false); service.Save("Two", v, false); Throws<InvalidOperationException>(() => service.Rename("Codex.Shared/One", "Codex.Shared/Two")); }
void TestUpdate() { var store = new MemoryStore(); var service = new SecretService(store); using var a = Secure("a"); using var b = Secure("b"); service.Save("Old", a, false); var updated = service.Update("Codex.Shared/Old", "SharedSecrets/New", b); Equal("SharedSecrets/New", updated); if (store.Exists("Codex.Shared/Old")) throw new Exception("Old entry remains"); using var read = service.Read(updated); Equal("b", Plain(read)); }
void TestCaseOnlyUpdate() { var store = new MemoryStore(StringComparer.OrdinalIgnoreCase); var service = new SecretService(store); using var a = Secure("a"); using var b = Secure("b"); service.Save("Firefox", a, false); var updated = service.Update("Codex.Shared/Firefox", "Codex.Shared/firefox", b); Equal("Codex.Shared/firefox", updated); using var read = service.Read(updated); Equal("b", Plain(read)); }
void TestProtectedArchive()
{
    var source = new[] { new VaultArchiveItem("Codex.Shared/AI/Key", "sekret-ą"), new VaultArchiveItem("RecoveryCodes/GitHub", "one\ntwo") };
    var bytes = VaultArchive.Create(source, "correct horse battery staple");
    try
    {
        if (!VaultArchive.IsPasswordProtected(bytes)) throw new Exception("Archive should be protected");
        Equal(2, VaultArchive.Inspect(bytes).ItemCount!.Value);
        var restored = VaultArchive.Open(bytes, "correct horse battery staple");
        Equal(2, restored.Items.Count);
        Equal(source[0], restored.Items[0]);
        Equal(source[1], restored.Items[1]);
    }
    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
}
void TestUnprotectedArchive()
{
    var source = new[] { new VaultArchiveItem("SharedSecrets/Test", "value") };
    var bytes = VaultArchive.Create(source, null);
    try
    {
        if (VaultArchive.IsPasswordProtected(bytes)) throw new Exception("Archive should not be protected");
        Equal(1, VaultArchive.Inspect(bytes).ItemCount!.Value);
        var restored = VaultArchive.Open(bytes, null);
        Equal(source[0], restored.Items[0]);
    }
    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
}
void TestWrongArchivePassword()
{
    var bytes = VaultArchive.Create(new[] { new VaultArchiveItem("Codex.Shared/Test", "value") }, "right-password");
    try { Throws<UnauthorizedAccessException>(() => VaultArchive.Open(bytes, "wrong-password")); }
    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
}
static SecureString Secure(string value) { var s = new SecureString(); foreach (var c in value) s.AppendChar(c); s.MakeReadOnly(); return s; }
static string Plain(SecureString value) { var p = Marshal.SecureStringToGlobalAllocUnicode(value); try { return Marshal.PtrToStringUni(p)!; } finally { Marshal.ZeroFreeGlobalAllocUnicode(p); } }
static void Equal<T>(T expected, T actual) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual) { if (!expected.SequenceEqual(actual)) throw new Exception("Sequences differ"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }

sealed class MemoryStore : ISecretStore
{
    private readonly Dictionary<string, SecureString> _items;
    public MemoryStore(IEqualityComparer<string>? comparer = null) => _items = new(comparer ?? StringComparer.Ordinal);
    public IReadOnlyList<string> ListNames() => _items.Keys.ToArray();
    public bool Exists(string name) => _items.ContainsKey(name);
    public void Save(string name, SecureString value) { if (_items.Remove(name, out var old)) old.Dispose(); _items[name] = value.Copy(); }
    public SecureString Read(string name) => _items.TryGetValue(name, out var value) ? value.Copy() : throw new KeyNotFoundException();
    public bool Delete(string name) { if (!_items.Remove(name, out var value)) return false; value.Dispose(); return true; }
}
