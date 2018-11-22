using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestoreService;
using System.Web.Script.Serialization;
using PolicyStorageService;
using System.Fabric.Query;
using WebInterface.Models;
using System.Xml.Linq;
using System.Net;
using System.Net.Http;

namespace WebInterface.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class RestoreServiceController : Controller
    {

        [HttpGet]
        [Route("apps/{primarycs}/{primaryThumbprint}/{primarycname}/{secondarycs}/{secondaryThumbprint}/{secondarycname}")]
        public async Task<IActionResult> GetApplications(String primarycs, String primaryThumbprint, String primarycname, String secondarycs, String secondaryThumbprint, String secondarycname)
        {
            FabricClient primaryfc = Utility.GetFabricClient(primarycs, primaryThumbprint, primarycname);
            FabricClient secondaryfc = Utility.GetFabricClient(secondarycs, secondaryThumbprint, secondarycname);

            var applicationsServicesMap = await GetApplicationsServices(primaryfc, primarycs, secondaryfc, secondarycs);

            return this.Json(applicationsServicesMap);

        }

        public async Task<Dictionary<String, List<List<String>>>> GetApplicationsServices(FabricClient primaryfc, String primarycs, FabricClient secondaryfc, String secondarycs)
        {
            Dictionary<String, List<List<String>>> applicationsServicesMap = new Dictionary<String, List<List<String>>>();

            FabricClient.QueryClient queryClient = primaryfc.QueryManager;
            ApplicationList appsList = await queryClient.GetApplicationListAsync();

            HashSet<String> configuredApplications = await GetConfiguredApplications(primarycs, secondarycs);
            HashSet<String> configuredServices = await GetConfiguredServices();
            HashSet<String> secServices = new HashSet<string>();

            foreach (Application application in appsList)
            {
                string applicationName = application.ApplicationName.ToString();
                string applicationStatus = "NotConfigured";

                ServiceList services = await primaryfc.QueryManager.GetServiceListAsync(new Uri(applicationName));

                ServiceList secondaryServices;

                try
                {
                    secondaryServices = await secondaryfc.QueryManager.GetServiceListAsync(new Uri(applicationName));

                    foreach (Service service in secondaryServices)
                    {
                        secServices.Add(service.ServiceName.ToString());
                    }
                }
                catch (System.Fabric.FabricElementNotFoundException e)
                {
                    ServiceEventSource.Current.Message("Web Service: Could not find application on secondary cluster: {0}", e);
                    applicationStatus = "NotExist";
                }
                catch (Exception e)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception with Fabric Client Query Manager {0}", e);
                    throw;
                }

                if (configuredApplications.Contains(applicationName))
                {
                    applicationStatus = "Configured";
                }

                List<List<String>> serviceList = new List<List<String>>();
                List<String> appStatusList = new List<String>();

                appStatusList.Add(applicationName);
                appStatusList.Add(applicationStatus);

                serviceList.Add(appStatusList);

                foreach (Service service in services)
                {
                    List<String> serviceInfo = new List<String>();
                    string serviceName = service.ServiceName.ToString();

                    if (secServices.Contains(serviceName))
                    {

                        if (configuredServices.Contains(serviceName))
                        {
                            //Configured
                            serviceInfo.Add(serviceName);
                            serviceInfo.Add("Configured");
                        }
                        else if (service.ServiceKind == ServiceKind.Stateless)
                        {
                            //Stateless
                            serviceInfo.Add(serviceName);
                            serviceInfo.Add("Stateless");
                        }
                        else
                        {
                            //NotConfigured
                            serviceInfo.Add(serviceName);
                            serviceInfo.Add("NotConfigured");
                        }
                    }
                    else
                    {
                        //NotExist
                        serviceInfo.Add(serviceName);
                        serviceInfo.Add("NotExist");
                    }


                    serviceList.Add(serviceInfo);
                }

                applicationsServicesMap.Add(applicationName, serviceList);
            }


            return applicationsServicesMap;
        }

        [HttpGet]
        [Route("storedpolicies")]
        public async Task<IActionResult> GetAllStoredPolices()
        {
            List<string> policiesList = await GetStoredPolicies();
            return this.Json(policiesList);
        }

        private async Task<PolicyStorageEntity> GetPolicyDetails(string httpConnectionString, string thumbprint, string policyName)
        {
            PolicyStorageEntity policyStorageEntity = new PolicyStorageEntity
            {
                policy = policyName
            };
            string URL = httpConnectionString + "/";
            string URLParameters = "BackupRestore/BackupPolicies/" + policyName + "?api-version=6.2-preview";

            HttpResponseMessage response = await Utility.HTTPGetAsync(URL, URLParameters, thumbprint);
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                var content = response.Content.ReadAsAsync<JObject>().Result;
                JObject objectData = (JObject)content["Storage"];
                BackupStorage backupStorage = JsonConvert.DeserializeObject<BackupStorage>(objectData.ToString());
                policyStorageEntity.backupStorage = backupStorage;
                return policyStorageEntity;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }

        [Route("servicepolicies/{httpConnectionStringEncoded}/{thumbprint}/{serviceName}")]
        [HttpGet]
        public async Task<IActionResult> GetServicePolicies(String httpConnectionStringEncoded, String thumbprint, String serviceName)
        {

            String httpConnectionString = Utility.decodeHTTPString(httpConnectionStringEncoded);

            List<PolicyStorageEntity> policyDetails = new List<PolicyStorageEntity>();
            List<string> policyNames = new List<string>();
            string mServiceName = serviceName.Replace("_", "/");
            string URL = httpConnectionString + "/";
                
            string URLParameters = "Services/" + mServiceName + "/$/GetBackupConfigurationInfo" + "?api-version=6.2-preview";

            HttpResponseMessage response = await Utility.HTTPGetAsync(URL, URLParameters, thumbprint);
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsAsync<JObject>().Result;
                JArray array = (JArray)content["Items"];
                foreach (var item in array)
                {
                    string policy = item["PolicyName"].ToString();
                    if (!policyNames.Contains(policy))
                        policyNames.Add(policy);
                }
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            foreach (string policyName in policyNames)
            {
                PolicyStorageEntity policyStorageEntity = await this.GetPolicyDetails(httpConnectionString, thumbprint, policyName);

                if (policyStorageEntity != null)
                {
                    policyDetails.Add(policyStorageEntity);
                }
                else
                {
                    return null;
                }
            }
            return this.Json(policyDetails);

        }

        [Route("apppolicies/{httpConnectionStringEncoded}/{thumbprint}/{appName}")]
        [HttpGet]
        public async Task<IActionResult> GetApplicationPolicies(String httpConnectionStringEncoded, String thumbprint, String appName)
        {

            String httpConnectionString = Utility.decodeHTTPString(httpConnectionStringEncoded); 

            List<PolicyStorageEntity> policyDetails = new List<PolicyStorageEntity>();
            List<string> policyNames = new List<string>();
            string URL = httpConnectionString + "/";

            string URLParameters = "Applications/" + appName + "/$/GetBackupConfigurationInfo" + "?api-version=6.2-preview";

            HttpResponseMessage response = await Utility.HTTPGetAsync(URL, URLParameters, thumbprint);
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsAsync<JObject>().Result;
                JArray array = (JArray)content["Items"];
                foreach (var item in array)
                {
                    string policy = item["PolicyName"].ToString();
                    if (!policyNames.Contains(policy))
                        policyNames.Add(policy);
                }
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            foreach (string policyName in policyNames)
            {
                PolicyStorageEntity policyStorageEntity = await this.GetPolicyDetails(httpConnectionString, thumbprint, policyName);

                if (policyStorageEntity != null)
                {
                    policyDetails.Add(policyStorageEntity);
                }
                else
                {
                    return null;
                }
            }
            return this.Json(policyDetails);
        }

        [HttpPost]
        [Route("configureapp/{primaryClusterAddress}/{primaryHttpEndpointEncoded}/{primaryThumbprint}/{primaryCommonName}/{secondaryClusterAddress}/{secondaryHttpEndpointEncoded}/{secondaryThumbprint}/{secondaryCommonName}")]
        public void ConfigureApplication([FromBody]JObject content, string primaryClusterAddress, string primaryHttpEndpointEncoded, string primaryThumbprint, string primaryCommonName, 
                                                                    string secondaryClusterAddress, string secondaryHttpEndpointEncoded, string secondaryThumbprint, string secondaryCommonName)
        {

            string[] primaryClusterDetails = primaryClusterAddress.Split(':');
            string[] secondaryClusterDetails = secondaryClusterAddress.Split(':');

            string primaryHttpEndpoint = Utility.decodeHTTPString(primaryHttpEndpointEncoded);
            string secondaryHttpEndpoint = Utility.decodeHTTPString(secondaryHttpEndpointEncoded);

            ClusterDetails primaryCluster = new ClusterDetails(primaryClusterDetails[0], primaryHttpEndpoint, primaryClusterAddress, primaryThumbprint, primaryCommonName);
            ClusterDetails secondaryCluster = new ClusterDetails(secondaryClusterDetails[0], secondaryHttpEndpoint, secondaryClusterAddress, secondaryThumbprint, secondaryCommonName);

            JArray applicationData = (JArray)content["ApplicationList"];
            JArray policiesData = (JArray)content["PoliciesList"];

            List<string> applicationDataObj = JsonConvert.DeserializeObject<List<string>>(applicationData.ToString());
            List<PolicyStorageEntity> policicesList = JsonConvert.DeserializeObject<List<PolicyStorageEntity>>(policiesData.ToString());

            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;

            foreach (Partition partition in partitionList)
            {
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    restoreServiceClient.ConfigureApplication(applicationDataObj[0], policicesList, primaryCluster, secondaryCluster);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception configuring the application {0}", ex);
                    throw;
                }
            }
        }

        // Calls configure service method of restore service
        [HttpPost]
        [Route("configureservice/{primaryClusterAddress}/{primaryHttpEndpointEncoded}/{primaryThumbprint}/{primaryCommonName}/{secondaryClusterAddress}/{secondaryHttpEndpointEncoded}/{secondaryThumbprint}/{secondaryCommonName}")]
        public void ConfigureService([FromBody]JObject content, string primaryClusterAddress, string primaryHttpEndpointEncoded, string primaryThumbprint, string primaryCommonName,
                                                                string secondaryClusterAddress, string secondaryHttpEndpointEncoded, string secondaryThumbprint, string secondaryCommonName)
        {
            string[] primaryClusterDetails = primaryClusterAddress.Split(':');
            string[] secondaryClusterDetails = secondaryClusterAddress.Split(':');

            string primaryHttpEndpoint = Utility.decodeHTTPString(primaryHttpEndpointEncoded);
            string secondaryHttpEndpoint = Utility.decodeHTTPString(secondaryHttpEndpointEncoded);

            ClusterDetails primaryCluster = new ClusterDetails(primaryClusterDetails[0], primaryHttpEndpoint, primaryClusterAddress, primaryThumbprint, primaryCommonName);
            ClusterDetails secondaryCluster = new ClusterDetails(secondaryClusterDetails[0], secondaryHttpEndpoint, secondaryClusterAddress, secondaryThumbprint, secondaryCommonName);

            JArray serviceData = (JArray)content["ServiceList"];
            JArray policiesData = (JArray)content["PoliciesList"];

            List<string> serviceDataObj = JsonConvert.DeserializeObject<List<string>>(serviceData.ToString());
            List<PolicyStorageEntity> policicesList = JsonConvert.DeserializeObject<List<PolicyStorageEntity>>(policiesData.ToString());

            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;

            foreach (Partition partition in partitionList)
            {
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    restoreServiceClient.ConfigureService(serviceDataObj[0], serviceDataObj[1], policicesList, primaryCluster, secondaryCluster);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception configuring the service {0}", ex);
                    throw;
                }
            }
        }

        [HttpPost]
        [Route("updatepolicy/{primaryHttpEndpointEncoded}/{primaryClusterThumbprint}")]
        public void UpdatePolicy([FromBody]JObject content, string primaryHttpEndpointEncoded, string primaryClusterThumbprint)
        {

            string primaryClusterAddress = Utility.decodeHTTPString(primaryHttpEndpointEncoded);

            JArray policiesData = (JArray)content["PoliciesList"];
            List<PolicyStorageEntity> policicesList = JsonConvert.DeserializeObject<List<PolicyStorageEntity>>(policiesData.ToString());

            IPolicyStorageService policyStorageClient = ServiceProxy.Create<IPolicyStorageService>(new Uri("fabric:/SFAppDRTool/PolicyStorageService"));

            try
            {
                bool updateOverwritePolicy = true;
                policyStorageClient.PostStorageDetails(policicesList, primaryClusterAddress, primaryClusterThumbprint, updateOverwritePolicy);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message("Web Service: Exception updating the stored polices {0}", ex);
                throw;
            }
        }


        /// <summary>
        /// Disconfigures the application for standby by calling disconfigure of the restore service method
        /// </summary>
        /// <param name="applicationName"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("disconfigureapp/{primaryCluster}/{secondaryCluster}")]
        public async Task<string> DisconfigureApplication([FromBody]JObject content, String primaryCluster, String secondaryCluster)
        {
            bool successfullyRemoved = true;

            JArray applicationData = (JArray)content["ApplicationList"];
            List<string> applicationDataObj = JsonConvert.DeserializeObject<List<string>>(applicationData.ToString());

            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;
            foreach (Partition partition in partitionList)
            {
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    string applicationRemoved = await restoreServiceClient.DisconfigureApplication(applicationDataObj[0], primaryCluster, secondaryCluster);
                    if(applicationRemoved == null) successfullyRemoved = false;
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception Disconfiguring Application {0}", ex);
                    throw;
                }
            }
            if (successfullyRemoved) return applicationDataObj[0];
            return null;
        }

        [HttpPost]
        [Route("disconfigureservice/{primaryCluster}/{secondaryCluster}")]
        public async Task<string> DisconfigureService([FromBody]JObject content, String primaryCluster, String secondaryCluster)
        {
            bool successfullyRemoved = true;

            JArray serviceData = (JArray)content["ServiceList"];
            List<string> serviceDataObj = JsonConvert.DeserializeObject<List<string>>(serviceData.ToString());

            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;
            foreach (Partition partition in partitionList)
            {
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    string serviceRemoved = await restoreServiceClient.DisconfigureService(serviceDataObj[0], primaryCluster, secondaryCluster);
                    if (serviceRemoved == null) successfullyRemoved = false;
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception Disconfiguring Service {0}", ex);
                    throw;
                }
            }
            if (successfullyRemoved) return serviceDataObj[0];
            return null;
        }

        /// <summary>
        /// This calls GetStatus method of restore service.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("status")]
        public async Task<IEnumerable<PartitionWrapper>> GetPartitionStatus()
        {
            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;

            List<PartitionStatusModel> partitionStatusList = new List<PartitionStatusModel>();
            List<PartitionWrapper> mappedPartitions = new List<PartitionWrapper>();

            foreach (Partition partition in partitionList)
            {
                List<PartitionWrapper> servicePartitions = new List<PartitionWrapper>();
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    servicePartitions = await restoreServiceClient.GetStatus();
                    mappedPartitions.AddRange(servicePartitions);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception getting the status {0}", ex);
                    throw;
                }
            }

            //if (mappedPartitions.Count == 0) return null;
            return mappedPartitions;
        }

        [HttpGet]
        [Route("clustercombinations")]
        public async Task<IEnumerable<Tuple<ClusterDetails, ClusterDetails>>> GetClusterCombinations()
        {
            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;

            List<Tuple<ClusterDetails, ClusterDetails>> allClusterCombinations = new List<Tuple<ClusterDetails, ClusterDetails>>();

            foreach (Partition partition in partitionList)
            {
                List<Tuple<ClusterDetails, ClusterDetails>> localClusterCombinations = new List<Tuple<ClusterDetails, ClusterDetails>>();
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    localClusterCombinations = await restoreServiceClient.GetClusterCombinations();
                    allClusterCombinations.AddRange(localClusterCombinations);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception getting cluster combinations {0}", ex);
                    throw;
                }
            }

            return allClusterCombinations;
        }

        public async Task<List<String>> GetStoredPolicies()
        {
            FabricClient fabricClient = new FabricClient();
            List<String> storedPolicies = new List<String>();

            IPolicyStorageService policyStorageClient = ServiceProxy.Create<IPolicyStorageService>(new Uri("fabric:/SFAppDRTool/PolicyStorageService"));

            try
            {
                storedPolicies = await policyStorageClient.GetAllStoredPolicies();
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message("Web Service: Exception getting the stored polices {0}", ex);
                throw;
            }

            return storedPolicies;
        }

        public async Task<HashSet<String>> GetConfiguredServices()
        {
            IEnumerable<PartitionWrapper> configuredPartitions = await GetPartitionStatus();
            HashSet<String> configuredServices = new HashSet<String>();
            foreach (var partition in configuredPartitions)
            {
                configuredServices.Add(partition.serviceName.ToString());
            }
            return configuredServices;
        }

        public async Task<HashSet<String>> GetConfiguredApplications(String primarycs, String secondarycs)
        {
            FabricClient fabricClient = new FabricClient();
            ServicePartitionList partitionList = fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/SFAppDRTool/RestoreService")).Result;

            List<String> configuredApplicationNames = new List<String>();

            foreach (Partition partition in partitionList)
            {
                List<String> configAppNames = new List<String>();
                var int64PartitionInfo = partition.PartitionInformation as Int64RangePartitionInformation;
                long lowKey = (long)int64PartitionInfo?.LowKey;
                IRestoreService restoreServiceClient = ServiceProxy.Create<IRestoreService>(new Uri("fabric:/SFAppDRTool/RestoreService"), new ServicePartitionKey(lowKey));
                try
                {
                    configAppNames = await restoreServiceClient.GetConfiguredApplicationNames(primarycs, secondarycs);
                    configuredApplicationNames.AddRange(configAppNames);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.Message("Web Service: Exception getting all configured application names {0}", ex);
                    throw;
                }
            }

            return new HashSet<String>(configuredApplicationNames);
        }

    }


}
