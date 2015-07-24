﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MinerControl.Services
{
    public interface IService : INotifyPropertyChanged
    {
        MiningEngine MiningEngine { get; set; }
        string ServiceName { get; }
        DateTime? LastUpdated { get; }
        decimal Balance { get; }
        decimal Currency { get; }

        string ServicePrint { get; }
        string LastUpdatedPrint { get; }
        string BalancePrint { get; }
        string CurrencyPrint { get; }
        string TimeMiningPrint { get; }

        void Initialize(IDictionary<string, object> data);
        void CheckPrices();
        void UpdateHistory(bool error = false);
        void UpdateTime();
        void UpdateExchange();
    }
}