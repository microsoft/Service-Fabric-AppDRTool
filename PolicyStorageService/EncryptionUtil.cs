// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Remoting.Contexts;
using RestoreService;
using System.Fabric;
using System.Runtime.InteropServices;
using System.Security;

namespace PolicyStorageService
{
    public class EncryptionUtil
    {
        public static string Encrypt(string clearText)
        {
            var thumbprint = GetCertThumbprint();
            return System.Fabric.Security.EncryptionUtility.EncryptText(clearText, thumbprint, "My");
        }

        public static string Decrypt(string cipherText)
        {
            // TODO: Make users of Decrypt use SecureString instead
            return SecureStringToString(System.Fabric.Security.EncryptionUtility.DecryptText(cipherText));
        }

        public static string SecureStringToString(SecureString value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public static string GetCertThumbprint()
        {
            return Utility.GetConfigValue("PolicyStorageSecurityConfig", "PolicyStorageCertThumbprint");
        }
    }
}
