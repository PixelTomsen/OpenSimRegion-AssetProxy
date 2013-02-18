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
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.AssetProxy
{
    public interface IAssetProxyData
    {
        void Initialise(string ConnectionString, int LogLevel);

        AssetBase GetAsset(string Id, bool IsTemporary);

        void StoreAsset(AssetBase Asset, bool IsTemporary);

        void DeleteAsset(string Id, bool IsTemporary);

        bool ExistsAsset(string Id, bool IsTemporary);

        AssetProxyDataStats Statistics { get; }

        void ResetStatus();
    }

    public class AssetProxyDataStats
    {
        public uint TimeStamp = 0;

        public ulong Gets = 0;
        public ulong GetsTmp = 0;

        public ulong Stores = 0;
        public ulong StoresTmp = 0;

        public ulong Deletes = 0;
        public ulong DeletesTmp = 0;

        public ulong Errors = 0;

        public int Assets = 0;
        public int AssetsTemp = 0;

        public ulong AssetsSize = 0;
    }
}

