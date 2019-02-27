// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

        public BackupStorage DeepCopy()
        {
            BackupStorage newBackupStorage = (BackupStorage) this.MemberwiseClone();
            if (connectionString != null)
            {
                newBackupStorage.connectionString = String.Copy(connectionString);
            }
            if (containerName != null)
            {
                newBackupStorage.containerName = String.Copy(containerName);
            }
            if (friendlyname != null)
            {
                newBackupStorage.friendlyname = String.Copy(friendlyname);
            }
            if (path != null)
            {
                newBackupStorage.path = String.Copy(path);
            }
            if (primaryPassword != null)
            {
                newBackupStorage.primaryPassword = String.Copy(primaryPassword);
            }
            if (primaryUsername != null)
            {
                newBackupStorage.primaryUsername = String.Copy(primaryUsername);
            }
            if (secondaryPassword != null)
            {
                newBackupStorage.secondaryPassword = String.Copy(secondaryPassword);
            }
            if (secondaryUsername != null)
            {
                newBackupStorage.secondaryUsername = String.Copy(secondaryUsername);
            }
            if (StorageKind != null)
            {
                newBackupStorage.StorageKind = String.Copy(StorageKind);
            }
            return newBackupStorage;
        }

        public void Encrypt()
        {
            if (connectionString != null)
            {
                this.connectionString = EncryptionUtil.Encrypt(connectionString);
            }
            if (primaryPassword != null)
            {
                this.primaryPassword = EncryptionUtil.Encrypt(primaryPassword);
            }
            if (secondaryPassword != null)
            {
                this.secondaryPassword = EncryptionUtil.Encrypt(secondaryPassword);
            }
        }

        public void Decrypt()
        {
            if (connectionString != null)
            {
                this.connectionString = EncryptionUtil.Decrypt(connectionString);
            }
            if (primaryPassword != null)
            {
                this.primaryPassword = EncryptionUtil.Decrypt(primaryPassword);
            }
            if (secondaryPassword != null)
            {
                this.secondaryPassword = EncryptionUtil.Decrypt(secondaryPassword);
            }
        }
    }
}
