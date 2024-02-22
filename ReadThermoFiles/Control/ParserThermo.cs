using ReadThermoFiles.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.RawFileReader;

namespace ReadThermoFiles.Control
{
    public static class ParserThermo
    {
        public static int Progress { get; private set; }

        public static IRawDataPlus ThermoLoad(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new Exception("No RAW file specified!");
            }

            // Check to see if the specified RAW file exists
            if (!File.Exists(fileName))
            {
                throw new Exception(@"The file doesn't exist in the specified location - " + fileName);
            }

            // Create the IRawDataPlus object for accessing the RAW file
            IRawDataPlus rawFile = RawFileReaderAdapter.FileFactory(fileName);

            if (!rawFile.IsOpen || rawFile.IsError)
            {
                throw new Exception("Unable to access the RAW file using the RawFileReader class!");
            }

            // Check for any errors in the RAW file
            if (rawFile.IsError)
            {
                throw new Exception($"Error opening ({rawFile.FileError}) - {fileName}");
            }

            // Check if the RAW file is being acquired
            if (rawFile.InAcquisition)
            {
                throw new Exception("RAW file still being acquired - " + fileName);
            }

            // Get the number of instruments (controllers) present in the RAW file and set the selected instrument to the MS instrument, first instance of it
            rawFile.SelectInstrument(Device.MS, 1);

            return rawFile;
        }
        public static List<MassSpectrum> Parse(string fileName, short MsnLevel = 2, short fileIndex = -1, bool saveScanHeader = false, List<int> scanNumbers = null, bool printConsole = true, int maximumNumberOfPeaks = 900)
        {
            IRawDataPlus rawFile = ThermoLoad(fileName);
            return Parse(rawFile, MsnLevel, fileIndex, saveScanHeader, scanNumbers, printConsole, maximumNumberOfPeaks);

        }

