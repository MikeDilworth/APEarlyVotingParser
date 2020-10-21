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
using System.Net.Mail;

namespace AP_EarlyVoting_Parser
{
    public partial class frmMain : Form
    {
        // Setup configuration
        public static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        
        private static StateAdvanceVotingDataCollection _stateAdvanceVotingDataCollection;
        private static List<EarlyVotingStateData> _stateAdvanceVotingDataObjects;

        //public static SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
        //public static SqlCommand cmd = new SqlCommand();

        // Schedule timer
        static System.Timers.Timer timer;
        static System.Timers.Timer timer2;
        static DateTime nowTime = DateTime.Now;
        static DateTime scheduledTime;
        static DateTime scheduledTime2;

        // Set URLs
        public string urlForReports = "https://api.ap.org/v2/reports?apikey=xEOmjspBfmqSpyWx5hIPfrDWxsbrKrSv&type=advvotes";
        public string urlForData = string.Empty; 
        public string apiKeyString = "?apikey=xEOmjspBfmqSpyWx5hIPfrDWxsbrKrSv";

        public String dataBody = string.Empty;

        // Declare background worker thread
        private BackgroundWorker backgroundWorker1;

        public frmMain()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            InitializeComponent();

            _stateAdvanceVotingDataCollection = new StateAdvanceVotingDataCollection();
            _stateAdvanceVotingDataObjects = _stateAdvanceVotingDataCollection.GetEarlyVotingStateDataCollection();

            // Setup the background worker thread
            backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += new DoWorkEventHandler(BackgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker1_RunWorkerCompleted);
        }

        // Method to read in the latest state-level early voting data file and post the results to the SQL DB
        private void GetLatestData()
        //private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // Setup stopwatch
                System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
                elapsed.Start();

                // These directives needed to prevent security error on HTTP request
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                EarlyVotingStateData stateDataRecord = new EarlyVotingStateData
                {
                };

                // Get list of reports
                XmlDocument reports = new XmlDocument();

                // Setup namespace for parsing atom feed; force name "Atom" for namespace
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(reports.NameTable);
                nsmgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");

                // Load the Atom feed for the reports
                reports.Load(urlForReports);

                // Get the list of reports
                //XmlNodeList reportsList = reports.SelectNodes("//entry");
                XmlNodeList reportsList = reports.DocumentElement.SelectNodes("atom:entry", nsmgr);

                // Get the report for the advance turnout
                //Iterates thru each race
                foreach (XmlNode report in reportsList)
                {
                    try
                    {
                        // If it's the advance turnout report, build the URL string for the data
                        if (report["title"].InnerText == "AdvVotes / advturnout2020GE")
                        {
                            urlForData = report["id"].InnerText + apiKeyString;
                        }
                    }
                    //catch (Exception e)
                    catch (Exception)
                    {
                        //logTxt.AppendText("Error occurred while trying to retrieve report name:" + e.Message + Environment.NewLine);
                        logTxt.AppendText("Error occurred while trying to retrieve report name" + Environment.NewLine);
                    }
                }

