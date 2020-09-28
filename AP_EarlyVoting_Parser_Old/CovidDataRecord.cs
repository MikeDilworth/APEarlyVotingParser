using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileHelpers;


namespace COVID_CSV_Parser
{
    // Set options for FileHelpers class
    [DelimitedRecord(",")]
    [IgnoreEmptyLines()]
    [IgnoreFirst()]

    // Define data classes
    public class CovidDataRecord
    {
        // FIPS code
        public Int32? FIPS { get; set; }
        //County Name
        public string Admin2 { get; set; }
        //State/Province
        [FieldQuoted('"', QuoteMode.OptionalForBoth)]
        public string Province_State { get; set; }
        //Country/Region
        [FieldQuoted('"', QuoteMode.OptionalForBoth)]
        public string Country_Region { get; set; }
        //Last Update timestamp
        //[FieldConverter(ConverterKind.Date, "d/M/yyyy H:m:ss")]
        //public DateTime? Last_Update { get; set; }
        public string Last_Update { get; set; }
        //Latitude
        public float? Lat { get; set; }
        //Longitude
        public float? Long_ { get; set; }
        // Confirmed
        public Int32? Confirmed { get; set; }
        // Deaths
        public Int32? Deaths { get; set; }
        // Recovered
        public Int32? Recovered { get; set; }
        // Active
        public Int32? Active { get; set; }
        //Combined key
        [FieldQuoted('"', QuoteMode.OptionalForBoth)]
        public string Combined_Key { get; set; }
    }
}
