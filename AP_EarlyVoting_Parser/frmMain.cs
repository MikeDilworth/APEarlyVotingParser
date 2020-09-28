using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Threading;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Configuration;
using System.Net;
using System.Timers;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Xml;


namespace AP_EarlyVoting_Parser
{
    public partial class frmMain : Form
    {
        // Setup configuration
        public static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        //public static SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
        //public static SqlCommand cmd = new SqlCommand();

        // Schedule timer
        static System.Timers.Timer timer;
        static DateTime nowTime = DateTime.Now;
        static DateTime scheduledTime;

        // Set data URL
        public string urlForData = "https://api.ap.org/v2/reports/d34c0da52f2b44389de43a43f05a2289?apikey=xEOmjspBfmqSpyWx5hIPfrDWxsbrKrSv";

        // Declare background worker thread
        private BackgroundWorker backgroundWorker1;


        public frmMain()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            InitializeComponent();

            // Setup the background worker thread
            backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += new DoWorkEventHandler(BackgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker1_RunWorkerCompleted);
        }

        // Method to read in the latest state-level early voting data file and post the results to the SQL DB
        private void GetLatestData()
        {
            try
            {
                // Setup stopwatch
                System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
                elapsed.Start();

                // These directives needed to prevent security error on HTTP request
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var stateDataRecord = new EarlyVotingStateData
                {
                };

                XmlDocument x = new XmlDocument();
                x.Load(urlForData);

                XmlNodeList xList = x.SelectNodes("//stateAdvanceTurnout");

                SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
                SqlCommand cmd = new SqlCommand();

                int rowCount = 0;

                //Iterates thru each race
                foreach (XmlNode a in xList)
                {
                    try
                    {
                        stateDataRecord.statePostal = a["statePostal"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.statePostal = "N/A";
                    }
                    try
                    {
                        stateDataRecord.mailOrAbsBallotsRequested = a["mailOrAbsBallotsRequested"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.mailOrAbsBallotsRequested = "N/A";
                    }
                    try
                    {
                        stateDataRecord.mailOrAbsBallotsSent = a["mailOrAbsBallotsSent"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.mailOrAbsBallotsSent = "N/A";
                    }
                    try
                    {
                        stateDataRecord.mailOrAbsBallotsCast = a["mailOrAbsBallotsCast"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.mailOrAbsBallotsCast = "N/A";
                    }
                    try
                    {
                        stateDataRecord.earlyInPersonCast = a["earlyInPersonCast"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.earlyInPersonCast = "N/A";
                    }
                    try
                    {
                        stateDataRecord.totalAdvVotesCast = a["totalAdvVotesCast"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.totalAdvVotesCast = "N/A";
                    }
                    try
                    {
                        stateDataRecord.dateofLastUpdate = a["dateofLastUpdate"].InnerText;
                    }
                    catch (NullReferenceException e)
                    {
                        stateDataRecord.dateofLastUpdate = "N/A";
                    }
                    stateDataRecord.updateDateTime = DateTime.Now;

                    try
                    {
                        rowCount++;

                        conn.Open();
                        cmd = new SqlCommand(config.AppSettings.Settings["storedProcedure"].Value, conn);
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add(new SqlParameter("@StateMnemonic", stateDataRecord.statePostal));

                        cmd.Parameters.Add(new SqlParameter("@Year", 2020));

                        int ballotsSentRequested = Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsSent, 0) + Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsRequested, 0);
                        cmd.Parameters.Add(new SqlParameter("@BallotsSentRequested", ballotsSentRequested));

                        int ballotsReturned = Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsCast, 0);
                        cmd.Parameters.Add(new SqlParameter("@BallotsReturned", ballotsReturned));

                        int earlyInPersonVote = Extensions.ParseInt(stateDataRecord.earlyInPersonCast, 0);
                        cmd.Parameters.Add(new SqlParameter("@EarlyInPersonVote", earlyInPersonVote));

                        int totalAdvanceTurnout = Extensions.ParseInt(stateDataRecord.totalAdvVotesCast, 0);
                        cmd.Parameters.Add(new SqlParameter("@TotalAdvanceTurnout", totalAdvanceTurnout));

                        cmd.Parameters.Add(new SqlParameter("@DateOfLastUpdate", stateDataRecord.dateofLastUpdate));

                        //cmd.Parameters.Add(new SqlParameter("@UpdateDateTime", SqlDbType.DateTime).Value = stateDataRecord.updateDateTime);

                        cmd.ExecuteNonQuery();

                        conn.Close();

                        logTxt.AppendText("Processed data for state: " + stateDataRecord.statePostal + Environment.NewLine);
                    }
                    catch (Exception e)
                    {
                        logTxt.AppendText("Error occurred during database posting on row " + rowCount.ToString() + ": " + e.Message + Environment.NewLine);
                    }
                }
              
                conn.Close();
                // Display stats for processing
                elapsed.Stop();
                logTxt.AppendText(Environment.NewLine + "Last state data update processed: " + DateTime.Now.ToString() + Environment.NewLine);
                logTxt.AppendText("Data rows processed: " + rowCount.ToString() + Environment.NewLine);
                logTxt.AppendText("Total elapsed time (seconds): " + elapsed.Elapsed.TotalSeconds + Environment.NewLine);
                txtStatus.Text = "State-Level data early voting data update completed at: " + DateTime.Now.ToString();

            }
            catch (Exception e)
            {
                txtStatus.Text = "Error occurred during state-level data retrieval and database posting: " + e.Message;
            }
        }

        // Here's the scheduling timer
        void schedule_Timer()
        {
            //Console.WriteLine("### Timer Started ###");
           //logTxt.AppendText("### Timer Started ###" + Environment.NewLine);

           nowTime = DateTime.Now;
           scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 06, 0, 0, 0); // Start at 6:00 AM

            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (double)(scheduledTime - DateTime.Now).TotalMilliseconds;
            timer = new System.Timers.Timer(tickTime);
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();

            logTxt.AppendText("Timer set to fire at: " + scheduledTime.ToString() + Environment.NewLine);
        }

        //static void timer_Elapsed(object sender, ElapsedEventArgs e)
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Update status label
            txtStatus.Text = "Update timer fired at " + DateTime.Now.ToString();

            // Call method with flag set to download the latest data; if false, filename is specified as 2nd parameter
            //GetLatestData_County(true, String.Empty);

            GetLatestData();

            // Stop the timer
            timer.Stop();

            // Restart
            schedule_Timer();
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            // Sleep 2 seconds to emulate getting data.
            Thread.Sleep(2000);
            e.Result = "This text was set safely by BackgroundWorker.";
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            txtStatus.Text = e.Result.ToString();
        }

        private void btnGetLatestStateData_Click(object sender, EventArgs e)
        {
            GetLatestData();
        }

        // Start timer on form activation
        private void frmMain_Activated(object sender, EventArgs e)
        {
           
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            // Init timer
            schedule_Timer();
        }
    }
    public static class Extensions
    {
        public static int ParseInt(this string value, int defaultIntValue = 0)
        {
            int parsedInt;
            if (int.TryParse(value, out parsedInt))
            {
                return parsedInt;
            }

            return defaultIntValue;
        }
    }


}
