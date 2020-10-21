using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AP_EarlyVoting_Parser
{
    public class StateAdvanceVotingDataCollection
    {
        #region Properties and Members
        public List<EarlyVotingStateData> EarlyVotingStateDataObjects;
        private int _collectionCount;

        private int collectionCount
        {
            get { return _collectionCount; }
            set { _collectionCount = value; }
        }

        public int CollectionCount
        {
            get { return collectionCount; }
            set { collectionCount = value; }
        }
        #endregion

        #region Public Methods
        // Constructor - instantiates list collection
        public StateAdvanceVotingDataCollection()
        {
            // Create list of candidate data for specified race
            EarlyVotingStateDataObjects = new List<EarlyVotingStateData> ();
        }

        /// <summary>
        /// Get an empty list collection
        /// </summary>
        public List<EarlyVotingStateData> GetEarlyVotingStateDataCollection()
        {
            // Clear out the current collection
            EarlyVotingStateDataObjects.Clear();

            // Return empty list
            return EarlyVotingStateDataObjects;
        }

        /// <summary>
        /// Append an element into the graphics channel data collection at the specified location
        /// </summary>
        public void AppendEarlyVotingStateDataObject(EarlyVotingStateData earlyVotingStateDataObject)
        {
            try
            {
                EarlyVotingStateDataObjects.Add(earlyVotingStateDataObject);
                collectionCount = EarlyVotingStateDataObjects.Count;
            }
            catch (Exception ex)
            {
                // Log error
            }
        }
        #endregion
    }
}
