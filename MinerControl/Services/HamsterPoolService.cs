﻿using System;
using System.Collections.Generic;
using System.Linq;
using MinerControl.PriceEntries;
using MinerControl.Utility;

namespace MinerControl.Services
{
    public class HamsterPoolService : ServiceBase<HamsterPoolPriceEntry>
    {
        // https://hamsterpool.com/index.php?page=api&action=algorithm_btc_per_mh
        // {"scrypt":"0.00015374509140941","nscrypt":"0.0012163696966358","sha256":"1.2994452372168E-8","x11":"0.00012615553588975","qubit":"0.00034249512834947","x13":"0.00019299647643157","neoscrypt":"0.0080657456143128"}

        // https://hamsterpool.com/index.php?page=api&action=user_algorithm_balance_btc&api_key=[apikey]
        // {"scrypt":0,"nscrypt":0,"x11":8.074500888863218e-8,"x13":0,"sha256":0,"qubit":0,"neoscrypt":0}

        private string _apikey;
        private decimal _donation;

        public HamsterPoolService()
        {
            ServiceEnum = ServiceEnum.HamsterPool;
            //DonationAccount = "MinerControl";  //Too hard to configure donation for this one.
            //DonationWorker = "1";

            AlgoTranslations = new Dictionary<string, string>
            {
                {"nscrypt", "scryptn"}
            };
        }

        public override void Initialize(IDictionary<string, object> data)
        {
            ExtractCommon(data);
            _apikey = data.GetString("apikey");
            _donation = data["donation"].ExtractDecimal()/100;

            object[] items = data["algos"] as object[];
            foreach (object rawitem in items)
            {
                Dictionary<string, object> item = rawitem as Dictionary<string, object>;
                HamsterPoolPriceEntry entry = CreateEntry(item);
                entry.Donation = _donation;

                Add(entry);
            }
        }

        public override void CheckPrices()
        {
            ClearStalePrices();
            WebUtil.DownloadJson(
                string.Format("https://hamsterpool.com/index.php?page=api&action=algorithm_btc_per_mh", _apikey),
                ProcessPrices);
            WebUtil.DownloadJson(
                string.Format(
                    "https://hamsterpool.com/index.php?page=api&action=user_algorithm_balance_btc&api_key={0}", _apikey),
                ProcessBalances);
            UpdateHistory();
        }

        private void ProcessPrices(object jsonData)
        {
            Dictionary<string, object> data = jsonData as Dictionary<string, object>;
            lock (MiningEngine)
            {
                foreach (string key in data.Keys)
                {
                    string algo = key.ToLower();
                    decimal price = data[key].ExtractDecimal();

                    HamsterPoolPriceEntry entry = GetEntry(algo);
                    if (entry == null) continue;

                    entry.Price = price*1000;
                }

                MiningEngine.PricesUpdated = true;
                MiningEngine.HasPrices = true;

                LastUpdated = DateTime.Now;
            }
        }

        private void ProcessBalances(object jsonData)
        {
            Dictionary<string, object> data = jsonData as Dictionary<string, object>;
            lock (MiningEngine)
            {
                foreach (string key in data.Keys)
                {
                    string algo = key.ToLower();
                    decimal balance = data[key].ExtractDecimal();

                    HamsterPoolPriceEntry entry = GetEntry(algo);
                    if (entry == null) continue;

                    entry.Balance = balance;
                }

                Balance = PriceEntries.Sum(o => o.Balance);

                MiningEngine.PricesUpdated = true;
                MiningEngine.HasPrices = true;

                LastUpdated = DateTime.Now;
            }
        }
    }
}