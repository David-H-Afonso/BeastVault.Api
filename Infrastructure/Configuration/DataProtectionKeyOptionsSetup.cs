using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace BeastVault.Api.Infrastructure.Configuration;

public sealed class DataProtectionKeyOptionsSetup(
    StorageConfiguration storage,
    ILoggerFactory loggerFactory) : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options)
    {
        Directory.CreateDirectory(storage.DataProtectionKeysDirectory);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    storage.DataProtectionKeysDirectory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        options.XmlRepository = new FileSystemXmlRepository(
            new DirectoryInfo(storage.DataProtectionKeysDirectory),
            loggerFactory);
    }
}
