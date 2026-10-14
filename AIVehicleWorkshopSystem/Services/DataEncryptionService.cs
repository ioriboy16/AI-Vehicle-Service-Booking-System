using Microsoft.AspNetCore.DataProtection;

namespace AIVehicleWorkshopSystem.Services
{
    public class DataEncryptionService
    {
        private readonly IDataProtector _protector;

        public DataEncryptionService(
            IDataProtectionProvider dataProtectionProvider)
        {
            _protector =
                dataProtectionProvider.CreateProtector(
                    "SerishAutoCare.SensitiveData.v1");
        }

        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return plainText;
            }

            return _protector.Protect(plainText);
        }

        public string? Decrypt(string? encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
            {
                return encryptedText;
            }

            try
            {
                return _protector.Unprotect(encryptedText);
            }
            catch
            {
                // Allows existing unencrypted records
                // to continue working during migration.
                return encryptedText;
            }
        }
    }
}