        private static List<MassSpectrum> Parse(IRawDataPlus rawFile, short MsnLevel = 2, short fileIndex = -1, bool saveScanHeader = false, List<int> scanNumbers = null, bool printConsole = true, int maximumNumberOfPeaks = 900)
        {
            Console.WriteLine("Parsing file: " + rawFile.FileName + " :: MSLevel :: " + MsnLevel);

            List<MassSpectrum> tmpList = new List<MassSpectrum>();

            object progress_lock = new object();
            int old_progress = 0;

            try
            {

                // Get the first and last scan from the RAW file
                int iFirstScan = rawFile.RunHeaderEx.FirstSpectrum;
                int iLastScan = rawFile.RunHeaderEx.LastSpectrum;

                int iNumPeaks = -1;
                short iPrecursorCharge = -1;
                double dPrecursor = -1;
                short iMassAnalyzer = -1;
                short iActivationType = -1;
                double dPrecursorMZ = -1;
                double[] pdMass;
                double[] pdInten;
                double[] pdCharge;
                int precursorScanNumber = -1;
                //double precursorIntensity = -1;

                int count = 0;

                if (scanNumbers == null)
                {
                    scanNumbers = Enumerable.Range(iFirstScan, iLastScan).ToList();
                }

                foreach (int iScanNumber in scanNumbers)
                {
                    count++;

                    ScanStatistics scanStatistics = rawFile.GetScanStatsForScanNumber(iScanNumber);
                    string sScanHeader = scanStatistics.ScanType;
                    double dRT = rawFile.RetentionTimeFromScanNumber(iScanNumber);

                    // Get the scan filter for this scan number
                    IScanFilter scanFilter = rawFile.GetFilterForScanNumber(iScanNumber);

                    if (!string.IsNullOrEmpty(scanFilter.ToString()) && MsnLevel == -1 ||
                    (scanFilter.MSOrder == MSOrderType.Ms && MsnLevel == 1 ||
                    scanFilter.MSOrder == MSOrderType.Ms2 && MsnLevel == 2 ||
                    scanFilter.MSOrder == MSOrderType.Ms3 && MsnLevel == 3))
                    {

                        // Check to see if the scan has centroid data or profile data.  Depending upon the
                        // type of data, different methods will be used to read the data.
                        CentroidStream centroidStream = rawFile.GetCentroidStream(iScanNumber, false);

                        if (centroidStream.Length > 0)
                        {
                            // Get the centroid (label) data from the RAW file for this scan

                            iNumPeaks = centroidStream.Length;
                            pdMass = new double[iNumPeaks];   // stores mass of spectral peaks
                            pdInten = new double[iNumPeaks];  // stores inten of spectral peaks
                            pdCharge = new double[iNumPeaks];
                            pdMass = centroidStream.Masses;
                            pdInten = centroidStream.Intensities;
                            pdCharge = centroidStream.Charges;
                        }
                        else
                        {
                            // Get the segmented (low res and profile) scan data
                            SegmentedScan segmentedScan = rawFile.GetSegmentedScanFromScanNumber(iScanNumber, scanStatistics);
                            iNumPeaks = segmentedScan.Positions.Length;
                            pdMass = new double[iNumPeaks];   // stores mass of spectral peaks
                            pdInten = new double[iNumPeaks];  // stores inten of spectral peaks
                            pdMass = segmentedScan.Positions;
                            pdInten = segmentedScan.Intensities;
                            pdCharge = new double[iNumPeaks];
                        }

                        if (iNumPeaks > 0)
                        {

                            MassAnalyzerType massAnalyzer = rawFile.GetScanEventForScanNumber(iScanNumber).MassAnalyzer;

                            switch (massAnalyzer)
                            {
                                case MassAnalyzerType.MassAnalyzerFTMS:
                                    iMassAnalyzer = 1;
                                    break;
                                case MassAnalyzerType.MassAnalyzerITMS:
                                    iMassAnalyzer = 2;
                                    break;
                                case MassAnalyzerType.MassAnalyzerTOFMS:
                                    iMassAnalyzer = 3;
                                    break;
                                case MassAnalyzerType.MassAnalyzerSQMS:
                                    iMassAnalyzer = 4;
                                    break;
                                default:
                                    iMassAnalyzer = -1;
                                    break;

                            }

                            // Get the scan event for this scan number
                            if (scanFilter.MSOrder != MSOrderType.Ms)
                            {

                                IScanEvent scanEvent = rawFile.GetScanEventForScanNumber(iScanNumber);
                                dPrecursor = scanEvent.GetReaction(0).PrecursorMass;

                                ActivationType activationType = scanEvent.GetReaction(0).ActivationType;

                                switch (activationType)
                                {
                                    case ActivationType.CollisionInducedDissociation:
                                        iActivationType = 1;
                                        break;
                                    case ActivationType.HigherEnergyCollisionalDissociation:
                                        iActivationType = 2;
                                        break;
                                    case ActivationType.ElectronTransferDissociation:
                                        iActivationType = 3;
                                        break;
                                    case ActivationType.ElectronCaptureDissociation:
                                        iActivationType = 4;
                                        break;
                                    case ActivationType.MultiPhotonDissociation:
                                        iActivationType = 5;
                                        break;
                                    case ActivationType.Any:
                                        iActivationType = 6;
                                        break;
                                    case ActivationType.PQD:
                                        iActivationType = 7;
                                        break;
                                    default:
                                        iActivationType = -1;
                                        break;

                                }

                                LogEntry trailerData = rawFile.GetTrailerExtraInformation(iScanNumber);
                                for (int i = 0; i < trailerData.Length; i++)
                                {

                                    if (trailerData.Labels[i] == "Monoisotopic M/Z:")
                                        dPrecursorMZ = double.Parse(trailerData.Values[i]);
                                    else if (trailerData.Labels[i] == "Charge State:")
                                        iPrecursorCharge = (short)double.Parse(trailerData.Values[i]);
                                    else if ((trailerData.Labels[i] == "Master Scan Number:") || (trailerData.Labels[i] == "Master Scan Number") || (trailerData.Labels[i] == "Master Index:"))
                                        precursorScanNumber = Convert.ToInt32(trailerData.Values[i]);
                                }

                                dPrecursorMZ = dPrecursorMZ == 0 ? dPrecursor : dPrecursorMZ;
                            }


                            //double dPepMass = (dPrecursorMZ * iPrecursorCharge) - (iPrecursorCharge - 1) * 1.00727646688;


                            (double MZ, double Intensity, int charge)[] ions = new (double MZ, double Intensity, int charge)[pdMass.Length];

                            for (int i = 0; i < pdInten.Length; i++)
                            {
                                ions[i].MZ = pdMass[i];
                                ions[i].Intensity = pdInten[i];
                                ions[i].charge = Convert.ToInt32(pdCharge[i]);
                            }

                            ions = FilterPeaks(ions, 0.01, maximumNumberOfPeaks);

                            MassSpectrum ms = new MassSpectrum()
                            {
                                ActivationType = iActivationType,
                                CromatographyRetentionTime = dRT,
                                FileNameIndex = fileIndex,
                                InstrumentType = iMassAnalyzer,
                                Ions = ions.ToList(),
                                MSLevel = (short)scanFilter.MSOrder,
                                Precursors = new List<(double MZ, short Z)>() { (dPrecursorMZ, iPrecursorCharge) },
                                ScanNumber = iScanNumber,
                                PrecursorScanNumber = precursorScanNumber
                            };

                            if (saveScanHeader)
                                ms.ScanHeader = sScanHeader;

                            tmpList.Add(ms);

                            lock (progress_lock)
                            {
                                Progress = (int)((double)iScanNumber / (iLastScan - iFirstScan) * 100);
                                if (Progress > old_progress)
                                {
                                    old_progress = Progress;
                                    if (printConsole)
                                    {
                                        int currentLineCursor = Console.CursorTop;
                                        Console.SetCursorPosition(0, Console.CursorTop);
                                        Console.Write("Reading RAW File: " + old_progress + "%");
                                        Console.SetCursorPosition(0, currentLineCursor);

                                    }
                                    else
                                    {
                                        Console.Write("Reading RAW File: " + old_progress + "%");
                                    }
                                }

                            }

                        }
                    }

                }

                rawFile.Dispose();
            }
            catch (Exception rawSearchEx)
            {
                Console.WriteLine(" Error: " + rawSearchEx.Message);
            }


            Console.WriteLine("Reading RAW File: " + "100" + "%");

            return tmpList;
        }

        public static (double MZ, double Intensity, int charge)[] FilterPeaks((double MZ, double Intensity, int charge)[] ions, double relativeThresholdPercent = 0.01, int maximumNumberOfPeaks = 900)
        {
            double relative_threshold = ions.Max(a => a.Intensity) * (relativeThresholdPercent / 100.0);
            ions = ions.OrderByDescending(a => a.Intensity).Take(maximumNumberOfPeaks).Where(a => a.Intensity > relative_threshold).OrderBy(a => a.MZ).ToArray();
            return ions;
        }
    }
}
