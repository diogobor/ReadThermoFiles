using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReadThermoFiles.Model
{
    public class MassSpectrum
    {
        public double CromatographyRetentionTime { get; set; }
        public int ScanNumber { get; set; }
        public int PrecursorScanNumber { get; set; }
        public string ScanHeader { get; set; }

        /// <summary>
        /// -1 for NA, 1 for FTMS, 2 for ITMS, 3 for TOF, 4 for quadrupole
        /// </summary>
        public short InstrumentType { get; set; }

        /// <summary>
        /// 1 for CID, 2 for HCD, 3 for ETD, 4 for ECD, 5 for MPD, 6 for Not Found, 7 for PQD
        /// </summary>
        public short ActivationType { get; set; }

        public short MSLevel { get; set; }

        public List<(double MZ, double Intensity, int Charge)> Ions { get; set; }

        public static string GetInstrumentType(short theType)
        {
            switch (theType)
            {
                case 1:
                    return ("FTMS");
                case 2:
                    return ("ITMS");
                case 3:
                    return ("TOF");
                case 4:
                    return ("Quadrupole");
                default:
                    throw new Exception("Unknown instrument type.");
            }
        }

        public static string GetActivationType(short theType)
        {
            switch (theType)
            {
                case 1:
                    return ("CID");
                case 2:
                    return ("HCD");
                case 3:
                    return ("ETD");
                case 4:
                    return ("ECD");
                case 5:
                    return ("MPD");
                case 6:
                    return ("Not found");
                case 7:
                    return ("PQD");
                default:
                    throw new Exception("Unknown activation type.");
            }
        }

        /// <summary>
        /// An index to represent the file where this spectrum was extracted from.
        /// </summary>
        public short FileNameIndex { get; set; }


        /// <summary>
        /// MZ, Z; a Z of 0 means it is unknown
        /// </summary>
        public List<(double MZ, short Z)> Precursors { get; set; }


        public MassSpectrum(double chromatograpgyRetentionTime,
                            int scanNumber,
                            List<(double, double, int)> ions,
                            List<(double, short)> precursors,
                            double precursorIntensity,
                            short instrumentTye,
                            short mslevel,
                            short fileNameIndex = -1)
        {
            this.CromatographyRetentionTime = chromatograpgyRetentionTime;
            this.ScanNumber = scanNumber;
            this.Ions = ions;
            Precursors = precursors;
            this.FileNameIndex = fileNameIndex;

            MSLevel = mslevel;
            InstrumentType = instrumentTye;
            ActivationType = -1;

        }

        /// <summary>
        /// Normalize intensities to unit vector so that square norm equals one.
        /// </summary>
        public void NormalizeIntensities()
        {
            double denominator = Math.Sqrt(Ions.Sum(a => Math.Pow(a.Intensity, 2)));
            Ions = Ions.Select(a => (a.MZ, a.Intensity / denominator, a.Charge)).ToList();
        }

        public MassSpectrum()
        {

        }

        public List<string> GetZLines()
        {
            if (MSLevel > 1)
            {
                StringBuilder zLinesSb = new StringBuilder();

                foreach (var p in Precursors)
                {
                    zLinesSb.Append("Z\t" + p.Item2 + "\t" + DechargeMSPeakToPlus1(p.Item1, p.Item2).ToString());
                }

                List<string> zLines = new List<string>();
                zLines = Regex.Split(zLinesSb.ToString(), "\r\n").ToList();
                zLines.RemoveAll(a => String.IsNullOrEmpty(a));
                return zLines;

            }
            else
            {
                return null;
            }
        }

        private static double DechargeMSPeakToPlus1(double mh, double charge)
        {
            return ((mh * charge) - (((charge - 1) * 1.007276466)));
        }


        /// <summary>
        /// This method will analyze the spectrum in bins of windowSize and for each window will keep the ionsInWindow most intense ions
        /// </summary>
        /// <param name="windowSize"></param>
        /// <param name="ionsInWindow"></param>
        /// <returns></returns>
        public List<(double, double, int)> GetIonsCleaned(float windowSize, int ionsInWindow)
        {

            List<(double, double, int)> cleanedMS = new();

            for (float window = 0; window <= Ions.Max(a => a.Item1); window += windowSize)
            {
                List<(double, double, int)> ions = Ions.FindAll(a => a.Item1 > window && a.Item1 <= window + windowSize);
                ions.Sort((a, b) => b.Item2.CompareTo(a.Item2));

                cleanedMS.AddRange(ions.Take(ionsInWindow).ToList());

            }

            cleanedMS.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return cleanedMS;
        }
    }
}
