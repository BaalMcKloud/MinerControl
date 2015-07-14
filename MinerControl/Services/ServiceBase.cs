﻿using System;
using System.Collections.Generic;
using System.Linq;
using MinerControl.History;
using MinerControl.PriceEntries;
using MinerControl.Utility;

namespace MinerControl.Services
{
    public abstract class ServiceBase<TEntry> : PropertyChangedBase, IService
        where TEntry : PriceEntryBase, new()
    {
        protected string _account;
        private decimal _balance;
        private DateTime? _lastUpdated;
        protected string _param1;
        protected string _param2;
        protected string _param3;
        private string _serviceName;
        protected decimal _weight = 1.0m;
        protected decimal _minProfit = 1.0m;
        protected string _worker;

        public ServiceBase()
        {
            DonationAccount = string.Empty;
            DonationWorker = string.Empty;
        }

        protected IList<TEntry> PriceEntries
        {
            get
            {
                return
                    MiningEngine.PriceEntries.Where(o => o.ServiceEntry.ServiceName == ServiceName)
                        .Select(o => (TEntry) o)
                        .ToList();
            }
        }

        protected ServiceHistory ServiceHistory
        {
            get { return MiningEngine.PriceHistories.SingleOrDefault(o => o.Service == ServiceName); }
        } 

        protected string DonationAccount { get; set; }
        protected string DonationWorker { get; set; }
        protected IDictionary<string, string> AlgoTranslations { get; set; }

        public MiningEngine MiningEngine { get; set; }

        public string ServiceName
        {
            get { return _serviceName; }
            protected set { SetField(ref _serviceName, value, () => ServiceName, () => ServicePrint); }
        }

        public DateTime? LastUpdated
        {
            get { return _lastUpdated; }
            protected set { SetField(ref _lastUpdated, value, () => LastUpdated, () => LastUpdatedPrint); }
        }

        public decimal Balance
        {
            get { return _balance; }
            protected set { SetField(ref _balance, value, () => Balance, () => BalancePrint, () => CurrencyPrint); }
        }

        public decimal Currency
        {
            get { return Balance*MiningEngine.Exchange; }
        }

        public virtual string ServicePrint
        {
            get { return ServiceName; }
        }

        public string LastUpdatedPrint
        {
            get { return LastUpdated == null ? string.Empty : LastUpdated.Value.ToString("HH:mm:ss"); }
        }

        public string BalancePrint
        {
            get { return Balance == 0.0m ? string.Empty : Balance.ToString("N8"); }
        }

        public string CurrencyPrint
        {
            get { return Currency == 0.0m ? string.Empty : Currency.ToString("N4"); }
        }

        public string TimeMiningPrint
        {
            get
            {
                double seconds = PriceEntries.Sum(o => o.TimeMiningWithCurrent.TotalSeconds);
                return TimeSpan.FromSeconds(seconds).FormatTime(true);
            }
        }

        public void UpdateTime()
        {
            OnPropertyChanged(() => TimeMiningPrint);
        }

        public void UpdateExchange()
        {
            OnPropertyChanged(() => CurrencyPrint);
        }

        public abstract void Initialize(IDictionary<string, object> data);
        public abstract void CheckPrices();

        protected void ExtractCommon(IDictionary<string, object> data)
        {
            _account = data.GetString("account") ?? string.Empty;
            _worker = data.GetString("worker") ?? string.Empty;
            if (data.ContainsKey("weight"))
                _weight = data["weight"].ExtractDecimal();
            if (data.ContainsKey("minprofit"))
                _minProfit = data["minprofit"].ExtractDecimal();
            _param1 = data.GetString("sparam1") ?? data.GetString("param1") ?? string.Empty;
            _param2 = data.GetString("sparam2") ?? data.GetString("param2") ?? string.Empty;
            _param3 = data.GetString("sparam3") ?? data.GetString("param3") ?? string.Empty;
        }

        protected string ProcessedSubstitutions(string raw, AlgorithmEntry algo)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw
                .Replace("_ACCOUNT_", _account)
                .Replace("_WORKER_", _worker);
            return ProcessCommon(raw, algo);
        }

        protected string ProcessedDonationSubstitutions(string raw, AlgorithmEntry algo)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw
                .Replace("_ACCOUNT_", DonationAccount)
                .Replace("_WORKER_", DonationWorker);
            return ProcessCommon(raw, algo);
        }

        private string ProcessCommon(string raw, AlgorithmEntry algo)
        {
            return raw
                .Replace("_PARAM1_", _param1)
                .Replace("_PARAM2_", _param2)
                .Replace("_PARAM3_", _param3)
                .Replace("_SPARAM1_", _param1)
                .Replace("_SPARAM2_", _param2)
                .Replace("_SPARAM3_", _param3)
                .Replace("_APARAM1_", algo.Param1)
                .Replace("_APARAM2_", algo.Param2)
                .Replace("_APARAM3_", algo.Param3);
        }

        protected TEntry CreateEntry(Dictionary<string, object> item)
        {
            string algoName = item.GetString("algo");
            AlgorithmEntry algo = MiningEngine.AlgorithmEntries.Single(o => o.Name == algoName);

            TEntry entry = new TEntry
            {
                MiningEngine = MiningEngine,
                ServiceEntry = this,
                AlgoName = algoName,
                Name = algo.Display,
                PriceId = item.GetString("priceid"),
                MinProfit = _minProfit,
                Hashrate = algo.Hashrate,
                Power = algo.Power,
                Priority = algo.Priority,
                Affinity = algo.Affinity,
                Weight = _weight,
                Folder = ProcessedSubstitutions(item.GetString("folder"), algo) ?? string.Empty,
                Command = ProcessedSubstitutions(item.GetString("command"), algo),
                Arguments = ProcessedSubstitutions(item.GetString("arguments"), algo) ?? string.Empty
            };


            if (item.ContainsKey("usewindow"))
                entry.UseWindow = bool.Parse(item["usewindow"].ToString());
            if (!string.IsNullOrWhiteSpace(DonationAccount))
            {
                entry.DonationFolder = ProcessedDonationSubstitutions(item.GetString("folder"), algo) ?? string.Empty;
                entry.DonationCommand = ProcessedDonationSubstitutions(item.GetString("command"), algo);
                entry.DonationArguments = ProcessedDonationSubstitutions(item.GetString("arguments"), algo) ??
                                          string.Empty;
            }

            return entry;
        }

        protected void Add(TEntry entry)
        {
            MiningEngine.PriceEntries.Add(entry);
        }

        private string GetAlgoName(string name)
        {
            if (AlgoTranslations == null || !AlgoTranslations.ContainsKey(name)) return name;
            return AlgoTranslations[name];
        }

        protected TEntry GetEntry(string algo)
        {
            return
                PriceEntries.FirstOrDefault(
                    o =>
                        (o.PriceId != null && o.PriceId == algo) ||
                        (o.PriceId == null && o.AlgoName == GetAlgoName(algo)));
        }

        protected void ClearStalePrices()
        {
            if (!LastUpdated.HasValue || LastUpdated.Value.AddMinutes(30) > DateTime.Now) return;

            foreach (TEntry entry in PriceEntries)
                entry.Price = 0;
        }

        public void UpdateHistory()
        {
            ServiceHistory serviceHistory = ServiceHistory;
            if(serviceHistory == null) return;

            IList<TEntry> priceEntries = PriceEntries;
            foreach (TEntry entry in priceEntries)
                serviceHistory.UpdatePrice(entry);
        }
    }
}