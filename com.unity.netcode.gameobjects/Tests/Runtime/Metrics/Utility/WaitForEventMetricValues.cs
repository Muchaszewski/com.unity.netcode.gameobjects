#if MULTIPLAYER_TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Unity.Multiplayer.Tools.MetricTypes;
using Unity.Multiplayer.Tools.NetStats;

namespace Unity.Netcode.RuntimeTests.Metrics.Utility
{
    internal class WaitForEventMetricValues<TMetric> : WaitForMetricValues<TMetric>
    {
        IReadOnlyCollection<TMetric> m_EventValues;

        public delegate bool EventFilter(TMetric metric);
        EventFilter m_EventFilterDelegate;

        public WaitForEventMetricValues(IMetricDispatcher dispatcher, DirectionalMetricInfo directionalMetricName)
            : base(dispatcher, directionalMetricName)
        {
        }

        public WaitForEventMetricValues(IMetricDispatcher dispatcher, DirectionalMetricInfo directionalMetricName, EventFilter eventFilter)
            : this(dispatcher, directionalMetricName)
        {
            m_EventFilterDelegate = eventFilter;
        }

        public IReadOnlyCollection<TMetric> AssertMetricValuesHaveBeenFound()
        {
            AssertHasError();
            AssertIsFound();

            return m_EventValues;
        }

        public override void Observe(MetricCollection collection)
        {
            if (FindMetric(collection, out var metric))
            {
                var typedMetric = metric as IEventMetric<TMetric>;
                if (typedMetric == default)
                {
                    SetError(metric);
                    return;
                }

                if (typedMetric.Values.Any())
                {
                    // Apply filter if one was provided
                    m_EventValues = m_EventFilterDelegate != null ? typedMetric.Values.Where(x => m_EventFilterDelegate(x)).ToList() : typedMetric.Values.ToList();
                    m_Found = m_EventValues.Count > 0;
                }
            }
        }
    }
}
#endif
