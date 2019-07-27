// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading.Tasks;
using System.Net.Http;
using System.Fabric;
using System.Net.Http.Headers;

namespace RestoreService
{
    public class Utility
    {
        public static object CloneObject(object objSource)
        {
            //step : 1 Get the type of source object and create a new instance of that type
            Type typeSource = objSource.GetType();
            object objTarget = Activator.CreateInstance(typeSource);
            //Step2 : Get all the properties of source object type
            PropertyInfo[] propertyInfo = typeSource.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            //Step : 3 Assign all source property to taget object 's properties
            foreach (PropertyInfo property in propertyInfo)
            {
                //Check whether property can be written to
                if (property.CanWrite)
                {
                    //Step : 4 check whether property type is value type, enum or string type
                    if (property.PropertyType.IsValueType || property.PropertyType.IsEnum || property.PropertyType.Equals(typeof(System.String)))
                    {
                        property.SetValue(objTarget, property.GetValue(objSource, null), null);
                    }
                    //else property type is object/complex types, so need to recursively call this method until the end of the tree is reached
                    else
                    {
                        object objPropertyValue = property.GetValue(objSource, null);
                        if (objPropertyValue == null)
                        {
                            property.SetValue(objTarget, null, null);
                        }
                        else
                        {
                            property.SetValue(objTarget, CloneObject(objPropertyValue), null);
                        }
                    }
                }
            }
            return objTarget;
        }

        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        public static String getPartitionAccessKey(Guid partitionId, String primaryClusterName, String secondaryClusterName)
        {
            return getPrimarySecondaryClusterJoin(primaryClusterName, secondaryClusterName) + "~" + partitionId.ToString();
        }

        public static bool isPartitionFromPrimarySecondaryCombination(String partitionAccessKey, String primaryClusterName, String secondaryClusterName)
        {
            string[] parts = partitionAccessKey.Split('~');
            String psc = getPrimarySecondaryClusterJoin(primaryClusterName, secondaryClusterName);
            return psc.Equals(parts[0]);
        }

        public static String getPrimarySecondary(String partitionAccessKey)
        {
            return partitionAccessKey.Split('~')[0];
        }

        public static String getPrimarySecondaryClusterJoin(String primaryClusterName, String secondaryClusterName)
        {
            return primaryClusterName + ":" + secondaryClusterName;
        }

        public static String getClusterNameFromTCPEndpoint(String clusterEndpoint)
        {
            return clusterEndpoint.Split(':')[0];
        }

        public static String decodeHTTPString(String encoded)
        {
            return encoded.Replace("__", "//");
        }

        public static async Task<HttpResponseMessage> HTTPGetAsync(String URL, String URLParameters, String certThumbprint = null)
        {
            HttpClient client;

            if (URL.Contains("https://") && certThumbprint != null && certThumbprint != "NotExist" && certThumbprint != "WindowsCredentials")
            {
                X509Certificate2 clientCert = GetClientCertificate(certThumbprint);
                WebRequestHandler requestHandler = new WebRequestHandler();
                requestHandler.ClientCertificates.Add(clientCert);
                requestHandler.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
                client = new HttpClient(requestHandler);
            }
            else
            {
                client = certThumbprint == "WindowsCredentials" ? new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true }) : new HttpClient();
            }

            client.BaseAddress = new Uri(URL);

            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(URLParameters);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occured: " + e.Message);
                return null;
            }

            return response;
        }
        
        public static async Task<HttpResponseMessage> HTTPPostAsync<T>(String URL, String URLParameters, T value, String certThumbprint = null)
        {
            HttpClient client;

            if (URL.Contains("https://") && certThumbprint != null && certThumbprint != "NotExist" && certThumbprint != "WindowsCredentials")
            {
                X509Certificate2 clientCert = GetClientCertificate(certThumbprint);
                WebRequestHandler requestHandler = new WebRequestHandler();
                requestHandler.ClientCertificates.Add(clientCert);
                requestHandler.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
                client = new HttpClient(requestHandler);
            }
            else
            {
                client = certThumbprint == "WindowsCredentials" ? new HttpClient(new HttpClientHandler() { UseDefaultCredentials = true }) : new HttpClient();
            }

            client.BaseAddress = new Uri(URL);

            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync(URLParameters, value);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occured: " + e.Message);
                return null;
            }

            return response;
        }


        public static X509Credentials GetCredentials(string clientCertThumb, string serverCertThumb, string name)
        {
            X509Credentials xc = new X509Credentials();
            xc.StoreLocation = StoreLocation.LocalMachine;
            xc.StoreName = "My";
            xc.FindType = X509FindType.FindByThumbprint;
            xc.FindValue = clientCertThumb;
            xc.RemoteCommonNames.Add(name);
            xc.RemoteCertThumbprints.Add(serverCertThumb);
            xc.ProtectionLevel = System.Fabric.ProtectionLevel.EncryptAndSign;
            return xc;
        }

        public static X509Certificate2 GetClientCertificate(String Thumbprint)
        {
            X509Store userCaStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                userCaStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certificatesInStore = userCaStore.Certificates;
                X509Certificate2Collection findResult = certificatesInStore.Find(X509FindType.FindByThumbprint, Thumbprint, false);
                X509Certificate2 clientCertificate = null;

                if (findResult.Count == 1)
                {
                    clientCertificate = findResult[0];
                }
                else
                {
                    throw new Exception("Unable to locate the correct client certificate.");
                }
                return clientCertificate;
            }
            catch
            {
                throw;
            }
            finally
            {
                userCaStore.Close();
            }
        }


        private static bool MyRemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static FabricClient GetFabricClient(string connectionEndpoint, string thumbprint = null, string cname = null)
        {

            FabricClient fc;

            try
            {
                if (thumbprint != null && cname != null && thumbprint != "NotExist" && cname != "NotExist" && thumbprint != "WindowsCredentials" && cname != "WindowsCredentials")
                {
                    var xc = GetCredentials(thumbprint, thumbprint, cname);
                    fc = new FabricClient(xc, connectionEndpoint);
                }
                else
                {
                    fc = thumbprint == "WindowsCredentials" ? new FabricClient(new WindowsCredentials(), connectionEndpoint) : new FabricClient(connectionEndpoint);
                }

                return fc;
            }

            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            
        }

        public static string GetConfigValue(string sectionName, string paramName)
        {
            CodePackageActivationContext activationContext = FabricRuntime.GetActivationContext();
            var configurationPackage = activationContext.GetConfigurationPackageObject("Config");

            string configValue = configurationPackage.Settings.Sections[sectionName].Parameters[paramName].Value;

            return configValue;
        }

        public static long GetRestoreFrequencyPeriod()
        {
            return Convert.ToInt64(GetConfigValue("RestoreDataFrequencyPeriodConfig", "RestoreDataFrequencyPeriod"));
        }
    }
}
