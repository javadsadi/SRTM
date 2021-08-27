﻿// The MIT License (MIT)

// Copyright (c) 2017 Alpine Chough Software, Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SRTM
{
    /// <summary>
    /// SRTM Data.
    /// </summary>
    /// <exception cref='DirectoryNotFoundException'>
    /// Is thrown when part of a file or directory argument cannot be found.
    /// </exception>
    public class SRTMData : ISRTMData
    {
        private const int RETRIES = 3;
        private ISRTMSource _source;
        int count = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="SRTM.SRTMData"/> class.
        /// </summary>
        /// <param name='dataDirectory'>
        /// Data directory.
        /// </param>
        /// <param name="source">
        /// Data source to use. Must be an instance of the <see cref="SRTM.ISRTMSource"/> class
        /// </param>
        /// <exception cref='DirectoryNotFoundException'>
        /// Is thrown when part of a file or directory argument cannot be found.
        /// </exception>
        public SRTMData(string dataDirectory, ISRTMSource source)
        {
            if (!Directory.Exists(dataDirectory))
                throw new DirectoryNotFoundException(dataDirectory);

            _source = source;
            GetMissingCell = _source.GetMissingCell;
            DataDirectory = dataDirectory;
            DataCells = new List<ISRTMDataCell>();
        }

        /// <summary>
        /// A delegate to get missing cells.
        /// </summary>
        public delegate bool GetMissingCellDelegate(string path, string name);

        /// <summary>
        /// Gets or sets the missing cell delegate.
        /// </summary>
        public GetMissingCellDelegate GetMissingCell { get; set; }
        
        /// <summary>
        /// Gets or sets the data directory.
        /// </summary>
        /// <value>
        /// The data directory.
        /// </value>
        public string DataDirectory { get; private set; }

        /// <summary>
        /// Gets or sets the SRTM data cells.
        /// </summary>
        /// <value>
        /// The SRTM data cells.
        /// </value>
        private List<ISRTMDataCell> DataCells { get; set; }
        
        #region Public methods

        /// <summary>
        /// Unloads all SRTM data cells.
        /// </summary>
        public void Unload()
        {
            DataCells.Clear();
        }

        /// <summary>
        /// Gets the elevation.
        /// </summary>
        /// <returns>
        /// The height. Null, if elevation is not available.
        /// </returns>
        /// <param name='latitude'>
        /// Latitude in decimal degrees of desired location.
        /// </param>
        /// <param name="longitude">
        /// Longitude in decimal degrees of desired location
        /// </param>
        /// <exception cref='Exception'>
        /// Represents errors that occur during application execution.
        /// </exception>
        public int? GetElevation(double latitude, double longitude)
        {
            ISRTMDataCell dataCell = GetDataCell(latitude, longitude);
            return dataCell.GetElevation(latitude, longitude);
        }

        /// <summary>
        /// Gets the elevation. Data is smoothed using bilinear interpolation.
        /// </summary>
        /// <returns>
        /// The height. Null, if elevation is not available.
        /// </returns>
        /// <param name='latitude'>
        /// Latitude in decimal degrees of desired location.
        /// </param>
        /// <param name="longitude">
        /// Longitude in decimal degrees of desired location
        /// </param>
        /// <exception cref='Exception'>
        /// Represents errors that occur during application execution.
        /// </exception>
        public double? GetElevationBilinear(double latitude, double longitude)
        {
            ISRTMDataCell dataCell = GetDataCell(latitude, longitude);
            return dataCell.GetElevationBilinear(latitude, longitude);
        }

        #endregion
        
        #region Private methods

        /// <summary>
        /// Method responsible for identifying the correct data cell and either retrieving it from cache ir downloading it from source.
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <returns>Elevation data cell. Must be an instance of the <see cref="SRTM.ISRTMDataCell"/> class.</returns>
        private ISRTMDataCell GetDataCell(double latitude, double longitude)
        {
            int cellLatitude = (int)Math.Floor(Math.Abs(latitude));
            if (latitude < 0)
            {
                cellLatitude *= -1;
                if (cellLatitude != latitude)
                { // if exactly equal, keep the current tile.
                    cellLatitude -= 1; // because negative so in bottom tile
                }
            }

            int cellLongitude = (int)Math.Floor(Math.Abs(longitude));
            if (longitude < 0)
            {
                cellLongitude *= -1;
                if (cellLongitude != longitude)
                { // if exactly equal, keep the current tile.
                    cellLongitude -= 1; // because negative so in left tile
                }
            }

            var dataCell = DataCells.Where(dc => dc.Latitude == cellLatitude && dc.Longitude == cellLongitude).FirstOrDefault();
            if (dataCell != null)
            {
                return dataCell;
            }

            string filename = string.Format("{0}{1:D2}{2}{3:D3}",
                cellLatitude < 0 ? "S" : "N",
                Math.Abs(cellLatitude),
                cellLongitude < 0 ? "W" : "E",
                Math.Abs(cellLongitude));

            var filePath = Path.Combine(DataDirectory, filename + ".hgt");
            var zipFilePath = Path.Combine(DataDirectory, filename + ".hgt.zip");
            var txtFilePath = Path.Combine(DataDirectory, filename + ".txt");
            //count = -1;

            if (!File.Exists(filePath) && !File.Exists(zipFilePath) && /*!File.Exists(txtFilePath) &&*/ this.GetMissingCell != null)
            {
                this.GetMissingCell(DataDirectory, filename);
            }
            /*else if(File.Exists(txtFilePath) && this.GetMissingCell != null)
            {
                var txtFile = File.ReadAllText(txtFilePath);
                if (!int.TryParse(txtFile, out count))
                {
                    File.Delete(txtFilePath);
                    count = -1;
                }
                else if(count < RETRIES)
                {
                    if (this.GetMissingCell(DataDirectory, filename))
                    {
                        File.Delete(txtFilePath);
                    }
                }
            }*/
            
            if (File.Exists(filePath))
            {
                dataCell = new SRTMDataCell(filePath);
            }
            else if(File.Exists(zipFilePath))
            {
                dataCell = new SRTMDataCell(zipFilePath);
            }
            else
            {
                if (count < 0)
                {
                    File.WriteAllText(txtFilePath, "1");
                    count = 1;
                    return GetDataCell(latitude, longitude);

                }
                else if (count < RETRIES)
                {
                    count++;
                    //File.WriteAllText(txtFilePath, count.ToString());
                    return GetDataCell(latitude, longitude);
                }
                else
                {
                    dataCell = new EmptySRTMDataCell(txtFilePath);
                }
            }
            DataCells.Add(dataCell);

            return dataCell;
        }
        
        #endregion
    }
}