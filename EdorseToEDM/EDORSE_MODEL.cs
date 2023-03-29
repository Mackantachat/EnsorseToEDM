using ITUtility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdorseToEDM
{
 public   class EDORSE_MODEL
    {
        public List<filedetail> filedetail { get; set; }
        public string ErrorMessage { get; set; }
        public string FLAG_ERROR { get; set; }
        public long? POLICY_ID { get; set; }
        public long? PS_ID { get; set; }
        public string POLICY_NUMBER { get; set; }
        public string APP_NO { get; set; }
        public string CHANNEL_TYPE { get; set; }
        public string CERT_NUMBER { get; set; }
        public DataFile file { get; set; }
    }
    public class filedetail
    {
        public string FILE_NAME { get; set; }
        public string FULL_FILE_NAME { get; set; }

    }
}
