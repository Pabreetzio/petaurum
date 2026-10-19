using System.Runtime.InteropServices;
using Windows.Security.Credentials;

namespace Petaurum.Windows.Router;

/// <summary>
/// The router's device access code, kept only in the Windows Credential Locker, never in files, logs or
/// environment variables. See specs/features/router.md, "Access code".
/// </summary>
internal static class AccessCodeStore
{
    private const string Resource = "Petaurum router";

    public static string? Get(string routerId)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, routerId);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (COMException)
        {
            return null; // Not stored.
        }
    }

    public static void Set(string routerId, string code)
    {
        Remove(routerId);
        new PasswordVault().Add(new PasswordCredential(Resource, routerId, code));
    }

    public static void Remove(string routerId)
    {
        var vault = new PasswordVault();
        try
        {
            vault.Remove(vault.Retrieve(Resource, routerId));
        }
        catch (COMException)
        {
            // Not stored.
        }
    }
}
