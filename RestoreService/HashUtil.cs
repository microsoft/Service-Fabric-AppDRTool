using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RestoreService
{
    public class HashUtil
    {
        public static long getLongHashCode(string stringInput)
        {
            byte[] byteContents = Encoding.Unicode.GetBytes(stringInput);
            MD5CryptoServiceProvider hash = new MD5CryptoServiceProvider();
            byte[] hashText = hash.ComputeHash(byteContents);
            return BitConverter.ToInt64(hashText, 0) ^ BitConverter.ToInt64(hashText, 7);
        }

        public static int getIntHashCode(string stringInput)
        {
            return (int)getLongHashCode(stringInput);
        }
    }
}
