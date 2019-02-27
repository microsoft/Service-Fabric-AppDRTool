// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace WebInterface.Models
{
    [DataContract]
    public class PartitionStatusModel
    {

        public PartitionStatusModel(Uri applicationName, Uri serviceName, string partitionId, string mappedPartitionId, string lastBackupRestored, string backupId)
        {
            this.applicationName = applicationName;
            this.serviceName = serviceName;
            this.partitionId = partitionId;
            this.lastBackupRestored = lastBackupRestored;
            this.backupId = backupId;
            this.mappedPartitionId = mappedPartitionId;
        }

        public PartitionStatusModel(Uri applicationName, Uri serviceName, string partitionId, string mappedPartitionId)
        {
            this.applicationName = applicationName;
            this.serviceName = serviceName;
            this.partitionId = partitionId;
            this.mappedPartitionId = mappedPartitionId;
        }

        [DataMember]
        Uri applicationName { get; set; }

        [DataMember]
        Uri serviceName { get; set; }

        [DataMember]
        string partitionId { get; set; }

        [DataMember]
        string mappedPartitionId { get; set; }

        [DataMember]
        string lastBackupRestored { get; set; }

        [DataMember]
        string backupId { get; set; }
    }
}
