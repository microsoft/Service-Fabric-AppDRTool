// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Http.Headers;
using RestoreService;
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
        public async Task<bool> PostStorageDetails(List<PolicyStorageEntity> policies, string primaryClusterConnectionString, string clusterThumbprint, bool overwritePolicyDetails)
        {
            IReliableDictionary<string, BackupStorage> myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BackupStorage>>("storageDictionary");
            foreach (var entity in policies)
            {
                BackupStorage backupStorage;
                try
                {
                    backupStorage = await GetStorageInfo(entity.policy, primaryClusterConnectionString, clusterThumbprint);
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
                    ConditionalValue<BackupStorage> cndbackupStorage = await myDictionary.TryGetValueAsync(tx, entity.policy);

                    if (!cndbackupStorage.HasValue || cndbackupStorage.HasValue && overwritePolicyDetails)
                    {
                        var result = await myDictionary.TryAddAsync(tx, entity.policy, backupStorage);
                        ServiceEventSource.Current.ServiceMessage(this.Context, result ? "Successfully added policy {0} storage details" : "Could not add policy", entity.policy);
                    }
                    else
                    {
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Policy {0} already exists", entity.policy);
                    }
                    
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
                    BackupStorage exportableBstorage = bstorage.DeepCopy();
                    exportableBstorage.Decrypt();
                    return exportableBstorage;
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

        public async Task<BackupStorage> GetStorageInfo(string policy, string primaryClusterConnectionString, string clusterThumbprint)
        {
            string URL = primaryClusterConnectionString + "/";
            string URLParameters = "BackupRestore/BackupPolicies/" + policy + "?api-version=6.4";

            HttpResponseMessage response = await Utility.HTTPGetAsync(URL, URLParameters, clusterThumbprint);

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
    }
}
