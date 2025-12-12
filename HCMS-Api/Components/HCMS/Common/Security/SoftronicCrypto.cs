using System.Security.Cryptography;
using System.Text;

namespace HCMS_Api.Components.HCMS.Common.Security
{
    public class SoftronicCrypto
    {
        #region secret
        private const string initVector = "CinOrTFosSmeTsyS";
        private const int keySize = 192;
        private const int passwordIterations = 7;
        private const string hashAlgorithm = "SHA1";


        #endregion secret

        //public static string Encrypt(string plainText, string passPhrase, string saltValue)
        //{
        //    byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        //    byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
        //    byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

        //    using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passPhrase, saltValueBytes, passwordIterations, HashAlgorithmName.SHA1))
        //    {
        //        byte[] keyBytes = password.GetBytes(keySize / 8);

        //        using (AesManaged symmetricKey = new AesManaged { Mode = CipherMode.CBC })
        //        {
        //            using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
        //            {
        //                using (MemoryStream memoryStream = new MemoryStream())
        //                {
        //                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        //                    {
        //                        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
        //                        cryptoStream.FlushFinalBlock();

        //                        byte[] cipherTextBytes = memoryStream.ToArray();
        //                        string cipherText = Convert.ToBase64String(cipherTextBytes);
        //                        return cipherText;
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        public static string Encrypt(string plainText, string passPhrase, string saltValue)
        {
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
            byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, saltValueBytes, hashAlgorithm, passwordIterations))
            {
                byte[] keyBytes = password.GetBytes(keySize / 8);

                using (RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC })
                {
                    using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();

                                byte[] cipherTextBytes = memoryStream.ToArray();
                                string cipherText = Convert.ToBase64String(cipherTextBytes);
                                return cipherText;
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// /
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="saltValue"></param>
        /// <returns></returns>
        public static string DecryptPlain(string cipherText, string passPhrase, string saltValue)
        {
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
            byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

            using (var password = new PasswordDeriveBytes(passPhrase, saltValueBytes, hashAlgorithm, passwordIterations))
            {
                byte[] keyBytes = password.GetBytes(keySize / 8);

                using (var symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Key = keyBytes;
                    symmetricKey.IV = initVectorBytes;

                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                using (var streamReader = new StreamReader(cryptoStream))
                                {
                                    return streamReader.ReadToEnd();
                                }
                            }
                        }
                    }
                }
            }
        }

        //public static string DecryptPlain(string cipherText, string passPhrase, string saltValue)
        //{
        //    byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        //    byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
        //    byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

        //    using (var password = new PasswordDeriveBytes(passPhrase, saltValueBytes, hashAlgorithm, passwordIterations))
        //    {
        //        byte[] keyBytes = password.GetBytes(keySize / 8);

        //        using (var symmetricKey = new RijndaelManaged())
        //        {
        //            symmetricKey.Mode = CipherMode.CBC;
        //            symmetricKey.Key = keyBytes;
        //            symmetricKey.IV = initVectorBytes;

        //            using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
        //            {
        //                using (var memoryStream = new MemoryStream(cipherTextBytes))
        //                {
        //                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
        //                    {
        //                        byte[] plainTextBytes = new byte[cipherTextBytes.Length];
        //                        int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

        //                        return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}



        //public static string EncryptPlain(string plainText, string passPhrase, string saltValue)
        //{
        //    byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        //    byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
        //    byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

        //    using (var password = new Rfc2898DeriveBytes(passPhrase, saltValueBytes, passwordIterations))
        //    {
        //        byte[] keyBytes = password.GetBytes(keySize / 8);

        //        using (var symmetricKey = new AesCryptoServiceProvider())
        //        {
        //            symmetricKey.Mode = CipherMode.CBC;
        //            symmetricKey.Padding = PaddingMode.PKCS7;

        //            using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
        //            using (var memoryStream = new MemoryStream())
        //            {
        //                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
        //                {
        //                    cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
        //                    cryptoStream.FlushFinalBlock();
        //                }

        //                byte[] cipherTextBytes = memoryStream.ToArray();
        //                return Convert.ToBase64String(cipherTextBytes);
        //            }
        //        }
        //    }
        //}

        //public static string Decrypt(string cipherText, string passPhrase, string saltValue)
        //{
        //    try
        //    {
        //        byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        //        byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
        //        byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

        //        using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passPhrase, saltValueBytes, passwordIterations, HashAlgorithmName.SHA1))
        //        {
        //            byte[] keyBytes = password.GetBytes(keySize / 8);

        //            using (AesManaged symmetricKey = new AesManaged { Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
        //            {
        //                using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
        //                {
        //                    using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
        //                    {
        //                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
        //                        {
        //                            using (StreamReader reader = new StreamReader(cryptoStream))
        //                            {
        //                                return reader.ReadToEnd();
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (CryptographicException ex)
        //    {
        //        // Handle cryptographic exception (e.g., log, throw, return null)
        //        // You can also inspect the exception details for more specific error information
        //        Console.WriteLine($"CryptographicException: {ex.Message}");
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle other exceptions
        //        Console.WriteLine($"Exception: {ex.Message}");
        //        return null;
        //    }
        //}


        public static string Decrypt(string cipherText, string passPhrase, string saltValue)
        {
            try
            {
                byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
                byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
                //
                using (PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, saltValueBytes, hashAlgorithm, passwordIterations))
                {
                    byte[] keyBytes = password.GetBytes(keySize / 8);

                    using (RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC })
                    {
                        using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                        {
                            using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
                            {
                                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                                {
                                    using (StreamReader reader = new StreamReader(cryptoStream))
                                    {
                                        return reader.ReadToEnd();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // Handle cryptographic exception (e.g., log, throw, return null)
                // You can also inspect the exception details for more specific error information
                Console.WriteLine($"CryptographicException: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }




        //public static string Decrypt(string cipherText, string passPhrase, string saltValue)
        //{
        //    byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
        //    byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
        //    byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

        //    using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passPhrase, saltValueBytes, passwordIterations, HashAlgorithmName.SHA1))
        //    {
        //        byte[] keyBytes = password.GetBytes(keySize / 8);

        //        using (AesManaged symmetricKey = new AesManaged { Mode = CipherMode.CBC })
        //        {
        //            using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
        //            {
        //                using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
        //                {
        //                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
        //                    {
        //                        using (StreamReader reader = new StreamReader(cryptoStream))
        //                        {
        //                            return reader.ReadToEnd();
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
