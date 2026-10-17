using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Plus.Communication.Encryption.Keys;

public interface IHandshakeRsaKeyProvider
{
    HandshakeRsaKey Key { get; }

    /// <summary>False while the emulator runs on the built-in fallback key.</summary>
    bool IsConfigured { get; }
}

/// <summary>
/// Loads the handshake key once: PLUS_RSA_PRIVATE_KEY (PEM text), then PLUS_RSA_PRIVATE_KEY_FILE, then
/// Encryption:RsaPrivateKeyFile. Without any of them it falls back to the public 1024-bit key with exponent 3 that
/// older PlusEMU releases built in. The AIR client cannot use that key: it hard-codes exponent 10001 and Habbo's
/// modulus (AIR WIN63-202610151745-318700621, communication/demo/class_1974.as:141), so an AIR launch needs a
/// configured key and the launcher's patched modulus.
/// </summary>
public sealed class HandshakeRsaKeyProvider : IHandshakeRsaKeyProvider
{
    private readonly Lazy<(HandshakeRsaKey Key, bool Configured)> _key;

    public HandshakeRsaKeyProvider(IOptions<EncryptionConfiguration> options, ILogger<HandshakeRsaKeyProvider> logger)
        : this(options.Value, Environment.GetEnvironmentVariable, logger) { }

    internal HandshakeRsaKeyProvider(EncryptionConfiguration configuration, Func<string, string?> environment, ILogger logger)
    {
        _key = new(() => Load(configuration, environment, logger));
    }

    public HandshakeRsaKey Key => _key.Value.Key;
    public bool IsConfigured => _key.Value.Configured;

    private static (HandshakeRsaKey, bool) Load(EncryptionConfiguration configuration, Func<string, string?> environment, ILogger logger)
    {
        var (pem, source) = ReadPem(configuration, environment);

        if (pem == null) {
            var legacy = new RsaKeys();
            logger.LogWarning("No RSA private key is configured; the handshake uses the built-in legacy key (exponent 3), which the AIR client rejects. " +
                              "Set Encryption:RsaPrivateKeyFile or {Variable}.", EncryptionConfiguration.PrivateKeyFileVariable);

            return (HandshakeRsaKey.FromHex(legacy.N, legacy.E, legacy.D), false);
        }

        var key = HandshakeRsaKey.FromPem(pem);

        if (!key.UsesAirExponent) {
            logger.LogWarning("The RSA key from {Source} has public exponent {Exponent}; the AIR client only accepts 65537.", source, key.PublicExponent);
        }

        logger.LogInformation("Handshake RSA key loaded from {Source} ({Bits} bits).", source, key.ModulusBits);

        return (key, true);
    }

    private static (string? Pem, string Source) ReadPem(EncryptionConfiguration configuration, Func<string, string?> environment)
    {
        if (environment(EncryptionConfiguration.PrivateKeyVariable) is { Length: > 0 } inline) {
            return (inline, EncryptionConfiguration.PrivateKeyVariable);
        }

        var file = environment(EncryptionConfiguration.PrivateKeyFileVariable) is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : configuration.RsaPrivateKeyFile;

        if (string.IsNullOrWhiteSpace(file)) {
            return (null, string.Empty);
        }

        var path = Path.IsPathRooted(file) ? file : Path.Join(Directory.GetCurrentDirectory(), "Config", file);

        return (File.ReadAllText(path), path);
    }
}
