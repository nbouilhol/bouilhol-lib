﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI.DataVisualization.Charting;
using System.Web.UI;
using System.IO;
using System.Globalization;

namespace Mvc.Helper
{
    public class MvcChart : IDisposable, IHtmlString 
    {
        public Chart Chart { get; set; }

        public MvcChart(Page page, Chart chart)
        {
            chart.Page = page;
            Chart = chart;
        }

        public override string ToString()
        {
            return RenderChart();
        }

        public string ToHtmlString()
        {
            return RenderChart();
        }

        /// <summary>
        /// Renders control to a string.
        /// </summary>
        /// <returns></returns>
        public string RenderChart()
        {
            using (StringWriter swriter = new StringWriter(CultureInfo.CurrentCulture))
            {
                using (HtmlTextWriter writer = new HtmlTextWriter(swriter))
                    Chart.RenderControl(writer);
                return swriter.ToString();
            }
        }

        public void Dispose()
        {
            Chart.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}