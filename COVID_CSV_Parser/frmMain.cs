using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileHelpers;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Configuration;
using System.Net;
using System.IO;
using System.Globalization;
using System.Timers;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace COVID_CSV_Parser
{
    // Set options for FileHelpers class
    [DelimitedRecord(",")]
    [IgnoreEmptyLines()]
    [IgnoreFirst()]

    public partial class frmMain : Form
    {
        // Setup configuration
        public static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        // Instantiate SQL connection & command
        public static SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
        public static SqlCommand cmd = new SqlCommand();
      
        // Schedule timer
        static System.Timers.Timer timer;
        static DateTime nowTime = DateTime.Now;
        static DateTime scheduledTime;

        // Declare background worker thread
        private BackgroundWorker backgroundWorker1;

        public frmMain()
        {
            InitializeComponent();

            // Start the schedule timer
            schedule_Timer();

            // Setup the background worker thread
            backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.DoWork += new DoWorkEventHandler(BackgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorker1_RunWorkerCompleted);
        }

        // Method to read in the latest state-level data file and post the results to the SQL DB
        private void GetLatestData(Boolean downloadLatestData)
        {
            try
            {
                // Setup stopwatch
                System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
                elapsed.Start();

                // These directives needed to prevent security error on HTTP request
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var client = new RestClient("http://covidtracking.com/api/v1/states/daily.json");
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                // Create list for data records
                var DailyDataList = new List<DailyStateTotals>();

                var StateDataAr = JArray.Parse(response.Content);

                Int32 rowCount = 0;
                logTxt.Clear();

                foreach (JObject a in StateDataAr)
                {
                    var StateData = JObject.Parse(a.ToString());

                    DailyStateTotals foo = new DailyStateTotals
                    {
                        date = (string)StateData.SelectToken("date"),
                        state = (string)StateData.SelectToken("state"),
                        positive = (string)StateData.SelectToken("positive"),
                        negative = (string)StateData.SelectToken("negative"),
                        deaths = (string)StateData.SelectToken("death"),
                        hospitalized = (string)StateData.SelectToken("hospitalized"),
                        total = (string)StateData.SelectToken("total"),
                        totalResults = (string)StateData.SelectToken("totalTestResults"),
                        fips = (string)StateData.SelectToken("fips"),
                        deathInc = (string)StateData.SelectToken("deathIncrease"),
                        hospInc = (string)StateData.SelectToken("hospitalizedIncrease"),
                        negativeInc = (string)StateData.SelectToken("negativeIncrease"),
                        positiveInc = (string)StateData.SelectToken("positiveIncrease"),
                        totalTestResultsInc = (string)StateData.SelectToken("totalTestResultsIncrease")
                    };

                    DailyDataList.Add(foo);
                    rowCount++;
                }

                SqlConnection conn = new SqlConnection(config.AppSettings.Settings["sqlConnString"].Value);
                conn.Open();
                SqlCommand cmd;

                foreach (DailyStateTotals b in DailyDataList)
                {
                    cmd = new SqlCommand(config.AppSettings.Settings["storedProcedure_State"].Value, conn)
                    {
                        CommandType = CommandType.StoredProcedure,
                    };

                    //ADD PARAMETER NAMES IN FIRST ARGUMENT VALUES
                    //cmd.Parameters.AddWithValue("@date", GetDateTimeString(b.date));
                    cmd.Parameters.AddWithValue("@state", b.state ?? "");
                    cmd.Parameters.AddWithValue("@positive", b.positive ?? "");
                    cmd.Parameters.AddWithValue("@negative", b.negative ?? "");
                    cmd.Parameters.AddWithValue("@deaths", b.deaths ?? "");
                    cmd.Parameters.AddWithValue("@hospitalized", b.hospitalized ?? "");
                    cmd.Parameters.AddWithValue("@total", b.total ?? "");
                    cmd.Parameters.AddWithValue("@totalResults", b.totalResults ?? "");
                    cmd.Parameters.AddWithValue("@fips", b.fips ?? "0");
                    cmd.Parameters.AddWithValue("@deathInc", b.deathInc ?? "");
                    cmd.Parameters.AddWithValue("@hospInc", b.hospInc ?? "");
                    cmd.Parameters.AddWithValue("@negativeInc", b.negativeInc ?? "");
                    cmd.Parameters.AddWithValue("@positiveInc", b.positiveInc ?? "");
                    cmd.Parameters.AddWithValue("@totalTestResultsInc", b.totalTestResultsInc ?? "");

                    // Handle conversion to get heat map color index value                    
                    //if (b.positive == null) b.positive = "0";
                    //cmd.Parameters.AddWithValue("@ColorIndex_HeatMap", GetCubeRootColorIndex(int.Parse(b.positive)));

                    cmd.ExecuteNonQuery();
                }

                conn.Close();
                // Display stats for processing
                elapsed.Stop();
                logTxt.AppendText("Last state data update processed: " + DateTime.Now.ToString() + Environment.NewLine);
                logTxt.AppendText("Data rows processed: " + rowCount.ToString() + Environment.NewLine);
                logTxt.AppendText("Total elapsed time (seconds): " + elapsed.Elapsed.TotalSeconds + Environment.NewLine);
                txtStatus.Text = "State-Level data update completed successfully at: " + DateTime.Now.ToString();

            }
            catch (Exception e)
            {
                txtStatus.Text = "Error occurred during state-level data retrieval and database posting: " + e.Message;
            }
        }

        // Handler for button to force getting latest data
        private void btnGetData_Click(object sender, EventArgs e)
        {
            // Call method with flag set to download the latest data; if false, filename is specified as 2nd parameter
            //GetLatestData_County(true, String.Empty);
        }

        // Here's the scheduling timer
        //static void schedule_Timer()
        void schedule_Timer()
        {
            Console.WriteLine("### Timer Started ###");

            nowTime = DateTime.Now;
            scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 6, 0, 0, 0); // Start at 6:00 AM

            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (double)(scheduledTime - DateTime.Now).TotalMilliseconds;
            timer = new System.Timers.Timer(tickTime);
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();
        }

        //static void timer_Elapsed(object sender, ElapsedEventArgs e)
        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Update status label
            txtStatus.Text = "Update timer fired at " + DateTime.Now.ToString();

            // Call method with flag set to download the latest data; if false, filename is specified as 2nd parameter
            //GetLatestData_County(true, String.Empty);

            GetLatestData(true);

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
    }
}