                if (urlForData != string.Empty)
                {
                    // Get data
                    XmlDocument x = new XmlDocument();

                    x.Load(urlForData);

                    XmlNodeList xList = x.SelectNodes("//stateAdvanceTurnout");

                    SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
                    SqlCommand cmd = new SqlCommand();

                    int rowCount = 0;

                    // Clear the collection
                    _stateAdvanceVotingDataObjects.Clear();

                    //Iterates thru each race
                    foreach (XmlNode a in xList)
                    {     
                        try
                        {
                            stateDataRecord.statePostal = a["statePostal"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.statePostal = "N/A";
                        }
                        try
                        {
                            stateDataRecord.mailOrAbsBallotsRequested = a["mailOrAbsBallotsRequested"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.mailOrAbsBallotsRequested = "N/A";
                        }
                        try
                        {
                            stateDataRecord.mailOrAbsBallotsSent = a["mailOrAbsBallotsSent"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.mailOrAbsBallotsSent = "N/A";
                        }
                        try
                        {
                            stateDataRecord.mailOrAbsBallotsCast = a["mailOrAbsBallotsCast"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.mailOrAbsBallotsCast = "N/A";
                        }
                        try
                        {
                            stateDataRecord.earlyInPersonCast = a["earlyInPersonCast"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.earlyInPersonCast = "N/A";
                        }
                        try
                        {
                            stateDataRecord.totalAdvVotesCast = a["totalAdvVotesCast"].InnerText;
                        }
                        catch (NullReferenceException)
                        {
                            stateDataRecord.totalAdvVotesCast = "N/A";
                        }
                        try
                        {
                            stateDataRecord.dateofLastUpdate = a["dateofLastUpdate"].InnerText;
                        }
                        catch (NullReferenceException)
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


                            //int ballotsSentRequested = Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsSent, 0);// + Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsRequested, 0);
                            //cmd.Parameters.Add(new SqlParameter("@BallotsSentRequested", ballotsSentRequested));

                            int ballotsSent = Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsSent, 0);
                            int ballotsRequested = Extensions.ParseInt(stateDataRecord.mailOrAbsBallotsRequested, 0);
                            int ballotsSentRequested = 0;

                            if (ballotsRequested > ballotsSent)
                            {
                                cmd.Parameters.Add(new SqlParameter("@BallotsSentRequested", ballotsRequested));
                                ballotsSentRequested = ballotsRequested;
                            }
                            else
                            {
                                cmd.Parameters.Add(new SqlParameter("@BallotsSentRequested", ballotsSent));
                                ballotsSentRequested = ballotsSent;
                            }

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

                            // Graphics channel object with patching IP & port not found, so instantiate the graphics channel object & set properties
                            EarlyVotingStateData earlyVotingStateData = new EarlyVotingStateData
                            {
                                statePostal = stateDataRecord.statePostal,
                                mailOrAbsBallotsRequested = ballotsSentRequested.ToString(),
                                mailOrAbsBallotsSent = ballotsSentRequested.ToString(),
                                mailOrAbsBallotsCast = stateDataRecord.mailOrAbsBallotsCast,
                                earlyInPersonCast = stateDataRecord.earlyInPersonCast,
                                totalAdvVotesCast = stateDataRecord.totalAdvVotesCast,
                                dateofLastUpdate = stateDataRecord.dateofLastUpdate,
                                updateDateTime = stateDataRecord.updateDateTime
                            };
                            _stateAdvanceVotingDataCollection.AppendEarlyVotingStateDataObject(earlyVotingStateData);

                            logTxt.AppendText("Processed data for state: " + stateDataRecord.statePostal + Environment.NewLine);
                        }
                        catch (Exception e)
                        //catch (Exception)
                        {
                            logTxt.AppendText("Error occurred during database posting on row " + rowCount.ToString() + ": " + e.Message + Environment.NewLine);
                            //logTxt.AppendText("Error occurred during database posting on row " + rowCount.ToString() + Environment.NewLine);
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
                else
                {
                    logTxt.AppendText("Could not determine URL for advance voting report" + Environment.NewLine);
                }

            }
            catch (Exception e)
            //catch (Exception)
            {
                txtStatus.Text = "Error occurred during state-level data retrieval and database posting: " + e.Message;
                //txtStatus.Text = "Error occurred during state-level data retrieval and database posting";
            }
        }

        // Here's the scheduling timer
        // Setup timer for 6:00 AM
        void schedule_Timer()
        {
            //Console.WriteLine("### Timer Started ###");
           logTxt.AppendText("### Timer #1 Started ###" + Environment.NewLine);

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

            logTxt.AppendText("Timer #1 set to fire at: " + scheduledTime.ToString() + Environment.NewLine);
        }

        // Setup timer for 6:00 PM
        void schedule_Timer2()
        {
            //Console.WriteLine("### Timer Started ###");
            logTxt.AppendText("### Timer #2 Started ###" + Environment.NewLine);

            nowTime = DateTime.Now;
            scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 18, 0, 0, 0); // Start at 6:00 PM

            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (double)(scheduledTime - DateTime.Now).TotalMilliseconds;
            timer2 = new System.Timers.Timer(tickTime);
            timer2.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer2.Start();

            logTxt.AppendText("Timer #2 set to fire at: " + scheduledTime.ToString() + Environment.NewLine);
        }

        // Timer #1 elapsed event
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Update status label
            //txtStatus.Text = "Update timer fired at " + DateTime.Now.ToString();
            logTxt.AppendText("Update timer #1 fired at " + DateTime.Now.ToString() + Environment.NewLine);

            GetLatestData();

            // Send the data e-mail
            sendDataEMail();

            // Stop the timer
            timer.Stop();

            // Restart
            schedule_Timer();
        }

        // Timer #2 elapsed event
        void timer2_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Update status label
            //txtStatus.Text = "Update timer fired at " + DateTime.Now.ToString();
            logTxt.AppendText("Update timer #2 fired at " + DateTime.Now.ToString() + Environment.NewLine);

            GetLatestData();

            // Send the data e-mail
            sendDataEMail();

            // Stop the timer
            timer2.Stop();

            // Restart
            schedule_Timer2();
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            GetLatestData();

            // Send the data e-mail
            sendDataEMail();
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            txtStatus.Text = e.Result.ToString();
        }

        private void btnGetLatestStateData_Click(object sender, EventArgs e)
        {
            GetLatestData();

            // Send the data e-mail
            sendDataEMail();
        }

        // Start timer on form activation
        private void frmMain_Activated(object sender, EventArgs e)
        {
           
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            // Init timers
            schedule_Timer();
            schedule_Timer2();
        }

        public static void sendDataEMail()
        {           
            try
            {
                string messageBody = "<font>Latest Advanced Voting Data from the Associated Press as of: " + DateTime.Now.ToString() + "</font><br><br>";
                //if (grid.RowCount == 0) return messageBody;
                string htmlTableStart = "<table style=\"border-collapse:collapse; text-align:center;\" >";
                string htmlTableEnd = "</table>";
                string htmlHeaderRowStart = "<tr style=\"background-color:#6FA1D2; color:#ffffff;\">";
                string htmlHeaderRowEnd = "</tr>";
                string htmlTrStart = "<tr style=\"color:#555555;\">";
                string htmlTrEnd = "</tr>";
                string htmlTdStart = "<td style=\" border-color:#5c87b2; border-style:solid; border-width:thin; padding: 5px;\">";
                string htmlTdEnd = "</td>";
                messageBody += htmlTableStart;
                messageBody += htmlHeaderRowStart;
                messageBody += htmlTdStart + "State Postal" + htmlTdEnd;
                messageBody += htmlTdStart + "Ballots Sent/Requested" + htmlTdEnd;
                messageBody += htmlTdStart + "Ballots Cast" + htmlTdEnd;
                messageBody += htmlTdStart + "In Person Vote" + htmlTdEnd;
                messageBody += htmlTdStart + "Total Advance Vote" + htmlTdEnd;
                messageBody += htmlHeaderRowEnd;
                int totalMailOrAbsBallotsRequested = 0;
                int totalMailOrAbsBallotsCast = 0;
                int totalEarlyInPersonCast = 0;
                int totalTotalAdvVotesCast = 0;

                //Loop all the rows from grid vew and added to html td  
                for (int i = 0; i <= _stateAdvanceVotingDataCollection.CollectionCount - 1; i++)
                {
                    messageBody = messageBody + htmlTrStart;
                    messageBody = messageBody + htmlTdStart + _stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].statePostal + htmlTdEnd; 
                    messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].mailOrAbsBallotsRequested)) + htmlTdEnd;
                    messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].mailOrAbsBallotsCast)) + htmlTdEnd;
                    messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].earlyInPersonCast)) + htmlTdEnd;
                    messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].totalAdvVotesCast)) + htmlTdEnd;
                    messageBody = messageBody + htmlTrEnd;

                    // Sum up totals & add to table
                    totalMailOrAbsBallotsRequested += Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].mailOrAbsBallotsRequested);
                    totalMailOrAbsBallotsCast += Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].mailOrAbsBallotsCast);
                    totalEarlyInPersonCast += Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].earlyInPersonCast);
                    totalTotalAdvVotesCast += Extensions.ParseInt(_stateAdvanceVotingDataCollection.EarlyVotingStateDataObjects[i].totalAdvVotesCast);
                }

                // Add totals
                messageBody = messageBody + htmlTdStart + " " + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + " " + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + " " + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + " " + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + " " + htmlTdEnd;
                messageBody = messageBody + htmlTrEnd;
                messageBody = messageBody + htmlTdStart + "U.S. TOTAL" + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", totalMailOrAbsBallotsRequested) + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", totalMailOrAbsBallotsCast) + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", totalEarlyInPersonCast) + htmlTdEnd;
                messageBody = messageBody + htmlTdStart + String.Format("{0:n0}", totalTotalAdvVotesCast) + htmlTdEnd;
                messageBody = messageBody + htmlTrEnd;

                messageBody = messageBody + htmlTableEnd;

                MailMessage mail = new MailMessage("ElectionData@foxnews.com", "seniorproducers@foxnews.com, producers@foxnews.com, brainroom@foxnews.com, politics3@foxnews.com, mike.dilworth@foxnews.com"); //config.AppSettings.Settings["toEmail"].Value);
                SmtpClient mailClient = new SmtpClient();
                mailClient.Port = 25;
                mailClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                mailClient.UseDefaultCredentials = true;
                mailClient.Host = "10.232.16.121";
                mail.Subject = "Latest AP Advanced Voting Data";
                //mail.Body = Environment.NewLine + "Latest Advance Voting Data from the Associated Press as of " + DateTime.Now.ToString() + Environment.NewLine;
                mail.Body += messageBody;
                mail.IsBodyHtml = true;
                mailClient.Send(mail);
            }
            catch (Exception ex)
            {
                //txtStatus.Text = "Error occurred during state-level data retrieval and database posting: " + ex.Message;
            }
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
