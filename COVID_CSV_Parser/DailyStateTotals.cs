using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AP_EarlyVoting_Parser
{
    public class EarlyVotingStateData
    {
        public string statePostal { get; set; }
        public string mailOrAbsBallotsRequested { get; set; }
        public string mailOrAbsBallotsSent { get; set; }
        public string mailOrAbsBallotsCast { get; set; }
        public string earlyInPersonCast { get; set; }
        public string totalAdvVotesCast { get; set; }
        public string dateofLastUpdate { get; set; }
        public DateTime updateDateTime { get; set; }
    }
}
