using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PolicyStorageService
{
[DataContract]
    public class BackupStorage
    {
        [DataMember]
        public String StorageKind;

        [DataMember]
        public String connectionString;

        [DataMember]
        public String path;

        [DataMember]
        public String primaryUsername;

        [DataMember]
        public String primaryPassword;

        [DataMember]
        public String secondaryUsername;

        [DataMember]
        public String secondaryPassword;

        [DataMember]
        public String friendlyname;

        [DataMember]
        public String containerName;

        public BackupStorage()
        {

        }
    }
}
