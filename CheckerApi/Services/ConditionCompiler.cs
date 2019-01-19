﻿using System;
using System.Collections.Generic;
using System.Linq;
using CheckerApi.Context;
using CheckerApi.Models.DTO;
using CheckerApi.Models.Entities;
using CheckerApi.Services.Conditions;
using CheckerApi.Services.Interfaces;

namespace CheckerApi.Services
{
    public class ConditionCompiler: IConditionCompiler
    {
        private readonly IServiceProvider _serviceProvider;

        public ConditionCompiler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IEnumerable<AlertDTO> Check(IEnumerable<IEnumerable<BidEntry>> orders, ApiConfiguration config, IEnumerable<ConditionSetting> settings)
        {
            var foundOrders = new List<AlertDTO>();
            var foundOrdersIDs = new HashSet<string>();

            // Conditions are in order of priority
            var conditions = Registry.GetConditions().OrderBy(Registry.GetPriority).ToList();
            foreach (var conditionEntry in conditions)
            {
                var setting = settings.FirstOrDefault(s => s.ConditionName == conditionEntry.Name);
                if (setting != null && setting.Enabled)
                {
                    var data = new List<AlertDTO>();
                    ICondition condition = (ICondition) Activator.CreateInstance(conditionEntry, args: _serviceProvider);
                    if (conditionEntry.IsDefined(typeof(GlobalConditionAttribute), false))
                    {
                        data = condition.Compute(orders.SelectMany(o => o), config).ToList();
                    }
                    else
                    {
                        foreach (var order in orders)
                        {
                            data.AddRange(condition.Compute(order, config));
                        }
                    }

                    foreach (var alert in data)
                    {
                        // Avoid duplicate alerts
                        var sig = $"{alert.BidEntry.NiceHashId}{alert.BidEntry.NiceHashDataCenter}";
                        if (!foundOrdersIDs.Contains(sig))
                        {
                            foundOrdersIDs.Add(sig);
                            foundOrders.Add(alert);
                        }
                    }
                }
            }

            return foundOrders;
        }
    }
}