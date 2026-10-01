// COPYRIGHT 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Orts.Formats.Msts;
using System.IO;

namespace Orts.Formats.OR
{
    public class MSTSData
    {
        public TrackDatabaseFile TDB { get; protected set; }
        public RouteFile TRK { get; protected set; }
        public TrackSectionsFile TSectionDat { get; protected set; }
        public SignalConfigurationFile SIGCFG { get; protected set; }
        public string RoutePath { get; set; }
        public string MstsPath { get; set; }
        public AESignals Signals { get; protected set; }

        public MSTSData (string mstsPath, string Route)
        {
            MstsPath = mstsPath;
            RoutePath = Route;
            TRK = new RouteFile(MSTS.MSTSPath.GetTRKFileName(RoutePath));
            string routePath = Path.Combine(Route, TRK.Tr_RouteFile.FileName);
            TDB = new TrackDatabaseFile(RoutePath + "/" + TRK.Tr_RouteFile.FileName + ".tdb");

            string ORfilepath = System.IO.Path.Combine(RoutePath, "OPENRAILS");

            if (File.Exists(ORfilepath + "/sigcfg.dat"))
            {
                SIGCFG = new SignalConfigurationFile(ORfilepath + "/sigcfg.dat", true);
            }
            else
            {
                SIGCFG = new SignalConfigurationFile(RoutePath + "/sigcfg.dat", false);
            }

            if (Directory.Exists(RoutePath + "/OPENRAILS") && File.Exists(RoutePath + "/OPENRAILS/tsection.dat"))
                TSectionDat = new TrackSectionsFile(RoutePath + "/OPENRAILS/tsection.dat");
            else if (Directory.Exists(RoutePath + "/GLOBAL") && File.Exists(RoutePath + "/GLOBAL/tsection.dat"))
                TSectionDat = new TrackSectionsFile(RoutePath + "/GLOBAL/tsection.dat");
            else
                TSectionDat = new TrackSectionsFile(MstsPath + "/GLOBAL/tsection.dat");
            if (File.Exists(RoutePath + "/tsection.dat"))
                TSectionDat.AddRouteTSectionDatFile(RoutePath + "/tsection.dat");
            Signals = new AESignals (this, SIGCFG);
        }
    }
}