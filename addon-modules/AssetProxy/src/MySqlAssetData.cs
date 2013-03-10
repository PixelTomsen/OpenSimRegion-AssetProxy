/*
 * Pixel Tomsen 2012 (pixel.tomsen [at] gridnet.info)
 *
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Reflection;
using OpenSim.Framework;

using OpenMetaverse;
using log4net;
using MySql.Data.MySqlClient;

using Migration = OpenSim.Data.Migration;

namespace OpenSim.Region.AssetProxy
{
    public class MySqlAssetData : IAssetProxyData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString = String.Empty;
        private object m_dbLock = new object();

        private int m_logLevel = 0;

        AssetProxyDataStats m_stats = new AssetProxyDataStats();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlAssetData(string connectionString, int LogLevel)
        {
            Initialise(connectionString, LogLevel);
        }

        public void Initialise(string connectionString, int LogLevel)
        {
            m_connectionString = connectionString;

            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    Migration m = new Migration(dbcon, Assembly, "mysql");
                    m.Update();
                }
            }
            catch (Exception ex)
            {
                m_stats.Errors++;

                m_log.ErrorFormat("[{0}]: MySql port is unable to migrate table, exiting! ->:{1}", "AssetProxy", ex.ToString());

                Environment.Exit(-1);
            }

            m_stats.TimeStamp = Utils.GetUnixTime();
        }

        public AssetBase GetAsset(string Id, bool IsTemporary)
        {
            AssetBase asset = null;

            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand("SELECT data FROM " + Table(IsTemporary) + " WHERE AssetID=?assetid", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?assetid", Id);

                        try
                        {
                            using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (dbReader.Read())
                                {
                                    BinaryFormatter bformatter = new BinaryFormatter();
                                    using (MemoryStream ms = new MemoryStream((byte[])dbReader["data"]))
                                    {
                                        asset = (AssetBase)bformatter.Deserialize(ms);
                                        if (IsTemporary) m_stats.GetsTmp++;
                                        else
                                            m_stats.Gets++;
                                    }
                                }
                            }
                        }
                        catch (SerializationException se)
                        {
                            if (m_logLevel >= 1)
                            {
                                m_log.ErrorFormat("[{0}]: MySql failure getting asset {1}{2}",
                                    "AssetProxy", Id, Environment.NewLine + se.ToString());

                                DeleteAsset(Id, IsTemporary);
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[{0}]: MySql failure fetching asset {1}{2}",
                                "AssetProxy", Id, Environment.NewLine + e.ToString());

                            m_stats.Errors++;
                            return asset;
                        }
                    }

                    using (MySqlCommand cmd
                        = new MySqlCommand("update " + Table(IsTemporary) + " set access_time=?access_time where AssetID=?id", dbcon))
                    {
                        try
                        {
                            using (cmd)
                            {
                                cmd.Parameters.AddWithValue("?id", Id);
                                cmd.Parameters.AddWithValue("?access_time", Utils.DateTimeToUnixTime(DateTime.UtcNow));
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[{0}]: MySql failure updating access_time for asset {1}{2}",
                                "AssetProxy", Id, Environment.NewLine + e.ToString());

                            m_stats.Errors++;
                        }
                    }

                    dbcon.Close();
                }
            }

            return asset;
        }

        public void StoreAsset(AssetBase asset, bool IsTemporary)
        {
            if (asset == null)
                return;

            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd =
                        new MySqlCommand(
                            "replace INTO " + Table(IsTemporary) + "(AssetID, create_time, access_time, data)" +
                            "VALUES(?assetid, ?create_time, ?access_time, ?data)",
                            dbcon))
                    {
                        try
                        {
                            using (cmd)
                            {
                                int now = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow);

                                BinaryFormatter bformatter = new BinaryFormatter();
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    bformatter.Serialize(ms, asset);

                                    cmd.Parameters.AddWithValue("?assetid", asset.ID);
                                    cmd.Parameters.AddWithValue("?create_time", now);
                                    cmd.Parameters.AddWithValue("?access_time", now);
                                    cmd.Parameters.AddWithValue("?data", ms.ToArray());
                                    cmd.ExecuteNonQuery();

                                    if (IsTemporary)
                                        m_stats.StoresTmp++;
                                    else
                                        m_stats.Stores++;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[{0}]: MySQL failure creating asset {1}. Error: {2}",
                                "Assetproxy", asset.ID, e.ToString());

                            m_stats.Errors++;
                        }
                    }

                    dbcon.Close();
                }
            }
        }

        public void DeleteAsset(string Id, bool IsTemporary)
        {
            lock (m_dbLock)
            {
                try
                {
                    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                    {
                        dbcon.Open();

                        using (MySqlCommand cmd = new MySqlCommand("delete from " + Table(IsTemporary) + " where AssetID=?id", dbcon))
                        {
                            cmd.Parameters.AddWithValue("?id", Id);
                            cmd.ExecuteNonQuery();

                            if (IsTemporary)
                                m_stats.DeletesTmp++;
                            else
                                m_stats.Deletes++;
                        }

                        dbcon.Close();
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}]: MySQL failure delete asset {1}. Error: {2}",
                        "AssetProxy", Id, e.ToString());

                    m_stats.Errors++;
                }
            }
        }

        public bool ExistsAsset(string Id, bool IsTemporary)
        {
            bool exists = false;

            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand("SELECT AssetID FROM " + Table(IsTemporary) + " WHERE AssetID=?assetid", dbcon))
                    {
                        cmd.Parameters.AddWithValue("?assetid", Id);

                        try
                        {
                            using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                            {
                                if (dbReader.Read())
                                {
                                    exists = true;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[{0}]: MySql failure fetching asset {1}" + Environment.NewLine + e.ToString(), "AssetProxy", Id);

                            m_stats.Errors++;
                        }
                    }

                    dbcon.Close();
                }
            }

            return exists;
        }


        private int AssetCount(bool countTemporary)
        {
            int rows = 0;

            lock (m_dbLock)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM " + Table(countTemporary), dbcon))
                    {
                        try
                        {
                            rows = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[{0}]: MySql failure counting asset" + Environment.NewLine + e.ToString(), "AssetProxy");

                            m_stats.Errors++;
                        }
                    }

                    dbcon.Close();
                }
            }

            return rows;
        }

        public AssetProxyDataStats Statistics
        {
            get
            {
                m_stats.Assets = this.AssetCount(false);
                m_stats.AssetsTemp = this.AssetCount(true);
                return m_stats;
            }
        }

        public void ResetStatus()
        {
            lock (m_stats)
            {
                m_stats.Deletes = 0;
                m_stats.DeletesTmp = 0;
                m_stats.Errors = 0;
                m_stats.Gets = 0;
                m_stats.GetsTmp = 0;
                m_stats.Stores = 0;
                m_stats.StoresTmp = 0;
                m_stats.TimeStamp = Utils.GetUnixTime();
            }
        }

        private string Table(bool IsTemp)
        {
            if (!IsTemp)
                return "assetcache";
            return "tmpcache";
        }

    }
}
