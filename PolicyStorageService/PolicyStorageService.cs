using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PolicyStorageService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class PolicyStorageService : StatefulService,IPolicyStorageService
    {
        public PolicyStorageService(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context => this.CreateServiceRemotingListener(context))
            };
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            
        }

        /// <summary>
        /// This is called by Restore Service to and it stores the storage details along with the policy details in the reliable dictionary
        /// </summary>
        /// <param name="policies"></param>
        /// <param name="primaryClusterConnectionString"></param>
        /// <returns></returns>
        public async Task<bool> PostStorageDetails(List<PolicyStorageEntity> policies, string primaryClusterConnectionString)
        {
            IReliableDictionary<string, BackupStorage> myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BackupStorage>>("storageDictionary");
            foreach (var entity in policies)
            {
                BackupStorage backupStorage;
                try
                {
                    backupStorage = await GetStorageInfo(entity.policy, primaryClusterConnectionString);
                }
                catch (Exception ex) {
                    ServiceEventSource.Current.Message("Policy Storage Service: Exception getting storage info {0}", ex);
                    throw;
                }
                if (backupStorage != null && entity.backupStorage != null)
                {
                    backupStorage.connectionString = entity.backupStorage.connectionString;
                    backupStorage.primaryUsername = entity.backupStorage.primaryUsername;
                    backupStorage.primaryPassword = entity.backupStorage.primaryPassword;
                    backupStorage.secondaryUsername = entity.backupStorage.secondaryUsername;
                    backupStorage.secondaryPassword = entity.backupStorage.secondaryPassword;
                    backupStorage.friendlyname = entity.backupStorage.friendlyname;
                    backupStorage.Encrypt();
                }
                else
                {
                    return false;
                }
                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryAddAsync(tx, entity.policy, backupStorage);

                    ServiceEventSource.Current.ServiceMessage(this.Context, result ? "Successfully added policy {0} storgae details" : "Already Exists", entity.policy);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }
            }
            return true;
        }

        /// <summary>
        /// This returns the storage details for the policy specified
        /// </summary>
        /// <param name="policy"></param>
        /// <returns></returns>
        public async Task<BackupStorage> GetPolicyStorageDetails(String policy)
        {
            IReliableDictionary<string, BackupStorage> myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BackupStorage>>("storageDictionary");
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<BackupStorage> backupStorage = await myDictionary.TryGetValueAsync(tx, policy);
                if (backupStorage.HasValue)
                {
                    BackupStorage bstorage = backupStorage.Value;
                    bstorage.Decrypt();
                    return bstorage;
                }
                else
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Policy not found");
                    return null;
                }
            }
        }

        public async Task<List<String>> GetAllStoredPolicies()
        {
            List<String> allBackupStoragePolicies = new List<String>();
            IReliableDictionary<string, BackupStorage> policiesDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BackupStorage>>("storageDictionary");

            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<string, BackupStorage>> enumerable = await policiesDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<string, BackupStorage>> asyncEnumerator = enumerable.GetAsyncEnumerator();
                while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                {
                    allBackupStoragePolicies.Add(asyncEnumerator.Current.Key);
                }
            }

            return allBackupStoragePolicies;
        }

        public async Task<BackupStorage> GetStorageInfo(string policy, string primaryClusterConnectionString)
        {
            string URL = "https://" + primaryClusterConnectionString + "/";
            string urlParameters = "BackupRestore/BackupPolicies/" + policy + "?api-version=6.2-preview";


            X509Certificate2 clientCert = GetClientCertificate();
            WebRequestHandler requestHandler = new WebRequestHandler();
            requestHandler.ClientCertificates.Add(clientCert);
            requestHandler.ServerCertificateValidationCallback = this.MyRemoteCertificateValidationCallback;


            HttpClient client = new HttpClient(requestHandler)
            {
                BaseAddress = new Uri(URL)
            };
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            // List data response.
            HttpResponseMessage response = await client.GetAsync(urlParameters);  // Blocking call!
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                var content = response.Content.ReadAsAsync<JObject>().Result;
                JObject objectData = (JObject)content["Storage"];
                BackupStorage backupStorage = JsonConvert.DeserializeObject<BackupStorage>(objectData.ToString());
                return backupStorage;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }

        static X509Certificate2 GetClientCertificate()
        {
            X509Store userCaStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                userCaStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certificatesInStore = userCaStore.Certificates;
                X509Certificate2Collection findResult = certificatesInStore.Find(X509FindType.FindByThumbprint, "45E894C34014B198B157F95A57EF98BD7D051194", false);
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

        private bool MyRemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
