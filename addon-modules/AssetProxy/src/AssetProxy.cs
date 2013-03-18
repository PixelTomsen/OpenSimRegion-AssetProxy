/*
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
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Reflection;


using log4net;
using Nini.Config;
using Mono.Addins;
using MySql.Data.MySqlClient;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

[assembly: Addin("AssetProxy", "1.0")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace OpenSim.Region.AssetProxy
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AssetProxy")]
    public class AssetProxy : ISharedRegionModule, IImprovedAssetCache, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;
        private int m_LogLevel = 0;

        private HashSet<string> m_CurrentlyWriting = new HashSet<string>();

        private IAssetService m_AssetService;
        private List<Scene> m_Scenes = new List<Scene>();

        private String m_connectionString = String.Empty;
        private IAssetProxyData m_dataStore = null;

        #region // ISharedRegionModule
        public AssetProxy()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "AssetProxy"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig cfg = source.Configs["Modules"];

            if (cfg == null)
                return;

            string name = cfg.GetString("AssetCaching", String.Empty);

            if (name == Name)
            {
                IConfig assetConfig = source.Configs["AssetCache"];

                if (assetConfig == null)
                {
                    m_log.ErrorFormat("[{0}]: Empty config ! exiting.");
                    Environment.Exit(-1);
                }

                m_connectionString = assetConfig.GetString("ConnectionString", String.Empty);
                m_LogLevel = assetConfig.GetInt("LogLevel", m_LogLevel);

                if (String.IsNullOrEmpty(m_connectionString))
                {
                    m_log.ErrorFormat("[{0}]: ConnectionString is missing from config (config-include/AssetProxy.ini, exiting!");
                    Environment.Exit(-1);
                }

                m_dataStore = new MySqlAssetData(m_connectionString, m_LogLevel);

                m_Enabled = true;
                m_log.InfoFormat("[{0}]: is enabled", Name);
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                MainConsole.Instance.Commands.AddCommand(
                    "Assets", true, "assetproxy status", "assetproxy status", "Display assetproxy status", HandleConsoleCommand);

                MainConsole.Instance.Commands.AddCommand(
                    "Assets", true, "assetproxy reset", "assetproxy reset", "Resets assetproxy status", HandleConsoleCommand);

                scene.RegisterModuleInterface<IImprovedAssetCache>(this);
                m_Scenes.Add(scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IImprovedAssetCache>(this);
                m_Scenes.Remove(scene);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled && m_AssetService == null)
                m_AssetService = scene.RequestModuleInterface<IAssetService>();
        }

        #endregion

        private void UpdateAsset(AssetBase asset)
        {
            if (asset == null)
                return;

            try
            {
                if (!m_dataStore.ExistsAsset(asset.ID, IsTemporary(asset)))
                {
                    lock (m_CurrentlyWriting)
                    {
                        if (m_CurrentlyWriting.Contains(asset.ID))
                        {
                            return;
                        }
                        else
                        {
                            m_CurrentlyWriting.Add(asset.ID);
                        }
                    }

                    Util.FireAndForget(
                        delegate { WriteAsset(asset); });
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: Failed to update asset {1}.  Exception {2} {3}",
                    Name, asset.ID, e.Message, e.StackTrace);
            }
        }

        #region // IImprovedAssetCache

        public void Cache(AssetBase asset)
        {
            if (asset != null)
            {
                if (m_LogLevel >= 1)
                {
                    m_log.DebugFormat("[{0}]: Store asset with id {1}", Name, asset.ID);
                }

                UpdateAsset(asset);
            }
        }

        public AssetBase Get(AssetBase Asset)
        {
            AssetBase asset = null;

            if (asset == null)
            {
                asset = m_dataStore.GetAsset(Asset.ID, IsTemporary(Asset));
            }

            if (m_LogLevel >= 1)
            {
                m_log.InfoFormat("[{0}]: Cache Get :: {1} :: {2}", Name, Asset.ID, asset == null ? "Miss" : "Hit");
            }

            return asset;
        }

        public AssetBase Get(string Id)
        {
            AssetBase asset = null;

            if (asset == null)
            {
                asset = m_dataStore.GetAsset(Id, false);

                if (asset == null)
                    asset = m_dataStore.GetAsset(Id, true);
            }

            if (m_LogLevel >= 1)
            {
                m_log.InfoFormat("[{0}]: Cache Get :: {1} :: {2}", Name, Id, asset == null ? "Miss" : "Hit");
            }

            return asset;
        }

        public void Expire(string id)
        {
            m_dataStore.DeleteAsset(id, true);
        }

        public void Clear()
        {
        }

        #endregion

        #region // IAssetService

        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset = Get(id);
            return asset.Metadata;
        }

        public byte[] GetData(string id)
        {
            AssetBase asset = Get(id);
            return asset.Data;
        }

        public bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset = Get(id);
            handler(id, sender, asset);
            return true;
        }

        public string Store(AssetBase asset)
        {
            if (asset.FullID == UUID.Zero)
            {
                asset.FullID = UUID.Random();
            }

            Cache(asset);

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = Get(id);
            asset.Data = data;
            Cache(asset);
            return true;
        }

        public bool Delete(string Id)
        {
            m_dataStore.DeleteAsset(Id, IsTemporary(Id));
            return true;
        }

        #endregion

        public AssetBase GetCached(string id)
        {
            return Get(id);
        }

        private void WriteAsset(AssetBase asset)
        {
            if (asset == null)
                return;

            try
            {
                m_dataStore.StoreAsset(asset, IsTemporary(asset));
            }
            finally
            {
                lock (m_CurrentlyWriting)
                {

                    m_CurrentlyWriting.Remove(asset.ID);
                }
            }
        }

        private bool IsTemporary(AssetBase asset)
        {
            if (asset.ID.Contains("j2kCache") || asset.Metadata.Temporary)
                return true;

            return false;
        }

        private bool IsTemporary(String id)
        {
            if (id.Contains("j2kCache"))
                return true;

            return false;
        }

        #region // Console handler

        private void HandleConsoleCommand(string module, string[] cmdparams)
        {
            if (cmdparams.Length >= 2)
            {
                string cmd = cmdparams[1];
                switch (cmd)
                {
                    case "status": RenderProxyStatus();
                        break;
                    case "reset": m_dataStore.ResetStatus();
                        break;
                    default:
                        m_log.InfoFormat("[{0}]: Unknown command {1}", Name, cmd);
                        break;
                }
            }
            else if (cmdparams.Length == 1)
            {
                m_log.InfoFormat("[{0}]: assetproxy status - Display asset proxy status", Name);
                m_log.InfoFormat("[{0}]: assetproxy reset - Resets asset proxy status", Name);
            }
        }

        private void RenderProxyStatus()
        {
            AssetProxyDataStats s = m_dataStore.Statistics;

            m_log.InfoFormat("[{0}]:Statistics since {2} with {1} Errors.",
                Name, s.Errors, Utils.UnixTimeToDateTime(s.TimeStamp));

            m_log.InfoFormat("[{0}]:=> Asset hits: {1}, saved: {2}, deleted: {3}, count: {4}",
                 Name, s.Gets, s.Stores, s.Deletes, s.Assets);

            m_log.InfoFormat("[{0}]:=> Temp asset hits: {1}, saved: {2}, deleted: {3}, count: {4}",
                Name, s.GetsTmp, s.StoresTmp, s.DeletesTmp, s.AssetsTemp);
        }

        #endregion
    }

}
