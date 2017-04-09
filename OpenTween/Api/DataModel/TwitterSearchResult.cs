﻿// OpenTween - Client of Twitter
// Copyright (c) 2014 kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
//
// This file is part of OpenTween.
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OpenTween.Api.DataModel
{
    [DataContract]
    public class TwitterSearchResult
    {
        [DataMember(Name = "statuses")]
        public TwitterStatus[] Statuses { get; set; }

        [DataMember(Name = "search_metadata")]
        public TwitterSearchResult.Metadata SearchMetadata { get; set; }

        [DataContract]
        public class Metadata
        {
            [DataMember(Name = "max_id")]
            public long MaxId { get; set; }

            [DataMember(Name = "max_id_str")]
            public string MaxIdStr { get; set; }

            [DataMember(Name = "since_id")]
            public long SinceId { get; set; }

            [DataMember(Name = "since_id_str")]
            public string SinceIdStr { get; set; }

            [DataMember(Name = "refresh_url")]
            public string RefreshUrl { get; set; }

            [DataMember(Name = "next_results")]
            public string NextResults { get; set; }

            [DataMember(Name = "count")]
            public int Count { get; set; }

            [DataMember(Name = "completed_in")]
            public double CompletedIn { get; set; }

            [DataMember(Name = "query")]
            public string Query { get; set; }
        }

        /// <exception cref="SerializationException"/>
        public static TwitterSearchResult ParseJson(string json)
        {
            return MyCommon.CreateDataFromJson<TwitterSearchResult>(json);
        }
    }
}
