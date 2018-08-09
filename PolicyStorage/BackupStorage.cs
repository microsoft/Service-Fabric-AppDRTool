using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PolicyStorage
{
    [DataContract]
    class BackupStorage
    {
        [DataMember]
        public String storageKind;

        [DataMember]
        public String connectionString;

        [DataMember]
        public String path;

        [DataMember]
        public String primaryUserName;

        [DataMember]
        public String primaryPassword;

        [DataMember]
        public String secondaryUserName;

        [DataMember]
        public String secondaryPassword;

        [DataMember]
        public String friendlyName;

        [DataMember]
        public String containerName;

        public BackupStorage(String storageKind, String path)
        {
            this.storageKind = storageKind;
            this.path = path;
        }
    }
}
