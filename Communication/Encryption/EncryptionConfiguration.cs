namespace Plus.Communication.Encryption;

/// <summary>
/// "Encryption" section of config.json. The handshake's RSA private key never lives in the repository: it is read from
/// the PEM file named here or by the PLUS_RSA_PRIVATE_KEY_FILE environment variable, or from the PEM text in
/// PLUS_RSA_PRIVATE_KEY. Create one with <c>"Plus Emulator" --generate-rsa-key Config/rsa-private.pem</c>.
/// </summary>
public class EncryptionConfiguration
{
    public const string PrivateKeyVariable = "PLUS_RSA_PRIVATE_KEY";
    public const string PrivateKeyFileVariable = "PLUS_RSA_PRIVATE_KEY_FILE";

    /// <summary>PEM private key file; a relative path is relative to the Config directory.</summary>
    public string RsaPrivateKeyFile { get; set; } = string.Empty;
}
