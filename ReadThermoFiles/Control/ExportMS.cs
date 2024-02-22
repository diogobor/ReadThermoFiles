using ReadThermoFiles.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThermoFisher.CommonCore.Data.Business;

namespace ReadThermoFiles.Control
{
    public static class ExportMS
    {
        /// <summary>
        /// Converter RAW file to MS2 file
        /// </summary>
        /// <param name="tmsList"></param>
        public static void Converter2MSn(
            List<MassSpectrum> tmsList,
            string fileName,
            int msnLevel = 2,
            bool keep_original_name = false)
        {
            if (tmsList == null || tmsList.Count == 0)
            {
                return;
            }
            string newName = "";
            if (!keep_original_name)
                newName = fileName.Substring(0, fileName.ToString().Length - 4) + ".ms" + msnLevel;
            else
                newName = fileName;
            StreamWriter sw = new StreamWriter(newName);
            converterMSn(tmsList, sw, msnLevel);
        }

        /// <summary>
        /// Converter to MS2 file
        /// </summary>
        /// <param name="tmsList"></param>
        private static void converterMSn(List<MassSpectrum> tmsList, StreamWriter sw, int msnLevel = 2)
        {
            if (tmsList == null || tmsList.Count == 0)
            {
                return;
            }

            int spectra_processed = 0;
            int old_progress = 0;
            double lengthFile = tmsList.Count;
            Console.WriteLine(" Writing MS" + msnLevel + " File.");
            int firstScan = tmsList[0].ScanNumber;
            int lastScan = tmsList[tmsList.Count() - 1].ScanNumber;
            ///<summary>
            ///Get Program Version
            ///</summary>
            string version = "1.0";

            sw.Write("H\tCreation Date\t" + DateTime.Now.ToString() + "\n"
                + "H\tExtractor\tThermo Reader\n"
                + "H\tFirstScan\t" + firstScan + "\n"
                + "H\tLastScan\t" + lastScan + "\n"
                + "H\tVersion\t" + version + "\n"
                );
            try
            {
                foreach (MassSpectrum tms in tmsList)
                {
                    if (msnLevel > 1)
                    {
                        
                        sw.Write("S\t" + String.Format("{0:000000}", tms.ScanNumber) + "\t" + String.Format("{0:000000}", tms.ScanNumber) + "\t" + tms.Precursors[0].MZ + "\n" +
                            "I\tRetTime\t" + tms.CromatographyRetentionTime + "\n" +
                            "I\tPrecursorScan\t" + tms.PrecursorScanNumber + "\n"
                            );
                        foreach (var precursor in tms.Precursors)
                        {
                            double mh = (precursor.MZ * precursor.Z) - (precursor.Z - 1) * 1.00727646688;
                            sw.WriteLine("Z\t" + precursor.Z + "\t" + mh);
                        }
                    }
                    else
                    {
                        sw.Write("S\t" + String.Format("{0:000000}", tms.ScanNumber) + "\t" + String.Format("{0:000000}", tms.ScanNumber) + "\n" +
                            "I\tRetTime\t" + tms.CromatographyRetentionTime + "\n"
                            );
                    }
                    foreach (var ion in tms.Ions)
                    {
                        sw.WriteLine(ion.MZ + "\t" + ion.Intensity + "\t" + ion.Charge);
                    }
                    spectra_processed++;
                    int new_progress = (int)((double)spectra_processed / (lengthFile) * 100);
                    if (new_progress > old_progress)
                    {
                        old_progress = new_progress;
                        int currentLineCursor = Console.CursorTop;
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write(" Writing MS" + msnLevel + " File: " + old_progress + "%");
                        Console.SetCursorPosition(0, currentLineCursor);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
            Console.WriteLine(" Done.");
            sw.Close();
        }
    }
}
