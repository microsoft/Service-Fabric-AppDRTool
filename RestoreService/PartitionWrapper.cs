using Newtonsoft.Json.Linq;
using PolicyStorageService;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RestoreService
{
    [Serializable]
    [DataContract]
    public class PartitionWrapper
    {
        [DataMember]
        public ClusterDetails primaryCluster;

        [DataMember]
        public ClusterDetails secondaryCluster;

        [DataMember]
        public Uri applicationName;

        [DataMember]
        public Uri serviceName;

        [DataMember]
        public Guid partitionId { get; set; }

        [DataMember]
        public Guid primaryPartitionId { get; set; }

        [DataMember]
        public BackupInfo LatestBackupAvailable { get; set; }

        [DataMember]
        public BackupInfo LastBackupRestored { get; set; }

        [DataMember]
        public BackupInfo CurrentlyUnderRestore { get; set; }

        [DataMember]
        public String RestoreState { get; set; }

        public ServiceKind ServiceKind { get; set; }

        public HealthState HealthState { get; set; }

        public ServicePartitionInformation PartitionInformation { get;  set; }

        public ServicePartitionStatus PartitionStatus { get;  set; }

        public PartitionWrapper(Partition partition, Guid primaryPartitionId, Uri applicationName, Uri serviceName, ClusterDetails primaryCluster, ClusterDetails secondaryCluster)
        {
            this.partitionId = partition.PartitionInformation.Id;
            this.primaryPartitionId = primaryPartitionId;
            this.PartitionInformation = partition.PartitionInformation;
            this.ServiceKind = partition.ServiceKind;
            this.HealthState = partition.HealthState;
            this.PartitionStatus = partition.PartitionStatus;
            this.applicationName = applicationName;
            this.serviceName = serviceName;
            this.primaryCluster = primaryCluster;
            this.secondaryCluster = secondaryCluster;
        }

        public PartitionWrapper(PartitionWrapper partitionWrapper)
        {
            this.partitionId = partitionWrapper.PartitionInformation.Id;
            this.PartitionInformation = partitionWrapper.PartitionInformation;
            this.ServiceKind = partitionWrapper.ServiceKind;
            this.HealthState = partitionWrapper.HealthState;
            this.PartitionStatus = partitionWrapper.PartitionStatus;
        }
    }

    [DataContract]
    public class BackupInfo
    {
        [DataMember]
        public string backupId { get; set; }

        [DataMember]
        public string backupLocation { get; set; }

        [DataMember]
        public DateTime backupTime { get; set; }

        [DataMember]
        public BackupStorage backupStorage;

        public BackupInfo(string backupId, string backupLocation, BackupStorage backupStorage, DateTime backupTime)
        {
            this.backupId = backupId;
            this.backupLocation = backupLocation;
            this.backupStorage = backupStorage;
            this.backupTime = backupTime;
        }

        public BackupInfo(string backupId, string backupLocation, DateTime backupTime)
        {
            this.backupId = backupId;
            this.backupLocation = backupLocation;
            this.backupTime = backupTime;
        }
    }

    [DataContract]
    public class ClusterDetails
    {
        [DataMember]
        public string address { get; set; }

        [DataMember]
        public string httpEndpoint { get; set; }

        [DataMember]
        public string clientConnectionEndpoint { get; set; }

        [DataMember]
        public string certificateThumbprint { get; set;  }

        [DataMember]
        public string commonName { get; set; }

        public ClusterDetails(string address, string httpEndpoint, string clientConnectionEndpoint)
        {
            this.address = address;
            this.httpEndpoint = httpEndpoint;
            this.clientConnectionEndpoint = clientConnectionEndpoint;
        }

        public ClusterDetails(string address, string httpEndpoint, string clientConnectionEndpoint, string certificateThumbprint, string commonName)
        {
            this.address = address;
            this.httpEndpoint = httpEndpoint;
            this.clientConnectionEndpoint = clientConnectionEndpoint;
            this.certificateThumbprint = certificateThumbprint;
            this.commonName = commonName;
        }
    }
}
