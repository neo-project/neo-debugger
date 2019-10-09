using Neo.VM;
using Neo.VM.Types;
using NeoDebug.Adapter.ModelAdapters;
using NeoFx;
using NeoFx.Models;
using NeoFx.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoDebug.Adapter
{
    internal partial class InteropService
    {
        public void RegisterAsset(Action<string, Func<ExecutionEngine, bool>, int> register)
        {
            register("Neo.Asset.Create", Asset_Create, 0);
            register("Neo.Asset.Renew", Asset_Renew, 0);
            register("Neo.Asset.GetAssetId", Asset_GetAssetId, 1);
            register("Neo.Asset.GetAssetType", Asset_GetAssetType, 1);
            register("Neo.Asset.GetAmount", Asset_GetAmount, 1);
            register("Neo.Asset.GetAvailable", Asset_GetAvailable, 1);
            register("Neo.Asset.GetPrecision", Asset_GetPrecision, 1);
            register("Neo.Asset.GetOwner", Asset_GetOwner, 1);
            register("Neo.Asset.GetAdmin", Asset_GetAdmin, 1);
            register("Neo.Asset.GetIssuer", Asset_GetIssuer, 1);

            register("AntShares.Asset.Create", Asset_Create, 0);
            register("AntShares.Asset.Renew", Asset_Renew, 0);
            register("AntShares.Asset.GetAssetId", Asset_GetAssetId, 1);
            register("AntShares.Asset.GetAssetType", Asset_GetAssetType, 1);
            register("AntShares.Asset.GetAmount", Asset_GetAmount, 1);
            register("AntShares.Asset.GetAvailable", Asset_GetAvailable, 1);
            register("AntShares.Asset.GetPrecision", Asset_GetPrecision, 1);
            register("AntShares.Asset.GetOwner", Asset_GetOwner, 1);
            register("AntShares.Asset.GetAdmin", Asset_GetAdmin, 1);
            register("AntShares.Asset.GetIssuer", Asset_GetIssuer, 1);
        }

        private bool Asset_Create(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_Create));
        }

        private bool Asset_Renew(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_Renew));
        }

        private bool Asset_GetAssetId(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetAssetId));
        }

        private bool Asset_GetAssetType(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetAssetType));
        }

        private bool Asset_GetAmount(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetAmount));
        }

        private bool Asset_GetAvailable(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetAvailable));
        }

        private bool Asset_GetPrecision(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetPrecision));
        }

        private bool Asset_GetOwner(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetOwner));
        }

        private bool Asset_GetAdmin(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetAdmin));
        }

        private bool Asset_GetIssuer(ExecutionEngine arg)
        {
            throw new NotImplementedException(nameof(Asset_GetIssuer));
        }
    }
}
