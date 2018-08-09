using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestoreService
{
    class RestoreResult
    {
        public RestoreResult(BackupInfo restoreInfo, string restoreState)
        {
            this.restoreInfo = restoreInfo;
            this.restoreState = restoreState;
        }

        public BackupInfo restoreInfo;

        public string restoreState;

    }
}
