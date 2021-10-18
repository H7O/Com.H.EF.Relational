﻿using Com.H.Data;
using Com.H.Data.EF.Relational;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.EF.Relational
{
    /// <summary>
    /// Direct (pure TSQL) transactional commands payload builder.
    /// </summary>
    public class CmdTransaction
    {
        public string OpeningTemplate { get; set; } = "SET NOCOUNT ON;\r\n"
            + "set xact_abort on\r\n"
            + "begin try\r\nbegin transaction\r\n";

        public string ClosingTemplate { get; set; } = "\r\nend try\r\nbegin catch"
                + "\r\nrollback transaction;\r\nthrow;\r\nend catch\r\ncommit transaction;\r\n";

        private int markersCount = 0;

        private List<QueryParams> QParams { get; set; }
        private StringBuilder Query { get; set; }
        public CmdTransaction()
        {
            this.ClearQueries();
        }

        public void AddQuery(
            string query,
            object queryParams,
            string openMarker = "{{",
            string closeMarker = "}}",
            string nullReplacement = "null")
        {
            if (string.IsNullOrEmpty(query)) return;
            markersCount++;
            string dstOpenMarker = $"{openMarker}_{markersCount}_";
            string dstCloseMarker = $"_{markersCount}_{closeMarker}";

            string adjustedQuery = query.ReplaceQueryParameterMarkers(
                openMarker, closeMarker, dstOpenMarker, dstCloseMarker);
            QueryParams qParams = new QueryParams()
            {
                OpenMarker = dstOpenMarker,
                CloseMarker = dstCloseMarker,
                DataModel = queryParams,
                NullReplacement = nullReplacement
            };
            this.AddQuery(adjustedQuery, qParams);
        }
        public void AddQuery(
            string query,
            QueryParams queryParams)
        {
            this.Query.Append($"\r\n{query}");
            this.QParams.Add(queryParams);
        }

        public void Execute(DbContext dc)
        {
            var query = this.Query.ToString() + this.ClosingTemplate;
            dc.ExecuteCommand(query, this.QParams);
            this.ClearQueries();
        }
        private void ClearQueries()
        {
            this.QParams = new List<QueryParams>();
            this.Query = new StringBuilder(this.OpeningTemplate);
            this.markersCount = 0;
        }

    }
}
