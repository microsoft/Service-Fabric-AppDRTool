using System;
using System.Collections;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json.Linq;
using PolicyStorageService;

namespace RestoreService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class RestoreService : Microsoft.ServiceFabric.Services.Runtime.StatefulService, IRestoreService
    {

        public static Dictionary<String, Task<RestoreResult>> workFlowsInProgress;

        private System.Threading.Timer timer;

        long periodTimeSpan = 30000;

        public RestoreService(StatefulServiceContext context)
            : base(context)
        {
            workFlowsInProgress = new Dictionary<String, Task<RestoreResult>>();
            this.timer = new System.Threading.Timer(this.TimerTickCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

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
            // To trigger OnTimerTick method every minute

            var periodTimeSpan = TimeSpan.FromMinutes(1);

            timer.Change(0, Timeout.Infinite);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        private void TimerTickCallback(object state)
        {
            try
            {
                this.OnTimerTick().Wait();
            }
            finally
            {
                // Configure timer to trigger after 1 Min.
                timer.Change(this.periodTimeSpan, Timeout.Infinite);
            }
        }

        public async Task OnTimerTick()
        {
            // On every timer tick calls this method which goes through the workflowsInProgress dictionary
            // and removes the completed tasks and updates the partition metadata.
            // For every partition mapping in the reliable dictionary if the corresponding task is not present in the 
            // workflowsInProgress dictionary it will create a task and puts in the dictionary
            IReliableDictionary<String, PartitionWrapper> partitionDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>("partitionDictionary");
            List<String> keysToRemove = new List<String>();
            if (workFlowsInProgress.Count != 0)
            {
                try
                {
                    foreach (KeyValuePair<String, Task<RestoreResult>> workFlow in workFlowsInProgress)
                    {
                        Task<RestoreResult> task = workFlow.Value;
                        if (task.IsCompleted)
                        {
                            RestoreResult restoreResult = task.Result;
                            using (ITransaction tx = this.StateManager.CreateTransaction())
                            {
                                ConditionalValue<PartitionWrapper> partitionWrapper = await partitionDictionary.TryGetValueAsync(tx, workFlow.Key);
                                if (partitionWrapper.HasValue)
                                {
                                    if(restoreResult == null)
                                    {
                                        PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(partitionWrapper.Value);
                                        updatedPartitionWrapper.CurrentlyUnderRestore = null;
                                        await partitionDictionary.SetAsync(tx, workFlow.Key, updatedPartitionWrapper);
                                        ServiceEventSource.Current.ServiceMessage(this.Context, "Restore Task returned null!!! ");
                                    }
                                    else if (restoreResult.restoreState.Equals("Success"))
                                    {
                                        PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(partitionWrapper.Value);
                                        updatedPartitionWrapper.LastBackupRestored = restoreResult.restoreInfo;
                                        updatedPartitionWrapper.CurrentlyUnderRestore = null;
                                        await partitionDictionary.SetAsync(tx, workFlow.Key, updatedPartitionWrapper);
                                        ServiceEventSource.Current.ServiceMessage(this.Context, "Restored succcessfully!!! ");
                                    }
                                }
                                await tx.CommitAsync();
                            }
                            keysToRemove.Add(workFlow.Key);
                        }
                    }
                    foreach(var key in keysToRemove)
                    {
                        workFlowsInProgress.Remove(key);
                    }
                }
                catch(Exception ex)
                {
                    ServiceEventSource.Current.Message("exception caught : {0}",ex);
                }
            }
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<String, PartitionWrapper>> enumerable = await partitionDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<String, PartitionWrapper>> asyncEnumerator = enumerable.GetAsyncEnumerator();
                while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                {
                    String primaryPartition = asyncEnumerator.Current.Key;
                    PartitionWrapper secondaryPartition = asyncEnumerator.Current.Value;
                    if (secondaryPartition == null)
                        continue;
                    JToken backupInfoToken = await GetLatestBackupAvailable(secondaryPartition.primaryPartitionId, "https://" + secondaryPartition.primaryCluster.address + ":" + secondaryPartition.primaryCluster.httpEndpoint, secondaryPartition.primaryCluster.certificateThumbprint);
                    if (backupInfoToken == null)
                        continue;
                    BackupInfo backupInfo = new BackupInfo(backupInfoToken["BackupId"].ToString(), backupInfoToken["BackupLocation"].ToString(), (DateTime)backupInfoToken["CreationTimeUtc"]);
                    string backupPolicy = await GetPolicy("https://" + secondaryPartition.primaryCluster.address + ":" + secondaryPartition.primaryCluster.httpEndpoint, secondaryPartition.primaryCluster.certificateThumbprint, secondaryPartition.primaryPartitionId);
                    if (backupPolicy == null)
                        continue;
                    Task<RestoreResult> task = workFlowsInProgress.TryGetValue(primaryPartition, out Task<RestoreResult> value) ? value : null;
                    if (task == null)
                    {
                        if (secondaryPartition.LastBackupRestored == null || DateTime.Compare(backupInfo.backupTime, secondaryPartition.LastBackupRestored.backupTime) > 0)
                        {
                            Task<RestoreResult> restoreTask = Task<RestoreResult>.Run(() => RestoreWorkFlow(backupInfoToken, backupPolicy, secondaryPartition, "https://" + secondaryPartition.secondaryCluster.address + ":" + secondaryPartition.secondaryCluster.httpEndpoint, secondaryPartition.secondaryCluster.certificateThumbprint));
                            workFlowsInProgress.Add(asyncEnumerator.Current.Key, restoreTask);
                            PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(secondaryPartition);
                            updatedPartitionWrapper.LatestBackupAvailable = backupInfo;
                            updatedPartitionWrapper.CurrentlyUnderRestore = backupInfo;
                            await partitionDictionary.SetAsync(tx, primaryPartition, updatedPartitionWrapper);
                        }
                        else
                            continue;
                    }
                    else if (task.IsCompleted)
                    {
                        RestoreResult restoreResult = task.Result;
                        if (restoreResult.restoreState.Equals("Success"))
                        {
                            PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(secondaryPartition);
                            updatedPartitionWrapper.LastBackupRestored = restoreResult.restoreInfo;
                            updatedPartitionWrapper.CurrentlyUnderRestore = null;
                            await partitionDictionary.SetAsync(tx, primaryPartition, updatedPartitionWrapper);
                            ServiceEventSource.Current.ServiceMessage(this.Context, "Successfully Restored!!! ");
                        }
                        workFlowsInProgress.Remove(primaryPartition);
                        if (secondaryPartition.LastBackupRestored == null || DateTime.Compare(backupInfo.backupTime, secondaryPartition.LastBackupRestored.backupTime) > 0)
                        {
                            Task<RestoreResult> restoreTask = Task<string>.Run(() => RestoreWorkFlow(backupInfoToken, backupPolicy, secondaryPartition, "https://" + secondaryPartition.secondaryCluster.address + ":" + secondaryPartition.secondaryCluster.httpEndpoint, secondaryPartition.secondaryCluster.certificateThumbprint));
                            workFlowsInProgress.Add(primaryPartition, restoreTask);
                            PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(secondaryPartition);
                            updatedPartitionWrapper.LatestBackupAvailable = backupInfo;
                            updatedPartitionWrapper.CurrentlyUnderRestore = backupInfo;
                            await partitionDictionary.SetAsync(tx, primaryPartition, updatedPartitionWrapper);
                        }
                        else
                            continue;
                    }
                    else
                    {
                        PartitionWrapper updatedPartitionWrapper = ObjectExtensions.Copy(secondaryPartition);
                        updatedPartitionWrapper.LatestBackupAvailable = backupInfo;
                        await partitionDictionary.SetAsync(tx, primaryPartition, updatedPartitionWrapper);
                    }
                }
                await tx.CommitAsync();
            }

        }

        public async Task ConfigureApplication(string application, List<PolicyStorageEntity> policyDetails, ClusterDetails primaryCluster, ClusterDetails secondaryCluster)
        {
            IPolicyStorageService policyStorageClient = ServiceProxy.Create<IPolicyStorageService>(new Uri("fabric:/SFAppDRTool/PolicyStorageService"));
            bool stored = policyStorageClient.PostStorageDetails(policyDetails, primaryCluster.address + ':' + primaryCluster.httpEndpoint).GetAwaiter().GetResult();
            await MapPartitionsOfApplication(new Uri(application), primaryCluster, secondaryCluster, "partitionDictionary");
            IReliableDictionary<String, List<String>> configuredApplicationsDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, List<String>>>("configuredApplicationsDictionary");
            using (var tx = this.StateManager.CreateTransaction())
            {
                String primarySecondaryJoin = Utility.getPrimarySecondaryClusterJoin(primaryCluster.address, secondaryCluster.address);
                ConditionalValue<List<String>> applicationsList = await configuredApplicationsDictionary.TryGetValueAsync(tx, primarySecondaryJoin);
                if (applicationsList.HasValue)
                {
                    List<String> configuredApplicationsList = applicationsList.Value;
                    configuredApplicationsList.Add(application);
                    var result = await configuredApplicationsDictionary.TryAddAsync(tx, primarySecondaryJoin, configuredApplicationsList);
                }
                else
                {
                    List<String> configuredApplicationsList = new List<String>();
                    configuredApplicationsList.Add(application);
                    var result = await configuredApplicationsDictionary.TryAddAsync(tx, primarySecondaryJoin, configuredApplicationsList);
                }

                await tx.CommitAsync();
            }
        }

        public async Task<List<String>> GetConfiguredApplicationNames(String primaryClusterEndpoint, String secondaryClusterEndpoint)
        {
            List<String> configuredApplicationNames;
            String primaryClusterName = Utility.getClusterNameFromTCPEndpoint(primaryClusterEndpoint);
            String secondaryClusterName = Utility.getClusterNameFromTCPEndpoint(secondaryClusterEndpoint);
            String primarySecondaryJoin = Utility.getPrimarySecondaryClusterJoin(primaryClusterName, secondaryClusterName);
            IReliableDictionary<String, List<String>> configuredApplicationsDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, List<String>>>("configuredApplicationsDictionary");
            using (var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<List<String>> applicationsList = await configuredApplicationsDictionary.TryGetValueAsync(tx, primarySecondaryJoin);
                if (applicationsList.HasValue)
                {
                    List<String> configuredApplicationsList = applicationsList.Value;
                    configuredApplicationNames = configuredApplicationsList;
                }
                else
                {
                    List<String> configuredApplicationsList = new List<String>();
                    configuredApplicationNames = configuredApplicationsList;
                }

                await tx.CommitAsync();
            }
            return configuredApplicationNames;
        }

        public async Task ConfigureService(String applicationName, String serviceName, List<PolicyStorageEntity> policyDetails, ClusterDetails primaryCluster, ClusterDetails secondaryCluster)
        {
            IPolicyStorageService policyStorageClient = ServiceProxy.Create<IPolicyStorageService>(new Uri("fabric:/SFAppDRTool/PolicyStorageService"));
            bool stored = await policyStorageClient.PostStorageDetails(policyDetails, primaryCluster.address + ':' + primaryCluster.httpEndpoint);
            await MapPartitionsOfService(new Uri(applicationName), new Uri(serviceName), primaryCluster, secondaryCluster, "partitionDictionary");
        }

        // An interface method which is for disconfiguring the appliations thereby deleting their entries in reliable dictionary.
        public async Task<string> DisconfigureApplication(string applicationName, string primaryCluster, string secondaryCluster)
        {
            List<String> keysToRemove = new List<String>();
            IReliableDictionary<String, PartitionWrapper> myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>("partitionDictionary");
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<String, PartitionWrapper>> enumerable = await myDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<String, PartitionWrapper>> asyncEnumerator = enumerable.GetAsyncEnumerator();
                while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                {
                    PartitionWrapper secondaryPartition = asyncEnumerator.Current.Value;
                    String partitionAccessKey = asyncEnumerator.Current.Key;

                    if (Utility.isPartitionFromPrimarySecondaryCombination(partitionAccessKey, primaryCluster, secondaryCluster))
                    {
                        if (secondaryPartition.applicationName.ToString().Equals(applicationName))
                        {
                            keysToRemove.Add(asyncEnumerator.Current.Key);
                        }
                    }
                }
                await tx.CommitAsync();
            }
            bool allPartitionsRemoved = true;
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                foreach(String key in keysToRemove)
                {
                    ConditionalValue<PartitionWrapper> value  = myDictionary.TryRemoveAsync(tx, key).Result;
                    if (!value.HasValue)
                    {
                        allPartitionsRemoved = false;
                    }
                }
                await tx.CommitAsync();
            }

            IReliableDictionary<String, List<String>> configuredApplicationsDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, List<String>>>("configuredApplicationsDictionary");
            using (var tx = this.StateManager.CreateTransaction())
            {
                String primarySecondaryJoin = Utility.getPrimarySecondaryClusterJoin(primaryCluster, secondaryCluster);
                ConditionalValue<List<String>> applicationsList = await configuredApplicationsDictionary.TryGetValueAsync(tx, primarySecondaryJoin);
                if (applicationsList.HasValue)
                {
                    List<String> configuredApplicationsList = applicationsList.Value;
                    configuredApplicationsList.Remove(applicationName);
                    var result = await configuredApplicationsDictionary.TryAddAsync(tx, primarySecondaryJoin, configuredApplicationsList);
                }
                await tx.CommitAsync();
            }

            if (allPartitionsRemoved) return applicationName;
            return null;
        }

        public async Task<string> DisconfigureService(string serviceName, string primaryCluster, string secondaryCluster)
        {
            List<String> keysToRemove = new List<String>();
            IReliableDictionary<String, PartitionWrapper> myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>("partitionDictionary");
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<String, PartitionWrapper>> enumerable = await myDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<String, PartitionWrapper>> asyncEnumerator = enumerable.GetAsyncEnumerator();
                while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                {
                    PartitionWrapper secondaryPartition = asyncEnumerator.Current.Value;
                    String partitionAccessKey = asyncEnumerator.Current.Key;

                    if (Utility.isPartitionFromPrimarySecondaryCombination(partitionAccessKey, primaryCluster, secondaryCluster))
                    {
                        if (secondaryPartition.serviceName.ToString().Equals(serviceName))
                        {
                            keysToRemove.Add(asyncEnumerator.Current.Key);
                        }
                    }

                    
                }
                await tx.CommitAsync();
            }
            bool allPartitionsRemoved = true;
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                foreach (String key in keysToRemove)
                {
                    ConditionalValue<PartitionWrapper> value = myDictionary.TryRemoveAsync(tx, key).Result;
                    if (!value.HasValue)
                    {
                        allPartitionsRemoved = false;
                    }
                }
                await tx.CommitAsync();
            }
            if (allPartitionsRemoved) return serviceName;
            return null;
        }


        // An interface method which returns the entries of reliable dictionary
        public async Task<List<PartitionWrapper>> GetStatus()
        {
            IReliableDictionary<String, PartitionWrapper> partitionDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>("partitionDictionary");
            List<PartitionWrapper> mappedPartitions = new List<PartitionWrapper>();
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<String, PartitionWrapper>> enumerable = await partitionDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<String, PartitionWrapper>> asyncEnumerator = enumerable.GetAsyncEnumerator();
                while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                {
                    ConditionalValue<PartitionWrapper> partitionWrapper = await partitionDictionary.TryGetValueAsync(tx,asyncEnumerator.Current.Key);
                    if (partitionWrapper.HasValue)
                    {
                        PartitionWrapper mappedPartition = partitionWrapper.Value;
                        mappedPartition.RestoreState = GetRestoreState(mappedPartition, "https://" + mappedPartition.secondaryCluster.address + ":" + mappedPartition.secondaryCluster.httpEndpoint, mappedPartition.secondaryCluster.certificateThumbprint);
                        mappedPartitions.Add(mappedPartition);
                        ServiceEventSource.Current.ServiceMessage(this.Context, "Successfully Retrieved!!! ");
                    }
                }
                await tx.CommitAsync();
            }
            return mappedPartitions;
        }

        // For a given partition gets the policy associated with it from primary cluster
        public async Task<String> GetPolicy(string primaryCluster, string clusterThumbprint, Guid partitionId)
        {
            string URL = primaryCluster + "/";
            string urlParameters = "Partitions/" + partitionId + "/$/GetBackupConfigurationInfo" + "?api-version=6.2-preview";


            X509Certificate2 clientCert = GetClientCertificate(clusterThumbprint);
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
                if (content == null)
                    return null;
                string policy = content["PolicyName"].ToString();
                return policy;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }

        // This encapsulates a restore task which triggers the restore and returns the restore result accordingly.
        public async Task<RestoreResult> RestoreWorkFlow(JToken latestbackupInfo, string policy, PartitionWrapper partition, String clusterConnectionString, String clusterThumbprint)
        {

            string URL = clusterConnectionString + "/";
            string urlParameters = "Partitions/" + partition.partitionId + "/$/Restore" + "?api-version=6.2-preview";


            X509Certificate2 clientCert = GetClientCertificate(clusterThumbprint);
            WebRequestHandler requestHandler = new WebRequestHandler();
            requestHandler.ClientCertificates.Add(clientCert);
            requestHandler.ServerCertificateValidationCallback = this.MyRemoteCertificateValidationCallback;


            HttpClient client = new HttpClient(requestHandler)
            {
                BaseAddress = new Uri(URL)
            };
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            BackupStorage backupStorage = await GetBackupStorageDetails(policy);
            if (backupStorage == null)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "backupstorage is null");
                return null;
            }

            BackupInfo backupInfo = new BackupInfo(latestbackupInfo["BackupId"].ToString(), latestbackupInfo["BackupLocation"].ToString(), backupStorage, (DateTime)latestbackupInfo["CreationTimeUtc"]);

            HttpResponseMessage response = await client.PostAsJsonAsync(urlParameters, backupInfo);  // Blocking call!
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict) // As calling Restore multiple times results in 409/Conflict Error, when in progress
            {
                string restoreState = "";
                restoreState = GetRestoreState(partition, clusterConnectionString, clusterThumbprint);
                return new RestoreResult(backupInfo, restoreState);
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

        }

        // Maps the paritions of the applications from primary cluster and secondary cluster
        public async Task MapPartitionsOfApplication(Uri applicationName, ClusterDetails primaryCluster, ClusterDetails secondaryCluster, String partitionDictionaryName)
        {
            FabricClient primaryFabricClient = GetSecureFabricClient(primaryCluster.address + ':' + primaryCluster.clientConnectionEndpoint, primaryCluster.certificateThumbprint, primaryCluster.commonName);
            FabricClient secondaryFabricClient = GetSecureFabricClient(secondaryCluster.address + ':' + secondaryCluster.clientConnectionEndpoint, secondaryCluster.certificateThumbprint, secondaryCluster.commonName);

            ServiceList services = await primaryFabricClient.QueryManager.GetServiceListAsync(applicationName);
            foreach(Service service in services)
            {
                ServicePartitionList primaryPartitions;
                ServicePartitionList secondaryPartitions;

                if (service.ServiceKind == ServiceKind.Stateless)
                {
                    continue;
                }

                try
                {
                    primaryPartitions = await primaryFabricClient.QueryManager.GetPartitionListAsync(service.ServiceName);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Restore Service: Exception getting primary partitions {0}", ex);
                    throw;
                }
                try
                {
                    secondaryPartitions = await secondaryFabricClient.QueryManager.GetPartitionListAsync(service.ServiceName);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Restore Service: Exception getting secondary partitions {0}", ex);
                    throw;
                }
                await MapPartitions(applicationName, service.ServiceName, primaryCluster, primaryPartitions, secondaryCluster, secondaryPartitions, partitionDictionaryName);
            }
        }

        public async Task MapPartitionsOfService(Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ClusterDetails secondaryCluster, String partitionDictionaryName)
        {
            FabricClient primaryFabricClient = GetSecureFabricClient(primaryCluster.address + ':' + primaryCluster.clientConnectionEndpoint, primaryCluster.certificateThumbprint, primaryCluster.commonName);
            FabricClient secondaryFabricClient = GetSecureFabricClient(secondaryCluster.address + ':' + secondaryCluster.clientConnectionEndpoint, secondaryCluster.certificateThumbprint, secondaryCluster.commonName);

            ServicePartitionList primaryPartitions = await primaryFabricClient.QueryManager.GetPartitionListAsync(serviceName);
            ServicePartitionList secondaryPartitions = await secondaryFabricClient.QueryManager.GetPartitionListAsync(serviceName);
            await MapPartitions(applicationName, serviceName, primaryCluster, primaryPartitions, secondaryCluster, secondaryPartitions, partitionDictionaryName);
        }

        public static FabricClient GetSecureFabricClient(string connectionEndpoint, string thumbprint, string cname)
        {
            var xc = GetCredentials(thumbprint, thumbprint, cname);

            FabricClient fc;

            try
            {
                fc = new FabricClient(xc, connectionEndpoint);
                return fc;
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.Message("Web Service: Exception while trying to connect securely: {0}", e);
                throw;
            }
        }

        static X509Credentials GetCredentials(string clientCertThumb, string serverCertThumb, string name)
        {
            X509Credentials xc = new X509Credentials();
            xc.StoreLocation = StoreLocation.CurrentUser;
            xc.StoreName = "My";
            xc.FindType = X509FindType.FindByThumbprint;
            xc.FindValue = clientCertThumb;
            xc.RemoteCommonNames.Add(name);
            xc.RemoteCertThumbprints.Add(serverCertThumb);
            xc.ProtectionLevel = System.Fabric.ProtectionLevel.EncryptAndSign;
            return xc;
        }

        public async Task MapPartitions(Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ServicePartitionList partitionsInPrimary, ClusterDetails secondaryCluster,ServicePartitionList partitionsInSecondary, string partitionDictionaryName)
        {
            if (partitionsInPrimary != null)
            {
                ServicePartitionKind partitionKind = partitionsInPrimary[0].PartitionInformation.Kind;
                if (partitionKind.Equals(ServicePartitionKind.Int64Range))
                {
                    await MapInt64Partitions(applicationName, serviceName, primaryCluster, partitionsInPrimary, secondaryCluster, partitionsInSecondary, partitionDictionaryName);
                }
                else if (partitionKind.Equals(ServicePartitionKind.Named))
                {
                    await MapNamedPartitions(applicationName, serviceName, primaryCluster, partitionsInPrimary, secondaryCluster, partitionsInSecondary, partitionDictionaryName);
                }
                else if (partitionKind.Equals(ServicePartitionKind.Singleton))
                {
                    await MapSingletonPartition(applicationName, serviceName, primaryCluster, partitionsInPrimary, secondaryCluster, partitionsInSecondary, partitionDictionaryName);
                }
            }
        }

         // Maps Int64 partitions based on their low key
        public async Task MapInt64Partitions(Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ServicePartitionList primaryPartitions, ClusterDetails secondaryCluster, ServicePartitionList secondaryPartitions, string partitionDictionaryName)
        {
            IReliableDictionary<String, PartitionWrapper> partitionDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>(partitionDictionaryName);
            foreach (var primaryPartition in primaryPartitions)
            {
                long hashCode = HashUtil.getLongHashCode(primaryPartition.PartitionInformation.Id.ToString());
                if (await BelongsToPartition(hashCode))
                {
                    var int64PartitionInfo = primaryPartition.PartitionInformation as Int64RangePartitionInformation;
                    long? lowKeyPrimary = int64PartitionInfo?.LowKey;
                    foreach (var secondaryPartition in secondaryPartitions)
                    {
                        long? lowKeySecondary = (secondaryPartition.PartitionInformation as Int64RangePartitionInformation)?.LowKey;
                        if (lowKeyPrimary == lowKeySecondary)
                        {

                            using (var tx = this.StateManager.CreateTransaction())
                            {
                                var result = await partitionDictionary.TryAddAsync(tx, Utility.getPartitionAccessKey(primaryPartition.PartitionInformation.Id, primaryCluster.address, secondaryCluster.address), new PartitionWrapper(secondaryPartition, primaryPartition.PartitionInformation.Id, applicationName, serviceName, primaryCluster, secondaryCluster));

                                ServiceEventSource.Current.ServiceMessage(this.Context, result ? "Successfully Mapped Partition-{0} to Partition-{1}" : "Already Exists", primaryPartition.PartitionInformation.Id, secondaryPartition.PartitionInformation.Id);
                                await tx.CommitAsync();
                            }
                        }
                    }
                }
            }
        }

        // Maps named partitions based on their name
        public async Task MapNamedPartitions(Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ServicePartitionList primaryPartitions, ClusterDetails secondaryCluster, ServicePartitionList secondaryPartitions, string partitionDictionaryName)
        {
            IReliableDictionary<String, PartitionWrapper> partitionDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>(partitionDictionaryName);
            foreach (var primaryPartition in primaryPartitions)
            {
                var partitionNamePrimary = (primaryPartition.PartitionInformation as NamedPartitionInformation).Name;
                foreach (var secondaryPartition in secondaryPartitions)
                {
                    string partitionNameSecondary = (secondaryPartition.PartitionInformation as NamedPartitionInformation).Name;
                    if (partitionNamePrimary == partitionNameSecondary)
                    {
                        using (var tx = this.StateManager.CreateTransaction())
                        {
                            var result = await partitionDictionary.TryAddAsync(tx, Utility.getPartitionAccessKey(primaryPartition.PartitionInformation.Id, primaryCluster.address, secondaryCluster.address), new PartitionWrapper(secondaryPartition, primaryPartition.PartitionInformation.Id, applicationName, serviceName, primaryCluster, secondaryCluster));

                            ServiceEventSource.Current.ServiceMessage(this.Context, result ? "Successfully Mapped Partition-{0} to Partition-{1}" : "Already Exists", primaryPartition.PartitionInformation.Id, secondaryPartition.PartitionInformation.Id);
                            // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                            // discarded, and nothing is saved to the secondary replicas.
                            await tx.CommitAsync();
                        }
                    }
                }
            }
        }

        // Maps singleton partitions
        public async Task MapSingletonPartition(Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ServicePartitionList primaryPartitions, ClusterDetails secondaryCluster, ServicePartitionList secondaryPartitions, string partitionDictionaryName)
        {
            IReliableDictionary<String, PartitionWrapper> partitionDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<String, PartitionWrapper>>(partitionDictionaryName);
            using (var tx = this.StateManager.CreateTransaction())
            {
                var result = await partitionDictionary.TryAddAsync(tx, Utility.getPartitionAccessKey(primaryPartitions[0].PartitionInformation.Id, primaryCluster.address, secondaryCluster.address), new PartitionWrapper(secondaryPartitions[0], primaryPartitions[0].PartitionInformation.Id, applicationName, serviceName, primaryCluster, secondaryCluster));

                ServiceEventSource.Current.ServiceMessage(this.Context, result ? "Successfully Mapped Partition-{0} to Partition-{1}" : "Already Exists", primaryPartitions[0].PartitionInformation.Id, secondaryPartitions[0].PartitionInformation.Id);
                // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                // discarded, and nothing is saved to the secondary replicas.
                await tx.CommitAsync();
            }
        }

        // Gets latest backups available from the primary cluster
        // TODO :  This method can be modified to fetch backups from the storage location rather than the primary cluster
        public async Task<JToken> GetLatestBackupAvailable(Guid partitionId, String clusterConnnectionString, String clusterThumbprint)
        {
            string URL = clusterConnnectionString + "/";
            string urlParameters = "Partitions/" + partitionId + "/$/GetBackups" + "?api-version=6.2-preview";


            X509Certificate2 clientCert = GetClientCertificate(clusterThumbprint);
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
                JArray array = (JArray)content["Items"];
                foreach (var item in array)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "BackUpID :" + item["BackupId"]);
                }
                return array.Last;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }

        static X509Certificate2 GetClientCertificate(string Thumbprint)
        {
            X509Store userCaStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
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

        private bool MyRemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        /// <summary>
        /// This method fetches the restore state of each restore flow task
        /// </summary>
        /// <param name="partition">Which is under restore</param>
        /// <param name="clusterConnectionString">Secondary Cluster String</param>
        /// <returns></returns>
        public string GetRestoreState(PartitionWrapper partition, string clusterConnectionString, string clusterThumbprint)
        {

            string URL = clusterConnectionString + "/";
            string urlParameters = "Partitions/" + partition.partitionId + "/$/GetRestoreProgress" + "?api-version=6.2-preview";


            X509Certificate2 clientCert = GetClientCertificate(clusterThumbprint);
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
            HttpResponseMessage response = client.GetAsync(urlParameters).Result;  // Blocking call!
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsAsync<JObject>().Result;
                string restoreState = content["RestoreState"].ToString();
                return restoreState;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }

        /// <summary>
        /// This method communicates with policystorage service and gets the backupstorage details
        /// </summary>
        /// <param name="policy">Policy which is to be restored</param>
        /// <returns>BackupStorage details</returns>
        public async Task<BackupStorage> GetBackupStorageDetails(string policy)
        {
            IPolicyStorageService policyStorageClient = ServiceProxy.Create<IPolicyStorageService>(new Uri("fabric:/SFAppDRTool/PolicyStorageService"));
            BackupStorage backupStorage = await policyStorageClient.GetPolicyStorageDetails(policy);
            return backupStorage;
        }

        /// <summary>
        /// This method tells whether the partition map should be stored in this partition or not. This is basically partitioning the data
        /// </summary>
        /// <param name="hashCode">Hashed value of partitionId</param>
        /// <returns></returns>
        public async Task<bool> BelongsToPartition(long hashCode)
        {
            FabricClient fabricClient = new FabricClient();
            System.Fabric.Query.ServicePartitionList partitions = await fabricClient.QueryManager.GetPartitionAsync(this.Context.PartitionId);
            foreach(var partition in partitions)
            {
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long? lowKey = int64PartitionInfo?.LowKey;
                long? highKey = int64PartitionInfo?.HighKey;
                if (hashCode >= lowKey && hashCode <= highKey)
                    return true;
            }
            return false;
        }
    }
}

